using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Refactored.Domain.Services.GeometryAlgorithms;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

[assembly: CommandClass(typeof(HyCADTool.Refactored.Presentation.Commands.DCELCommand))]

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// DCEL 命令（Doubly Connected Edge List Command）
    /// 从选定的曲线构建 DCEL 图并绘制结果
    /// 命令名：HYDCEL（统一大写，保持命名一致性）
    /// </summary>
    public class DCELCommand
    {
        private readonly ICurveSegmentExtractor _curveExtractor;
        private readonly IDCELBuilderService _dcelBuilder;
        private readonly IDCELRenderer _dcelRenderer;

        /// <summary>
        /// 构造函数（用于依赖注入）
        /// </summary>
        public DCELCommand(
            ICurveSegmentExtractor curveExtractor,
            IDCELBuilderService dcelBuilder,
            IDCELRenderer dcelRenderer)
        {
            _curveExtractor = curveExtractor ?? throw new ArgumentNullException(nameof(curveExtractor));
            _dcelBuilder = dcelBuilder ?? throw new ArgumentNullException(nameof(dcelBuilder));
            _dcelRenderer = dcelRenderer ?? throw new ArgumentNullException(nameof(dcelRenderer));
        }

        /// <summary>
        /// 默认构造函数（用于AutoCAD命令注册）
        /// </summary>
        public DCELCommand()
        {
            // 创建服务实例（简单工厂模式）
            _curveExtractor = new CurveSegmentExtractor();
            _dcelBuilder = new DCELBuilderService();
            _dcelRenderer = new DCELRenderer();
        }

        /// <summary>
        /// AutoCAD 命令入口
        /// 命令名：HYDCEL（统一大写，保持命名一致性）
        /// </summary>
        [CommandMethod("HYDCEL")]
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            var ed = doc.Editor;

            try
            {
                // 1. 提示用户选择曲线
                var selectionOptions = new PromptSelectionOptions
                {
                    MessageForAdding = "\n请选择用于生成 DCEL 的曲线："
                };

                var filter = new SelectionFilter(new[]
                {
                    new TypedValue((int)DxfCode.Start, "LINE,ARC,LWPOLYLINE,POLYLINE,SPLINE")
                });

                var selectionResult = ed.GetSelection(selectionOptions, filter);

                if (selectionResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未选择任何曲线。");
                    return;
                }

                // 版本标识：新简化架构
                ed.WriteMessage("\n[HYDCEL v4.1 - 端点对齐修复 2025-10-30]");
                
                // 显示当前配置
                var settings = Domain.Services.DCELSettings.Current;
                ed.WriteMessage($"\n配置: Arc={settings.ArcSegmentCount?.ToString() ?? "自动"}, " +
                               $"Ellipse={settings.EllipseSegmentCount?.ToString() ?? "自动"}, " +
                               $"Spline={settings.SplineSegmentCount?.ToString() ?? "自动"}, " +
                               $"恢复原曲线={settings.RestoreOriginalCurves}");

                var selectionSet = selectionResult.Value;

                // ⏱️ 性能测量开始（用户选择完成后）
                var stopwatch = Stopwatch.StartNew();

                // 2. 提取曲线 ID
                var sw0 = Stopwatch.StartNew();
                var curveIds = new List<ObjectId>();
                foreach (SelectedObject selObj in selectionSet)
                {
                    if (selObj != null)
                    {
                        curveIds.Add(selObj.ObjectId);
                    }
                }
                sw0.Stop();
                long collectTime = sw0.ElapsedMilliseconds;

                // 3. 提取并简化所有曲线（使用全局配置）
                var tolerance = 0.01;
                var sw1 = Stopwatch.StartNew();
                var simplificationService = new Domain.Services.CurveSimplificationService();
                var (segments, mappings) = _curveExtractor.ExtractAndSimplify(
                    curveIds, 
                    simplificationService, 
                    tolerance,
                    arcSegmentCount: settings.ArcSegmentCount,
                    ellipseSegmentCount: settings.EllipseSegmentCount,
                    splineSegmentCount: settings.SplineSegmentCount);
                
                sw1.Stop();
                long extractTime = sw1.ElapsedMilliseconds;

                if (segments.Count == 0)
                {
                    ed.WriteMessage("\n没有提取到有效的线段。");
                    return;
                }

                // 4. 构建 DCEL 图（使用简化后的线段）
                var sw3 = Stopwatch.StartNew();
                var graph = _dcelBuilder.BuildFromSegments(segments, new Tolerance(tolerance));
                sw3.Stop();
                long buildTime = sw3.ElapsedMilliseconds;

                if (graph == null || graph.Faces.Count == 0)
                {
                    ed.WriteMessage("\n未能构建 DCEL 图或未生成任何面。");
                    return;
                }

                // 5. 统计信息
                var sw5 = Stopwatch.StartNew();
                var stats = graph.GetStatistics();
                var outerCount = graph.Faces.Count(f => f.IsOuter);
                var innerCount = stats.FaceCount - outerCount;
                sw5.Stop();
                long statsTime = sw5.ElapsedMilliseconds;

                // 6. 验证拓扑一致性
                var sw6 = Stopwatch.StartNew();
                var isValid = graph.Validate(out var errors);
                sw6.Stop();
                long validateTime = sw6.ElapsedMilliseconds;

#if DEBUG
                if (!isValid)
                {
                    ed.WriteMessage($"\n⚠️ 拓扑验证失败（{errors.Count} 个问题）");
                    foreach (var error in errors)
                        ed.WriteMessage($"\n  - {error}");
                }
#endif

                // 7. 渲染到 AutoCAD
                var sw4 = Stopwatch.StartNew();
                
                if (mappings.Count > 0)
                {
                    // 包含曲线，使用曲线恢复渲染（根据用户配置）
                    _dcelRenderer.RenderWithMappings(graph, mappings, "dcelOuter", "dcelInner", restoreOriginal: settings.RestoreOriginalCurves);
                }
                else
                {
                    // 纯直线，使用简单渲染
                    _dcelRenderer.Render(graph, "dcelOuter", "dcelInner");
                }
                
                sw4.Stop();
                long renderTime = sw4.ElapsedMilliseconds;
                
                // 统计
                var sw7 = Stopwatch.StartNew();
                var curveStats = new Dictionary<string, int>
                {
                    ["Line"] = segments.Count,
                    ["SimplifiedCurves"] = mappings.Count
                };
                var arcCount = mappings.Count(m => m.OriginalType == CurveSegmentType.Arc);
                var ellipseCount = mappings.Count(m => m.OriginalType == CurveSegmentType.Ellipse);
                var splineCount = mappings.Count(m => m.OriginalType == CurveSegmentType.Spline);
                if (arcCount > 0) curveStats["Arc"] = arcCount;
                if (ellipseCount > 0) curveStats["Ellipse"] = ellipseCount;
                if (splineCount > 0) curveStats["Spline"] = splineCount;
                sw7.Stop();
                long groupTime = sw7.ElapsedMilliseconds;
                
                stopwatch.Stop();
                long totalTime = stopwatch.ElapsedMilliseconds;
                long otherTime = totalTime - collectTime - extractTime - buildTime - statsTime - validateTime - renderTime - groupTime;
                
                ed.WriteMessage($"\n曲线统计：{string.Join(", ", curveStats.Select(kv => $"{kv.Key}:{kv.Value}"))}");
                ed.WriteMessage($"\nDCEL 完成：{stats.FaceCount} 面（{outerCount} 外 + {innerCount} 内）");
                ed.WriteMessage($"\n━━━━━━━━━━ 详细性能分析 ━━━━━━━━━━");
                ed.WriteMessage($"\n  1. ID收集     : {collectTime}ms ({collectTime * 100.0 / totalTime:F1}%)");
                ed.WriteMessage($"\n  2. 提取+简化  : {extractTime}ms ({extractTime * 100.0 / totalTime:F1}%)");
                ed.WriteMessage($"\n  3. DCEL构建   : {buildTime}ms ({buildTime * 100.0 / totalTime:F1}%)");
                ed.WriteMessage($"\n  4. 统计信息   : {statsTime}ms ({statsTime * 100.0 / totalTime:F1}%)");
                ed.WriteMessage($"\n  5. 拓扑验证   : {validateTime}ms ({validateTime * 100.0 / totalTime:F1}%)");
                ed.WriteMessage($"\n  6. 渲染       : {renderTime}ms ({renderTime * 100.0 / totalTime:F1}%)");
                ed.WriteMessage($"\n  7. 其他开销   : {otherTime}ms ({otherTime * 100.0 / totalTime:F1}%)");
                ed.WriteMessage($"\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                ed.WriteMessage($"\nINFO: DCEL处理 总耗时 {totalTime} 毫秒（不含用户选择）");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
                ed.WriteMessage($"\n{ex.StackTrace}");
            }
        }
    }
}


