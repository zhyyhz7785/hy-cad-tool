//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using HyCADTool.Tools;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text.RegularExpressions;
//namespace HyCADTool.HelpClass.Section
//{
//    public class Section
//    {
//        public static Dictionary<Polyline, string> GetPolylineTextDictionary()
//        {
//            Dictionary<Polyline, string> resultDict = new Dictionary<Polyline, string>();
//            Editor ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
//            try
//            {
//                Database db = HostApplicationServices.WorkingDatabase;
//                PromptSelectionOptions selOpts = new PromptSelectionOptions();
//                selOpts.MessageForAdding = "\n请选择封闭的Polyline和DBText: ";
//                TypedValue[] filterlist = new TypedValue[]
//                {
//            new TypedValue((int)DxfCode.Operator, "<or"),
//            new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
//            new TypedValue((int)DxfCode.Start, "TEXT"),
//            new TypedValue((int)DxfCode.Operator, "or>")
//                };
//                SelectionFilter filter = new SelectionFilter(filterlist);
//                PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
//                if (selRes.Status != PromptStatus.OK)
//                    return resultDict;
//                using (Transaction tr = db.TransactionManager.StartTransaction())
//                {
//                    List<Polyline> polylines = new List<Polyline>();
//                    List<DBText> texts = new List<DBText>();
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
//                    ed.WriteMessage($"\nFound {polylines.Count} polylines and {texts.Count} texts");
//                    foreach (DBText text in texts)
//                    {
//                        // 使用边界框中心点（更准确）
//                        Point3d checkPoint = text.Bounds.HasValue
//                            ? new Point3d(
//                                (text.Bounds.Value.MinPoint.X + text.Bounds.Value.MaxPoint.X) / 2,
//                                (text.Bounds.Value.MinPoint.Y + text.Bounds.Value.MaxPoint.Y) / 2,
//                                text.Position.Z)
//                            : text.Position;
//                        foreach (Polyline pl in polylines)
//                        {
//                            if (IsPointInsidePolyline(pl, checkPoint))
//                            {
//                                string textValue = text.TextString.Trim();
//                                resultDict[pl] = textValue; // 允许重复值
//                                ed.WriteMessage($"\nAdded Polyline with text: {textValue}");
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
//        /// 判断点是否在封闭多段线内部（射线法）
//        /// </summary>
//        /// <param name="polyline">封闭的多段线</param>
//        /// <param name="point">要检查的点</param>
//        /// <returns>如果点在多段线内部返回true</returns>
//        private static bool IsPointInsidePolyline(Polyline polyline, Point3d point)
//        {
//            if (!polyline.Closed) return false;
//            int intersectCount = 0;
//            int vertexCount = polyline.NumberOfVertices;
//            for (int i = 0; i < vertexCount; i++)
//            {
//                Point3d p1 = polyline.GetPoint3dAt(i);
//                Point3d p2 = polyline.GetPoint3dAt((i + 1) % vertexCount); // 循环到第一个点
//                // 检查射线与边的交点
//                if (((p1.Y <= point.Y && point.Y < p2.Y) || (p2.Y <= point.Y && point.Y < p1.Y)) &&
//                    (point.X < (p2.X - p1.X) * (point.Y - p1.Y) / (p2.Y - p1.Y) + p1.X))
//                {
//                    intersectCount++;
//                }
//            }
//            // 奇数次相交表示点在内部
//            return (intersectCount % 2) == 1;
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
//        // 在你的Section类中使用
//        public static void ProcessSection()
//        {
//            Dictionary<Polyline, string> plTextDict = GetPolylineTextDictionary();
//            Editor ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
//            Database db = HostApplicationServices.WorkingDatabase;
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                // 获取或创建图层 "Section_01"
//                LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForWrite) as LayerTable;
//                LayerTableRecord ltr = null;
//                if (lt.Has("Section_01"))
//                {
//                    ltr = tr.GetObject(lt["Section_01"], OpenMode.ForWrite) as LayerTableRecord;
//                }
//                else
//                {
//                    ltr = new LayerTableRecord();
//                    ltr.Name = "Section_01";
//                    ltr.Color = Autodesk.AutoCAD.Colors.Color.FromColor(System.Drawing.Color.Red); // 设置为红色
//                    lt.Add(ltr);
//                    tr.AddNewlyCreatedDBObject(ltr, true);
//                }
//                // 创建一个列表存储所有相关的 DBText 对象
//                List<DBText> relatedTexts = new List<DBText>();
//                // 处理字典中的 Polyline
//                foreach (KeyValuePair<Polyline, string> kvp in plTextDict)
//                {
//                    Polyline pl = kvp.Key;
//                    string textValue = kvp.Value;
//                    // 修改 Polyline 的图层
//                    pl = tr.GetObject(pl.ObjectId, OpenMode.ForWrite) as Polyline;
//                    pl.Layer = "Section_01";
//                    // 查找对应的 DBText
//                    BlockTableRecord btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead) as BlockTableRecord;
//                    foreach (ObjectId id in btr)
//                    {
//                        if (id.ObjectClass.DxfName == "TEXT")
//                        {
//                            DBText txt = tr.GetObject(id, OpenMode.ForWrite) as DBText;
//                            if (txt.TextString.Trim() == textValue && IsPointInsidePolyline(pl, txt.Position))
//                            {
//                                txt.Layer = "Section_01";
//                                relatedTexts.Add(txt); // 记录已处理的 DBText
//                                break; // 找到匹配的文本后退出内层循环
//                            }
//                        }
//                    }
//                    ed.WriteMessage($"\n找到多段线，包含文字: {textValue}，已移至图层 Section_01");
//                }
//                tr.Commit();
//            }
//        }
//        public static void ExtrudeSections(Dictionary<Polyline, string> plTextDict)
//        {
//            Editor ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
//            Database db = HostApplicationServices.WorkingDatabase;
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                BlockTableRecord btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;
//                foreach (KeyValuePair<Polyline, string> kvp in plTextDict)
//                {
//                    Polyline pl = tr.GetObject(kvp.Key.ObjectId, OpenMode.ForRead) as Polyline;
//                    string textValue = kvp.Value;
//                    // 提取数字并处理特殊情况
//                    double extrudeDistance = ParseExtrudeDistance(textValue) * 1000;
//                    // 如果距离为 0，则跳过拉伸
//                    if (Math.Abs(extrudeDistance) < Tolerance.Global.EqualPoint)
//                    {
//                        ed.WriteMessage($"\n跳过拉伸 Polyline，文字 '{textValue}' 解析为 0");
//                        continue;
//                    }
//                    try
//                    {
//                        // 将 Polyline 转换为 Region
//                        using (DBObjectCollection curves = new DBObjectCollection { pl })
//                        {
//                            using (DBObjectCollection regions = Region.CreateFromCurves(curves))
//                            {
//                                if (regions.Count == 0)
//                                {
//                                    ed.WriteMessage($"\n无法从 Polyline 创建 Region，文字: {textValue}");
//                                    continue;
//                                }
//                                Region region = regions[0] as Region;
//                                // 创建拉伸实体
//                                Solid3d solid = new Solid3d();
//                                solid.Extrude(region, extrudeDistance, 0.0); // 拉伸距离，正向上，负向下
//                                // 添加到模型空间
//                                btr.AppendEntity(solid);
//                                tr.AddNewlyCreatedDBObject(solid, true);
//                                ed.WriteMessage($"\n成功创建三维实体，文字: {textValue}, 拉伸距离: {extrudeDistance}");
//                            }
//                        }
//                    }
//                    catch (System.Exception ex)
//                    {
//                        ed.WriteMessage($"\n拉伸失败，文字: {textValue}, 错误: {ex.Message}");
//                    }
//                }
//                tr.Commit();
//            }
//        }
//        // 示例命令
//        public static void RunExtrudeSections()
//        {
//            Dictionary<Polyline, string> plTextDict = GetPolylineTextDictionary();
//            ExtrudeSections(plTextDict);
//        }
//        /// <summary>
//        /// 使用 Polyline 和 string 生成三维拉伸实体，string 为标高，所有面向下拉伸 40mm
//        /// </summary>
//        public static void ExtrudeSectionsByElevation(Dictionary<Polyline, string> plTextDict)
//        {
//            Editor ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
//            Database db = HostApplicationServices.WorkingDatabase;
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                BlockTableRecord btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;
//                foreach (KeyValuePair<Polyline, string> kvp in plTextDict)
//                {
//                    Polyline pl = tr.GetObject(kvp.Key.ObjectId, OpenMode.ForWrite) as Polyline;
//                    string textValue = kvp.Value;
//                    // 提取标高（单位：米，转为毫米）
//                    double elevation = ParseElevation(textValue) * 1000;
//                    // 将 Polyline 的 Z 坐标设置为标高
//                    for (int i = 0; i < pl.NumberOfVertices; i++)
//                    {
//                        Point3d vertex = pl.GetPoint3dAt(i);
//                        pl.SetPointAt(i, new Point2d(vertex.X, vertex.Y)); // 保留 XY
//                        pl.Elevation = elevation; // 设置标高
//                    }
//                    try
//                    {
//                        // 将 Polyline 转换为 Region
//                        using (DBObjectCollection curves = new DBObjectCollection { pl })
//                        {
//                            using (DBObjectCollection regions = Region.CreateFromCurves(curves))
//                            {
//                                if (regions.Count == 0)
//                                {
//                                    ed.WriteMessage($"\n无法从 Polyline 创建 Region，文字: {textValue}");
//                                    continue;
//                                }
//                                Region region = regions[0] as Region;
//                                // 创建拉伸实体，向下拉伸 40mm
//                                Solid3d solid = new Solid3d();
//                                solid.Extrude(region, -40.0, 0.0); // 固定向下拉伸 40mm
//                                // 添加到模型空间
//                                btr.AppendEntity(solid);
//                                tr.AddNewlyCreatedDBObject(solid, true);
//                                ed.WriteMessage($"\n成功创建三维实体，文字: {textValue}, 标高: {elevation}mm, 向下拉伸 40mm");
//                            }
//                        }
//                    }
//                    catch (System.Exception ex)
//                    {
//                        ed.WriteMessage($"\n拉伸失败，文字: {textValue}, 错误: {ex.Message}");
//                    }
//                }
//                tr.Commit();
//            }
//        }
//        /// <summary>
//        /// 解析文字中的数字作为标高，处理特殊情况
//        /// </summary>
//        private static double ParseElevation(string text)
//        {
//            // 处理特殊情况
//            if (string.IsNullOrEmpty(text) || text.Contains("%%P0.000") || text == "0.000" || text.Contains("±"))
//                return 0.0;
//            // 使用正则表达式提取数字（包括正负号和小数）
//            string pattern = @"[-+]?[0-9]*\.?[0-9]+";
//            Match match = Regex.Match(text, pattern);
//            if (match.Success)
//            {
//                if (double.TryParse(match.Value, out double elevation))
//                {
//                    return elevation;
//                }
//            }
//            // 如果解析失败，返回 0
//            return 0.0;
//        }
//        // 示例命令
//        public static void RunExtrudeSectionsByElevation()
//        {
//            Dictionary<Polyline, string> plTextDict = GetPolylineTextDictionary();
//            ExtrudeSectionsByElevation(plTextDict);
//        }
//    }
//}