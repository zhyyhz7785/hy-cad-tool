using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
[assembly: CommandClass(typeof(HyCADTool.Command.DrawBaseReinforcement))]
namespace HyCADTool.Command
{
    public static partial class DrawBaseReinforcement
    {
        public static Dictionary<Polyline, (int Count, double Min, double Max, double Average, double StdDev)> ReinforcementStepA()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            // 用户选择图形中两个指定图层的数据
            var selection = GetUserSelectionForReinforcement(ed);
            if (selection == null) return null;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var polylines = new List<Polyline>();
                var texts = new List<DBText>();
                foreach (var id in selection.GetObjectIds())
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity != null)
                    {
                        if (entity.Layer == "00_hy_调整配筋轮廓" && entity is Polyline)
                        {
                            polylines.Add((Polyline)entity);
                        }
                        else if (entity.Layer == "筏板板元配筋标注" && entity is DBText)
                        {
                            texts.Add((DBText)entity);
                        }
                    }
                }
                // 创建字典 key为Polyline ，value为文字内容转为double的集合
                var polylineTextMap = new Dictionary<Polyline, List<double>>();
                foreach (var polyline in polylines)
                {
                    var textValues = new List<double>();
                    foreach (var text in texts)
                    {
                        if (IsPointInsidePolyline(polyline, text.Position))
                        {
                            if (double.TryParse(text.TextString, out double value))
                            {
                                textValues.Add(value);
                            }
                        }
                    }
                    if (textValues.Count > 0)
                    {
                        textValues.Sort();
                        polylineTextMap[polyline] = textValues;
                    }
                }
                // 创建新字典，包含统计信息
                var statsMap = new Dictionary<Polyline, (int Count, double Min, double Max, double Average, double StdDev)>();
                foreach (var kvp in polylineTextMap)
                {
                    var values = kvp.Value;
                    int count = values.Count;
                    double min = values.Min();
                    double max = values.Max();
                    double average = values.Average();
                    double stdDev = CalculateStandardDeviation(values, average);
                    statsMap[kvp.Key] = (count, min, max, average, stdDev);
                }
                tr.Commit();
                return statsMap;
            }
        }
        private static SelectionSet GetUserSelectionForReinforcement(Editor ed)
        {
            var pso = new PromptSelectionOptions
            {
                MessageForAdding = "请选择'00_hy_调整配筋轮廓'图层的Polyline和'筏板板元配筋标注'图层的文字:"
            };
            var filterList = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.LayerName, "00_hy_调整配筋轮廓"),
                new TypedValue((int)DxfCode.LayerName, "筏板板元配筋标注"),
                new TypedValue((int)DxfCode.Operator, "OR>")
            };
            var filter = new SelectionFilter(filterList);
            var psr = ed.GetSelection(pso, filter);
            if (psr.Status == PromptStatus.OK)
            {
                return psr.Value;
            }
            ed.WriteMessage("未选择任何对象。\n");
            return null;
        }
        private static bool IsPointInsidePolyline(Polyline polyline, Point3d point)
        {
            int crossings = 0;
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                Point3d p1 = polyline.GetPoint3dAt(i);
                Point3d p2 = polyline.GetPoint3dAt((i + 1) % polyline.NumberOfVertices);
                if (((p1.Y <= point.Y && point.Y < p2.Y) || (p2.Y <= point.Y && point.Y < p1.Y)) &&
                    (point.X < (p2.X - p1.X) * (point.Y - p1.Y) / (p2.Y - p1.Y) + p1.X))
                {
                    crossings++;
                }
            }
            return (crossings % 2 != 0);
        }
        private static double CalculateStandardDeviation(List<double> values, double mean)
        {
            double sum = values.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sum / values.Count);
        }
    }
}
