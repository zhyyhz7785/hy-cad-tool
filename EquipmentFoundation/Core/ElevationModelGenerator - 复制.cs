//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using EquipmentFoundation.Models;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//namespace EquipmentFoundation
//{
//    public partial class ElevationModelGenerator
//    {
//        private GeometryInput SelectGeometryInputFromAutoCAD()
//        {
//            var input = new GeometryInput();
//            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
//            var db = HostApplicationServices.WorkingDatabase;
//            string warningLayerName = "00ElevationWarnings";
//            string textLayerName = "00_hy_3公共_标注4_标高";
//            using (var tr = db.TransactionManager.StartTransaction())
//            {
//                var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
//                if (!layerTable.Has("dcelOuter") || !layerTable.Has("dcelInter"))
//                {
//                    ed.WriteMessage("\n错误: 图层 'dcelOuter' 或 'dcelInter' 不存在，请创建这些图层后重试。\n");
//                    tr.Commit();
//                    return input;
//                }
//                if (!layerTable.Has(warningLayerName))
//                {
//                    layerTable.UpgradeOpen();
//                    var newLayer = new LayerTableRecord
//                    {
//                        Name = warningLayerName,
//                        Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 1)
//                    };
//                    layerTable.Add(newLayer);
//                    tr.AddNewlyCreatedDBObject(newLayer, true);
//                }
//                var selOpts = new PromptSelectionOptions { MessageForAdding = "\n请选择封闭的 Polyline、标高文本和螺栓: " };
//                var filter = new SelectionFilter(new TypedValue[]
//                {
//                    new TypedValue((int)DxfCode.Operator, "<or"),
//                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
//                    new TypedValue((int)DxfCode.Start, "TEXT"),
//                    new TypedValue((int)DxfCode.Start, "CIRCLE"),
//                    new TypedValue((int)DxfCode.Operator, "or>")
//                });
//                PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
//                if (selRes.Status != PromptStatus.OK)
//                {
//                    ed.WriteMessage("\n错误: 未选择任何对象，请选择封闭的 Polyline、标高文本和螺栓后重试。\n");
//                    tr.Commit();
//                    return input;
//                }
//                ed.WriteMessage($"\n找到 {selRes.Value.Count} 个对象。\n");
//                var outerPolylines = new List<Polyline>();
//                var innerPolylines = new List<Polyline>();
//                var texts = new List<DBText>();
//                var circles = new List<Circle>();
//                // 分类选择的对象
//                foreach (ObjectId id in selRes.Value.GetObjectIds())
//                {
//                    var obj = tr.GetObject(id, OpenMode.ForRead);
//                    if (obj is Polyline pl && pl.Closed)
//                    {
//                        if (pl.Layer == "dcelOuter")
//                            outerPolylines.Add(pl);
//                        else if (pl.Layer == "dcelInter")
//                            innerPolylines.Add(pl);
//                    }
//                    else if (obj is DBText txt && txt.Layer == textLayerName)
//                    {
//                        texts.Add(txt);
//                    }
//                    else if (obj is Circle circle && circle.Layer.StartsWith("00_Hy_螺栓"))
//                    {
//                        circles.Add(circle);
//                    }
//                }
//                // 赋值到 GeometryInput
//                input.OuterContours = outerPolylines;
//                input.InnerPolygons = innerPolylines;
//                input.Bolts = circles;
//                if (input.OuterContours.Count == 0)
//                {
//                    ed.WriteMessage("\n错误: 未在 'dcelOuter' 图层中找到封闭的 Polyline，请检查图纸并添加外轮廓。\n");
//                    tr.Commit();
//                    return input;
//                }
//                if (input.InnerPolygons.Count == 0)
//                {
//                    ed.WriteMessage("\n错误: 未在 'dcelInter' 图层中找到封闭的 Polyline，请检查图纸并添加内多边形。\n");
//                    tr.Commit();
//                    return input;
//                }
//                // 处理标高
//                var polygonElevations = new Dictionary<Polyline, List<double>>();
//                foreach (var text in texts)
//                {
//                    var checkPoint = text.Bounds.HasValue
//                        ? new Point3d((text.Bounds.Value.MinPoint.X + text.Bounds.Value.MaxPoint.X) / 2,
//                                      (text.Bounds.Value.MinPoint.Y + text.Bounds.Value.MaxPoint.Y) / 2, text.Position.Z)
//                        : text.Position;
//                    foreach (var polyline in innerPolylines)
//                    {
//                        if (IsPointInsidePolygon(checkPoint, polyline))
//                        {
//                            double elevation = ParseExtrudeDistance(text.TextString.Trim());
//                            if (!polygonElevations.ContainsKey(polyline))
//                                polygonElevations[polyline] = new List<double>();
//                            polygonElevations[polyline].Add(elevation);
//                            break;
//                        }
//                    }
//                }
//                var btr = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
//                bool hasErrors = false;
//                foreach (var polyline in innerPolylines)
//                {
//                    if (!IsValidPolyline(polyline))
//                    {
//                        hasErrors = true;
//                        AddWarningEntity(btr, tr, polyline, warningLayerName, "错误: 自交多边形，请修正多边形几何。");
//                        continue;
//                    }
//                    if (!polygonElevations.ContainsKey(polyline))
//                    {
//                        hasErrors = true;
//                        AddWarningEntity(btr, tr, polyline, warningLayerName, "错误: 未找到标高，请为该多边形添加标高文本。");
//                    }
//                    else if (polygonElevations[polyline].Distinct().Count() > 1)
//                    {
//                        hasErrors = true;
//                        AddWarningEntity(btr, tr, polyline, warningLayerName, "错误: 标高不一致，请确保该多边形只有一个标高值。");
//                    }
//                    else
//                    {
//                        input.Elevations[polyline] = polygonElevations[polyline].First();
//                    }
//                }
//                if (hasErrors)
//                {
//                    ed.WriteMessage("\n命令中止: 图纸中存在错误，请查看 '00ElevationWarnings' 图层并修正问题后重试。\n");
//                    tr.Commit();
//                    return input;
//                }
//                ed.WriteMessage($"\n选择统计: OuterContours={input.OuterContours.Count}, InnerPolygons={input.InnerPolygons.Count}, Elevations={input.Elevations.Count}, Bolts={input.Bolts.Count}\n");
//                tr.Commit();
//            }
//            return input;
//        }
//        // 辅助方法（从原代码迁移）
//        private bool IsValidPolyline(Polyline polyline)
//        {
//            // 检查多边形是否自交的简单实现
//            for (int i = 0; i < polyline.NumberOfVertices - 1; i++)
//            {
//                for (int j = i + 2; j < polyline.NumberOfVertices - 1; j++)
//                {
//                    var seg1Start = polyline.GetPoint3dAt(i);
//                    var seg1End = polyline.GetPoint3dAt(i + 1);
//                    var seg2Start = polyline.GetPoint3dAt(j);
//                    var seg2End = polyline.GetPoint3dAt((j + 1) % polyline.NumberOfVertices);
//                    if (AreLinesIntersecting(seg1Start, seg1End, seg2Start, seg2End))
//                        return false;
//                }
//            }
//            return true;
//        }
//        private bool AreLinesIntersecting(Point3d p1, Point3d p2, Point3d q1, Point3d q2)
//        {
//            // 简单线段相交检测
//            double denom = (q2.Y - q1.Y) * (p2.X - p1.X) - (q2.X - q1.X) * (p2.Y - p1.Y);
//            if (Math.Abs(denom) < Tolerance.Global.EqualPoint) return false;
//            double ua = ((q2.X - q1.X) * (p1.Y - q1.Y) - (q2.Y - q1.Y) * (p1.X - q1.X)) / denom;
//            double ub = ((p2.X - p1.X) * (p1.Y - q1.Y) - (p2.Y - p1.Y) * (p1.X - q1.X)) / denom;
//            return ua >= 0 && ua <= 1 && ub >= 0 && ub <= 1;
//        }
//        private void AddWarningEntity(BlockTableRecord btr, Transaction tr, Polyline polyline, string layerName, string message)
//        {
//            var centroid = GetPolylineCentroid(polyline);
//            var text = new DBText
//            {
//                Position = centroid,
//                Height = 250,
//                TextString = message,
//                Layer = layerName
//            };
//            btr.AppendEntity(text);
//            tr.AddNewlyCreatedDBObject(text, true);
//        }
//        private Point3d GetPolylineCentroid(Polyline polyline)
//        {
//            double xSum = 0, ySum = 0;
//            int n = polyline.NumberOfVertices;
//            for (int i = 0; i < n; i++)
//            {
//                Point3d pt = polyline.GetPoint3dAt(i);
//                xSum += pt.X;
//                ySum += pt.Y;
//            }
//            return new Point3d(xSum / n, ySum / n, 0);
//        }
//        private double ParseExtrudeDistance(string text)
//        {
//            if (string.IsNullOrEmpty(text) || text.Contains("%%P0.000") || text == "0.000" || text.Contains("±"))
//                return 0.0;
//            string pattern = @"[-+]?[0-9]*\.?[0-9]+";
//            var match = System.Text.RegularExpressions.Regex.Match(text, pattern);
//            return match.Success && double.TryParse(match.Value, out double distance) ? distance * 1000 : 0.0;
//        }
//        private bool IsPointInsidePolygon(Point3d point, Polyline polyline)
//        {
//            int intersections = 0;
//            int n = polyline.NumberOfVertices;
//            for (int i = 0; i < n; i++)
//            {
//                Point3d p1 = polyline.GetPoint3dAt(i);
//                Point3d p2 = polyline.GetPoint3dAt((i + 1) % n);
//                if ((p1.Y <= point.Y && p2.Y > point.Y) || (p2.Y <= point.Y && p1.Y > point.Y))
//                {
//                    double xIntersection = p1.X + (point.Y - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y);
//                    if (xIntersection > point.X)
//                        intersections++;
//                }
//            }
//            return (intersections % 2) == 1;
//        }
//    }
//}