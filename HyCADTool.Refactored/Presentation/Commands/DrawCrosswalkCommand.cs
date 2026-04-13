using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using System.Collections.Generic;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 人行横道绘制命令 (hyRoad)
    /// 选择交叉口道路边线 → 分析几何 → 绘制辅助线/偏移线/斑马线/停止线
    /// 输入值直接使用绘图单位（默认 m）。
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
                ed.WriteMessage("\n选择交叉口道路边线（直线 + 圆弧，多选少选均可自动过滤）");
                var filter = new SelectionFilter(new[]
                {
                    new TypedValue((int)DxfCode.Start, "LINE,ARC")
                });
                var selRes = ed.GetSelection(
                    new PromptSelectionOptions { MessageForAdding = "\n选择道路边线: " },
                    filter);
                if (selRes.Status != PromptStatus.OK) return;

                var d1 = GetPositiveDouble(ed, "空隙宽度 (Line1→Line2)", 5.0);
                if (d1 == null) return;

                var d2 = GetPositiveDouble(ed, "人行横道宽度 (Line2→Line3)", 5.0);
                if (d2 == null) return;

                var d3 = GetPositiveDouble(ed, "停止线距离 (Line3→Line4)", 2.0);
                if (d3 == null) return;

                var spacing = GetPositiveDouble(ed, "斑马线间距", 1.0);
                if (spacing == null) return;

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

                    ed.WriteMessage($"\n选中 {analysis.TotalLines} 线 + {analysis.TotalArcs} 弧 → " +
                        $"连接 {analysis.ConnectedLineCount} 线, 忽略 {analysis.IgnoredLineCount} 线, " +
                        $"有效角点 {analysis.ValidArcCount}, 跳过弧 {analysis.SkippedArcCount} → " +
                        $"{analysis.Arms.Count} 个方向");

                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    service.DrawAuxiliaryLines(tr, ms, analysis.Arms, LayerAux);

                    int okCount = 0;
                    for (int idx = 0; idx < analysis.Arms.Count; idx++)
                    {
                        var arm = analysis.Arms[idx];
                        var roadW = arm.LeftCorner.DistanceTo(arm.RightCorner);
                        ed.WriteMessage($"\n  方向{idx + 1}: 路宽={roadW:F2}, " +
                            $"L=({arm.LeftCorner.X:F2},{arm.LeftCorner.Y:F2}), " +
                            $"R=({arm.RightCorner.X:F2},{arm.RightCorner.Y:F2}), " +
                            $"→({arm.OutwardDirection.X:F2},{arm.OutwardDirection.Y:F2})");

                        try
                        {
                            service.DrawCrosswalkForArm(tr, ms, arm,
                                d1.Value, d2.Value, d3.Value, spacing.Value,
                                LayerAux, LayerCrosswalk, LayerStopLine);
                            ed.WriteMessage(" OK");
                            okCount++;
                        }
                        catch (System.Exception armEx)
                        {
                            ed.WriteMessage($" 失败: {armEx.Message}");
                        }
                    }

                    tr.Commit();
                    ed.WriteMessage($"\n完成 {okCount}/{analysis.Arms.Count} 个方向, " +
                        $"参数 {d1.Value}/{d2.Value}/{d3.Value}/{spacing.Value}");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n绘制失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private double? GetPositiveDouble(Editor ed, string prompt, double defaultValue)
        {
            var opts = new PromptDoubleOptions($"\n{prompt} <{defaultValue}>: ")
            {
                AllowNone = true,
                AllowZero = false,
                AllowNegative = false,
                DefaultValue = defaultValue,
                UseDefaultValue = true
            };

            var result = ed.GetDouble(opts);
            if (result.Status == PromptStatus.None)
                return defaultValue;
            if (result.Status == PromptStatus.OK)
                return result.Value;
            return null;
        }
    }
}
