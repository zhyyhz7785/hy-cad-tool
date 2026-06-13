using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Features.DCEL.Domain.Services;
using HyCADTool.Features.DCEL.Services;
using HyCADTool.Shared.AutoCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace HyCADTool.Features.Misc
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

        public DCELCommand(
            ICurveSegmentExtractor curveExtractor,
            IDCELBuilderService dcelBuilder,
            IDCELRenderer dcelRenderer)
        {
            _curveExtractor = curveExtractor ?? throw new ArgumentNullException(nameof(curveExtractor));
            _dcelBuilder = dcelBuilder ?? throw new ArgumentNullException(nameof(dcelBuilder));
            _dcelRenderer = dcelRenderer ?? throw new ArgumentNullException(nameof(dcelRenderer));
        }

        public DCELCommand()
        {
            _curveExtractor = HyCADTool.App.Bootstrap.ServiceLocator.Resolve<ICurveSegmentExtractor>();
            _dcelBuilder = HyCADTool.App.Bootstrap.ServiceLocator.Resolve<IDCELBuilderService>();
            _dcelRenderer = HyCADTool.App.Bootstrap.ServiceLocator.Resolve<IDCELRenderer>();
        }

        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            var ed = doc.Editor;

            try
            {
                var selectionOptions = new PromptSelectionOptions
                {
                    MessageForAdding = "\n请选择用于生成 DCEL 的曲线："
                };

                var filter = new SelectionFilter(new[]
                {
                    new TypedValue((int)DxfCode.Start, "LINE,ARC,CIRCLE,ELLIPSE,LWPOLYLINE,POLYLINE,SPLINE")
                });

                var selectionResult = ed.GetSelection(selectionOptions, filter);
                if (selectionResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未选择任何曲线。");
                    return;
                }

                var settings = DCELSettings.Current;
                var curveIds = new List<ObjectId>(selectionResult.Value.Count);
                foreach (SelectedObject selObj in selectionResult.Value)
                {
                    if (selObj != null)
                        curveIds.Add(selObj.ObjectId);
                }

                var totalSw = Stopwatch.StartNew();
                var run = DCELPipelineRunner.Run(
                    curveIds, settings, _curveExtractor, _dcelBuilder, _dcelRenderer,
                    render: true);
                totalSw.Stop();
                run.Metrics.TotalMs = totalSw.ElapsedMilliseconds;

                if (run.Metrics.SegmentCount == 0)
                {
                    ed.WriteMessage("\n没有提取到有效的线段。");
                    return;
                }

                if (run.Graph == null || run.Graph.Faces.Count == 0)
                {
                    ed.WriteMessage("\n未能构建 DCEL 图或未生成任何面。");
                    return;
                }

                var m = run.Metrics;
                ed.WriteMessage($"\nDCEL：{m.SegmentCount} 段（曲线 {m.MappingCount}）→ {m.FaceCount} 面（{m.OuterFaceCount} 外 + {m.InnerFaceCount} 内）");
                ed.WriteMessage($"\nINFO: DCEL处理 总耗时 {m.TotalMs} 毫秒（不含用户选择）");

                if (settings.VerboseTiming)
                    DCELTimingReporter.WriteDetailedTiming(ed, m);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
                ed.WriteMessage($"\n{ex.StackTrace}");
            }
        }
    }
}
