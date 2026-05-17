using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Features.AcadDimension.Domain.Boundary;
using HyCADTool.Features.AcadDimension.Domain.Config;
using HyCADTool.Features.AcadDimension.Domain.Context;
using HyCADTool.Features.AcadDimension.Domain.Derivation;
using HyCADTool.Features.AcadDimension.Domain.PostProcessing;
using HyCADTool.Features.AcadDimension.Domain.Results;
using HyCADTool.Features.AcadDimension.Validation;
using HyCADTool.Shared.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.AcadDimension.Services
{
    /// <summary>
    /// NewDDS 编排服务（无状态——所有 per-run 状态进 NewDdsContext，修 06 §7 #10）。
    /// 链路：拾取 → ConfigAdapter → Bulge tessellate → Context.Init
    ///       → SweepLine → Composite(Outside + Inside)
    ///       → Composite(NearbyParallelMerge + EqualExtensionLength) → Coverage Q → AcadRenderer（Phase 5 写扩展字典）。
    /// 当前阶段：Phase 5——每条 RotatedDimension 扩展字典 HyCAD_NewDDS（源 Polyline Handle + FeatureSignature + Config）；
    ///           Phase 6 完整——FullQualityFunction（5 条规则：Coverage / NoOverlap / WithinBounds / Consistency / DirectionalCoverage）；
    ///           Phase 8（nddsR）——比对 FeatureSignature 自动重生成。
    /// </summary>
    public sealed class NewDdsService
    {
        private readonly IBoundaryFeatureExtractor _extractor;
        private readonly IDimensionDeriver _deriver;
        private readonly IDimensionPostProcessor _postProcessor;
        private readonly IQualityFunction _qualityFunction;
        private readonly INewDdsRenderer _renderer;

        public NewDdsService(
            IBoundaryFeatureExtractor extractor = null,
            IDimensionDeriver deriver = null,
            IDimensionPostProcessor postProcessor = null,
            IQualityFunction qualityFunction = null,
            INewDdsRenderer renderer = null)
        {
            _extractor = extractor ?? new SweepLineFeatureExtractor();
            _deriver = deriver ?? new CompositeDimensionDeriver(
                new OutsideDimensionDeriver(),
                new InsideDimensionDeriver());
            _postProcessor = postProcessor ?? new CompositeDimensionPostProcessor(
                new NearbyParallelMergePostProcessor(),
                new EqualExtensionLengthPostProcessor());
            _qualityFunction = qualityFunction ?? new FullQualityFunction();
            _renderer = renderer ?? new AcadDimensionRenderer();
        }

        /// <summary>
        /// 单条多段线入口（ndds）。
        /// 设计要点（修 06 §7 #6 #9）：
        ///  - 入参用 ObjectId，Service 自身控制事务边界 → Renderer 失败整体回滚；
        ///  - LockDocument 防多文档并发污染；
        ///  - 命令行打印各阶段计数证明链路打通。
        /// </summary>
        public void Execute(ObjectId polylineId)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;
            if (ed == null) return;
            if (polylineId.IsNull || polylineId.IsErased)
            {
                ed.WriteMessage("\n[NewDDS] 多段线 ObjectId 无效，已跳过。");
                return;
            }

            try
            {
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var run = RunInTransaction(doc, tr, polylineId);
                    if (run == null)
                    {
                        ed.WriteMessage("\n[NewDDS] 选择的不是多段线，已跳过。");
                        tr.Commit();
                        return;
                    }
                    LogRun(ed, run, prefix: "Phase 6 M1 + Phase 5 绑定");
                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[NewDDS] 链路异常：{ex.GetType().Name} - {ex.Message}");
            }
        }

        /// <summary>
        /// 在调用方提供的事务里执行一遍完整链路并写入标注。<paramref name="polylineId"/> 不是 Polyline 时返回 null。
        /// 用于 nddsR 在单事务内对多条多段线连续重生成；调用方负责事务的 Commit/Abort 与文档锁。
        /// </summary>
        public NewDdsRunOutcome RunInTransaction(Document doc, Transaction tr, ObjectId polylineId)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (polylineId.IsNull || polylineId.IsErased) return null;

            var acadPolyline = tr.GetObject(polylineId, OpenMode.ForRead) as Polyline;
            if (acadPolyline == null) return null;

            var config = NewDdsConfigAdapter.FromSettingsPanel();
            var polyline2D = AcadPolylineConverter.ToPolyline2D(acadPolyline, config.BulgeTessellatePrecision);

            var context = new NewDdsContext
            {
                Boundary = polyline2D,
                Bounds = default(BoundingBox),
                EffectiveStep = config.StepStrategy == StepStrategy.Fixed ? config.FixedStepValue : 0.0
            };

            var features = _extractor.Extract(context.Boundary, config, context);
            var raw = _deriver.Derive(features, config);
            var clean = _postProcessor.Process(raw, config);

            var result = new NewDdsResult { Dimensions = clean };
            result.ScoreCard = _qualityFunction.Evaluate(result, features);
            result.Diagnostics = context.Diagnostics.Concat(features.Diagnostics).ToList();

            var featSig = NewDdsFeatureSignature.Compute(polyline2D);
            var bindCtx = new NewDdsRenderBindingContext
            {
                SourcePolylineId = polylineId,
                FeatureSignature = featSig,
                ConfigSnapshot = config
            };

            int written = _renderer.Render(result, doc, tr, bindCtx);

            return new NewDdsRunOutcome
            {
                AcadPolyline = acadPolyline,
                Polyline2DVertexCount = polyline2D.VertexCount,
                Config = config,
                Context = context,
                Features = features,
                RawCount = raw.Count,
                Result = result,
                FeatureSignature = featSig,
                Written = written
            };
        }

        private static void LogRun(Editor ed, NewDdsRunOutcome run, string prefix)
        {
            int hCols = run.Features.HorizontalSecantColumns?.Count ?? 0;
            int vCols = run.Features.VerticalSecantColumns?.Count ?? 0;
            int hEdges = SumEdges(run.Features.HorizontalSecantColumns);
            int vEdges = SumEdges(run.Features.VerticalSecantColumns);
            ed.WriteMessage(
                $"\n[NewDDS] {prefix}：" +
                $"Polyline(原顶点={run.AcadPolyline.NumberOfVertices} 闭={run.AcadPolyline.Closed}) " +
                $"→ tessellated(顶点={run.Polyline2DVertexCount}) " +
                $"| 配置(内={run.Config.DimensionDistanceInside:F0} 外={run.Config.DimensionDistanceOutside:F0} " +
                $"带尺寸={run.Config.DimensionDistanceWithDim:F0} 容差={run.Config.DimDistanceTolerance:F0}) " +
                $"| 步长={run.Context.EffectiveStep:F1} " +
                $"| 特征 H={hCols}列/{hEdges}段 V={vCols}列/{vEdges}段 " +
                $"| 派生={run.RawCount} 后处理={run.Result.Dimensions.Count} 写入={run.Written} " +
                $"| Q={run.Result.ScoreCard.Overall:F2}");
            if (run.Written > 0)
            {
                string sigShort = run.FeatureSignature.Length <= 12
                    ? run.FeatureSignature
                    : run.FeatureSignature.Substring(0, 12) + "...";
                ed.WriteMessage(
                    $"\n[NewDDS] Phase 5：HyCAD_NewDDS×{run.Written}，源 Handle={run.AcadPolyline.Handle}，Sig={sigShort}");
            }
            ed.WriteMessage("\n[NewDDS] Source 分布(后处理后)：" + FormatSourceDistribution(run.Result.Dimensions));
            ed.WriteMessage(
                $"\n[NewDDS] Q 明细：Coverage={run.Result.ScoreCard.Coverage:F2} " +
                $"NoOverlap={run.Result.ScoreCard.NoOverlap:F2} " +
                $"WithinBounds={run.Result.ScoreCard.WithinBounds:F2} " +
                $"Consistency={run.Result.ScoreCard.Consistency:F2} " +
                $"DirCoverage={run.Result.ScoreCard.DirectionalCoverage:F2}");
            if (run.Result.ScoreCard.Issues.Count > 0)
            {
                ed.WriteMessage($"\n[NewDDS] Q 诊断({run.Result.ScoreCard.Issues.Count})：" +
                    string.Join(" / ", run.Result.ScoreCard.Issues));
            }
            if (run.Result.Diagnostics.Count > 0)
            {
                ed.WriteMessage($"\n[NewDDS] 几何诊断({run.Result.Diagnostics.Count})：" +
                    string.Join(" / ", run.Result.Diagnostics.Take(4)));
            }
        }

        private static int SumEdges(IReadOnlyList<IReadOnlyList<Line2D>> columns)
        {
            if (columns == null) return 0;
            int total = 0;
            for (int i = 0; i < columns.Count; i++)
                total += columns[i]?.Count ?? 0;
            return total;
        }

        // Source 分布缩写：OL/OR/OU/OD = OutsideLeft/Right/Up/Down，
        // OTL/OTR/OTU/OTD = OutsideTotal*，IL/IU = InsideLeftRight/InsideUpDown。
        private static string FormatSourceDistribution(IEnumerable<DerivedDimension> dims)
        {
            if (dims == null) return "(空)";
            var counts = dims.GroupBy(d => d.Source).ToDictionary(g => g.Key, g => g.Count());
            int total = counts.Values.Sum();
            if (total == 0) return "(空)";
            int Get(DimensionSource s) => counts.TryGetValue(s, out int v) ? v : 0;
            return $"OL={Get(DimensionSource.OutsideLeft)} OR={Get(DimensionSource.OutsideRight)} " +
                   $"OU={Get(DimensionSource.OutsideUp)} OD={Get(DimensionSource.OutsideDown)} " +
                   $"OTL={Get(DimensionSource.OutsideTotalLeft)} OTR={Get(DimensionSource.OutsideTotalRight)} " +
                   $"OTU={Get(DimensionSource.OutsideTotalUp)} OTD={Get(DimensionSource.OutsideTotalDown)} " +
                   $"IL={Get(DimensionSource.InsideLeftRight)} IU={Get(DimensionSource.InsideUpDown)} " +
                   $"| 合计={total}";
        }
    }
}
