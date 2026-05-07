using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
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
    ///       → Composite(NearbyParallelMerge + EqualExtensionLength) → Q → AcadRenderer。
    /// 当前阶段：Phase 4——外部/内部派生 + 0 标注根因消除（见 OutsideDimensionDeriver.CollapseSamePrimary）
    ///           + 同尺寸近距合并（DimDistanceTolerance 控制）+ ExtensionLine 等长。
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
            _qualityFunction = qualityFunction ?? new NoOpQualityFunction();
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
                    var acadPolyline = tr.GetObject(polylineId, OpenMode.ForRead) as Polyline;
                    if (acadPolyline == null)
                    {
                        ed.WriteMessage("\n[NewDDS] 选择的不是多段线，已跳过。");
                        tr.Commit();
                        return;
                    }

                    var config = NewDdsConfigAdapter.FromSettingsPanel();

                    var polyline2D = AcadPolylineConverter.ToPolyline2D(
                        acadPolyline, config.BulgeTessellatePrecision);

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
                    result.Diagnostics = context.Diagnostics
                        .Concat(features.Diagnostics)
                        .ToList();

                    int written = _renderer.Render(result, doc, tr);

                    int hCols = features.HorizontalSecantColumns?.Count ?? 0;
                    int vCols = features.VerticalSecantColumns?.Count ?? 0;
                    int hEdges = SumEdges(features.HorizontalSecantColumns);
                    int vEdges = SumEdges(features.VerticalSecantColumns);
                    ed.WriteMessage(
                        $"\n[NewDDS] Phase 4 链路：" +
                        $"Polyline(原顶点={acadPolyline.NumberOfVertices} 闭={acadPolyline.Closed}) " +
                        $"→ tessellated(顶点={polyline2D.VertexCount}) " +
                        $"| 配置(内={config.DimensionDistanceInside:F0} 外={config.DimensionDistanceOutside:F0} " +
                        $"带尺寸={config.DimensionDistanceWithDim:F0} 容差={config.DimDistanceTolerance:F0}) " +
                        $"| 步长={context.EffectiveStep:F1} " +
                        $"| 特征 H={hCols}列/{hEdges}段 V={vCols}列/{vEdges}段 " +
                        $"| 派生={raw.Count} 后处理={clean.Count} 写入={written} " +
                        $"| Q={result.ScoreCard.Overall:F2}");

                    if (result.Diagnostics.Count > 0)
                    {
                        ed.WriteMessage($"\n[NewDDS] 诊断({result.Diagnostics.Count})：" +
                            string.Join(" / ", result.Diagnostics.Take(4)));
                    }

                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[NewDDS] 链路异常：{ex.GetType().Name} - {ex.Message}");
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

        #region NoOp 默认实现 —— Phase 3b / 4 / 6 逐阶段替换

        private sealed class NoOpQualityFunction : IQualityFunction
        {
            public QualityScoreCard Evaluate(NewDdsResult result, BoundaryFeatures features)
                => new QualityScoreCard();
        }

        #endregion
    }
}
