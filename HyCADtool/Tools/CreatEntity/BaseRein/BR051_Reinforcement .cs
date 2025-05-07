using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.HelpClass;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
[assembly: CommandClass(typeof(HyCADTool.BaseRein))]
namespace HyCADTool
{
    public static partial class BaseRein
    {
        /// <summary>
        /// 得到配筋区域的面积，和配筋区域的统计数据
        /// </summary>
        /// <returns></returns>
        public static Dictionary<Polyline, (int Count, double Min, double Max, double Average, double StdDev, double additionalDiameter)> ReinforcementStepA()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            // 用户选择一个实体来获取图层
            var layerName = GetLayerFromUserSelection(ed);
            if (layerName == null) return null;
            // 用户选择图形中指定图层的数据
            var selection = GetUserSelectionForReinforcement(ed, layerName);
            if (selection == null) return null;
            using (var doclock = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    var polylines = new List<Polyline>();
                    var texts = new List<DBText>();
                    foreach (var id in selection.GetObjectIds())
                    {
                        var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (entity != null)
                        {
                            if (entity.Layer == layerName && entity is Polyline polyline)
                            {
                                if (IsRectangle(polyline))
                                {
                                    // 将Polyline以写模式打开
                                    polyline = tr.GetObject(polyline.ObjectId, OpenMode.ForWrite) as Polyline;
                                    if (polyline != null)
                                    {
                                        polyline.ResetPolyVertex();
                                        polylines.Add(polyline);
                                    }
                                }
                            }
                            else if (entity is DBText)
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
                    var statsMap = new Dictionary<Polyline, (int Count, double Min, double Max, double Average, double StdDev, double additionalDiameter)>();
                    foreach (var kvp in polylineTextMap)
                    {
                        var values = kvp.Value;
                        int count = values.Count;
                        double min = values.Min();
                        double max = values.Max();
                        double average = values.Average();
                        double stdDev = CalculateStandardDeviation(values, average);
                        double additionalDiameter = MinAdditionalDiameter;
                        statsMap[kvp.Key] = (count, min, max, average, stdDev, additionalDiameter);
                    }
                    tr.Commit();
                    return statsMap;
                }
            }
        }
        private static string GetLayerFromUserSelection(Editor ed)
        {
            var peo = new PromptEntityOptions("请选择一个实体来确定图层: ");
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
            {
                ed.WriteMessage("未选择任何实体。\n");
                return null;
            }
            using (var tr = ed.Document.Database.TransactionManager.StartTransaction())
            {
                var entity = (Entity)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                var layerName = entity.Layer;
                tr.Commit();
                return layerName;
            }
        }
        private static SelectionSet GetUserSelectionForReinforcement(Editor ed, string layerName)
        {
            var pso = new PromptSelectionOptions
            {
                MessageForAdding = $"请选择'{layerName}'图层的Polyline和所有需要的文字:"
            };
            var filter = AcTv.Or(AcTv.Polyline, AcTv.DBText, AcTv.DBText).Getfilter();
            var psr = ed.GetSelection(pso, filter);
            if (psr.Status == PromptStatus.OK)
            {
                return psr.Value;
            }
            ed.WriteMessage("未选择任何对象。\n");
            return null;
        }
        private static bool IsRectangle(Polyline polyline)
        {
            if (polyline.NumberOfVertices != 4)
                return false;
            var points = new List<Point2d>();
            for (int i = 0; i < 4; i++)
            {
                points.Add(polyline.GetPoint2dAt(i));
            }
            var vector1 = points[1] - points[0];
            var vector2 = points[2] - points[1];
            var vector3 = points[3] - points[2];
            var vector4 = points[0] - points[3];
            return vector1.IsPerpendicularTo(vector2) && vector2.IsPerpendicularTo(vector3) &&
                   vector3.IsPerpendicularTo(vector4) && vector4.IsPerpendicularTo(vector1) &&
                   vector1.Length == vector3.Length && vector2.Length == vector4.Length;
        }
        private static double CalculateStandardDeviation(List<double> values, double mean)
        {
            double sum = values.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sum / values.Count);
        }
    }
}
