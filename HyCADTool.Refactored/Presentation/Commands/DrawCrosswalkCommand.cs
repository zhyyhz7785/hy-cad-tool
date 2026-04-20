using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using HyCADTool.Refactored.Presentation.Commands.Road;
using System.Collections.Generic;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 人行横道绘制命令 (hyRoad)
    /// 先弹出 WPF 参数窗，确定后从选中的 Line/Arc 分析交叉口并绘制；绘图单位 = m。
    /// </summary>
    public class DrawCrosswalkCommand
    {
        private const string LayerAux = "0-road-辅助线";
        private const short LayerAuxColor = 6;

        private const string LayerCrosswalk = "0-road-标线-人行横道-实线";
        private const short LayerCrosswalkColor = 7;

        private const string LayerStopLine = "0-road-标线-停止线";
        private const short LayerStopLineColor = 7;

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                if (!CrosswalkDrawParamsDialog.TryShow(out double d1, out double d2, out double d3, out double spacing))
                {
                    ed.WriteMessage("\n人行横道: 已取消。");
                    return;
                }

                ed.WriteMessage($"\n人行横道: 空隙={d1} 宽度={d2} 停止线={d3} 间距={spacing} (m)");
                ed.WriteMessage("\n选择交叉口道路边线（直线 + 圆弧，多选少选均可自动过滤）");

                var filter = new SelectionFilter(new[]
                {
                    new TypedValue((int)DxfCode.Start, "LINE,ARC")
                });
                var selRes = ed.GetSelection(
                    new PromptSelectionOptions { MessageForAdding = "\n选择道路边线: " },
                    filter);
                if (selRes.Status != PromptStatus.OK) return;

                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var layerMgr = new LayerManager();
                    layerMgr.EnsureLayers(tr,
                        (LayerAux, LayerAuxColor),
                        (LayerCrosswalk, LayerCrosswalkColor),
                        (LayerStopLine, LayerStopLineColor));

                    var lines = new List<Line>();
                    var arcs = new List<Arc>();
                    foreach (var objId in selRes.Value.GetObjectIds())
                    {
                        var ent = tr.GetObject(objId, OpenMode.ForRead);
                        if (ent is Line line) lines.Add(line);
                        else if (ent is Arc arc) arcs.Add(arc);
                    }

                    var service = new CrosswalkService();
                    var analysis = service.AnalyzeIntersection(lines, arcs);

                    ed.WriteMessage($"\n分析: {analysis.ConnectedLineCount} 线, " +
                        $"{analysis.ValidArcCount} 弧 → {analysis.Arms.Count} 个方向");

                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    service.DrawAuxiliaryLines(tr, ms, analysis.Arms, LayerAux);

                    int okCount = 0;
                    for (int idx = 0; idx < analysis.Arms.Count; idx++)
                    {
                        var arm = analysis.Arms[idx];
                        try
                        {
                            service.DrawCrosswalkForArm(tr, ms, arm,
                                d1, d2, d3, spacing,
                                LayerAux, LayerCrosswalk, LayerStopLine);
                            okCount++;
                        }
                        catch (System.Exception armEx)
                        {
                            ed.WriteMessage($"\n  方向{idx + 1} 失败: {armEx.Message}");
                        }
                    }

                    tr.Commit();
                    ed.WriteMessage($"\n完成 {okCount}/{analysis.Arms.Count} 个方向");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n绘制失败: {ex.Message}");
            }
        }
    }
}
