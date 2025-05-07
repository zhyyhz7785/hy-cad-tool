//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.DatabaseServices.Filters;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using HyCADTool.Tools;
//using NetTopologySuite.Geometries;
//using NetTopologySuite.Operation.Distance;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Net;
//using HyCADTool;
//using HyCADTool.Services;
//using System.Text.RegularExpressions;
//namespace HyCADTool.HelpClass.Section
//{
//    public class Section1
//    {
//        public static Dictionary<Polygon, double > GetPolygonTextDictionary()
//        {
//            Dictionary<Polygon, double > resultDict = new Dictionary<Polygon, double>();
//            Editor ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
//            try
//            {
//                Database db = HostApplicationServices.WorkingDatabase;
//                PromptSelectionOptions selOpts = new PromptSelectionOptions();
//                selOpts.MessageForAdding = "\n请选择封闭的Polyline和DBText: ";
//                TypedValue[] filterlist = new TypedValue[]
//                {
//                new TypedValue((int)DxfCode.Operator, "<or"),
//                new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
//                 new TypedValue((int)DxfCode.Start, "TEXT"),
//                 new TypedValue((int)DxfCode.Operator, "or>")
//                };
//                SelectionFilter filter = new SelectionFilter(filterlist);
//                PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
//                if (selRes.Status != PromptStatus.OK)
//                    return resultDict;
//                using (Transaction tr = db.TransactionManager.StartTransaction())
//                {
//                    List<Polyline> polylines = new List<Polyline>();
//                    List<DBText> texts = new List<DBText>();
//                    // 收集 AutoCAD 对象
//                    foreach (ObjectId id in selRes.Value.GetObjectIds())
//                    {
//                        if (id.ObjectClass.DxfName == "LWPOLYLINE")
//                        {
//                            Polyline pl = tr.GetObject(id, OpenMode.ForRead) as Polyline;
//                            if (pl != null && pl.Closed)
//                                polylines.Add(pl);
//                        }
//                        else if (id.ObjectClass.DxfName == "TEXT")
//                        {
//                            DBText txt = tr.GetObject(id, OpenMode.ForRead) as DBText;
//                            if (txt != null)
//                                texts.Add(txt);
//                        }
//                    }
//                    // 转换为 NTS Polygon
//                    List<Polygon> polygons = GeometryConverter.ToNetTopologySuite(polylines);
//                    ed.WriteMessage($"\nFound {polygons.Count} polygons and {texts.Count} texts");
//                    foreach (DBText text in texts)
//                    {
//                        // 使用边界框中心点（更准确）
//                        Point3d checkPoint = text.Bounds.HasValue
//                            ? new Point3d(
//                                (text.Bounds.Value.MinPoint.X + text.Bounds.Value.MaxPoint.X) / 2,
//                                (text.Bounds.Value.MinPoint.Y + text.Bounds.Value.MaxPoint.Y) / 2,
//                                text.Position.Z)
//                            : text.Position;
//                        // 转换为 NTS Point
//                        Point ntsPoint = GeometryConverter.ToNetTopologySuite(checkPoint);
//                        foreach (Polygon polygon in polygons)
//                        {
//                            // 使用 NTS 的 Contains 方法检查点是否在多边形内
//                            if (polygon.Contains(ntsPoint))
//                            {
//                                string textValue = text.TextString.Trim();
//                                var doubletext = ParseExtrudeDistance(textValue);
//                                resultDict[polygon] = doubletext; // 允许重复值
//                                ed.WriteMessage($"\nAdded Polygon with text: {textValue}");
//                                break;
//                            }
//                        }
//                    }
//                    tr.Commit();
//                }
//            }
//            catch (System.Exception ex)
//            {
//                Autodesk.AutoCAD.ApplicationServices.Application.ShowAlertDialog("错误: " + ex.Message);
//            }
//            return resultDict;
//        }
//        /// <summary>
//        /// 解析文字中的数字并处理特殊情况
//        /// </summary>
//        private static double ParseExtrudeDistance(string text)
//        {
//            // 处理特殊情况
//            if (string.IsNullOrEmpty(text) || text.Contains("%%P0.000") || text == "0.000" || text.Contains("±"))
//                return 0.0;
//            // 使用正则表达式提取数字（包括正负号和小数）
//            string pattern = @"[-+]?[0-9]*\.?[0-9]+";
//            Match match = Regex.Match(text, pattern);
//            if (match.Success)
//            {
//                if (double.TryParse(match.Value, out double distance))
//                {
//                    return distance;
//                }
//            }
//            // 如果解析失败，返回 0
//            return 0.0;
//        }
//    }
//}