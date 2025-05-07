//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using Autodesk.AutoCAD.Runtime;
//namespace HyCADTool
//{
//    public static partial class TestFunction
//    {
//        // CommandMethod 特性表明这是一个 AutoCAD 命令
//        [CommandMethod("SelectFEGrid")]
//        public static void SelectFiniteElementGrid()
//        {
//            // 获取当前文档、编辑器和数据库
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Editor ed = doc.Editor;
//            Database db = doc.Database;
//            // 提示用户选择有限元网格中的对象
//            PromptSelectionOptions selOpts = new PromptSelectionOptions
//            {
//                MessageForAdding = "\n请选择有限元网格中的对象: "
//            };
//            PromptSelectionResult selRes = ed.GetSelection(selOpts);
//            // 如果用户取消选择，输出消息并返回
//            if (selRes.Status != PromptStatus.OK)
//            {
//                ed.WriteMessage("\n用户取消了选择。");
//                return;
//            }
//            // 获取选择集
//            SelectionSet selSet = selRes.Value;
//            using (Transaction trans = db.TransactionManager.StartTransaction())
//            {
//                // 获取包含计算配筋文字的文字对象
//                List<DBText> rebarTexts = GetRebarTextsFromSelection(selSet, trans);
//                // 获取所有选择的对象ID
//                List<ObjectId> allObjectIds = GetAllObjectIdsFromSelection(selSet);
//                // 获取包含这些文字对象的网格对象
//                List<ObjectId> gridObjects = GetGridObjectsFromSelection(allObjectIds, rebarTexts, trans);
//                // 高亮显示这些文字对象和网格对象
//                HighlightSelection(ed, rebarTexts, gridObjects);
//                // 提交事务
//                trans.Commit();
//            }
//        }
//        // 获取选择集中包含数值文字的文字对象
//        private static List<DBText> GetRebarTextsFromSelection(SelectionSet selSet, Transaction trans)
//        {
//            List<DBText> rebarTexts = new List<DBText>();
//            foreach (SelectedObject selObj in selSet)
//            {
//                if (selObj != null)
//                {
//                    DBObject obj = trans.GetObject(selObj.ObjectId, OpenMode.ForRead);
//                    if (obj is DBText textObj && double.TryParse(textObj.TextString, out _))
//                    {
//                        rebarTexts.Add(textObj);
//                    }
//                }
//            }
//            return rebarTexts;
//        }
//        // 获取选择集中所有对象的ID
//        private static List<ObjectId> GetAllObjectIdsFromSelection(SelectionSet selSet)
//        {
//            List<ObjectId> objectIds = new List<ObjectId>();
//            foreach (SelectedObject selObj in selSet)
//            {
//                if (selObj != null)
//                {
//                    objectIds.Add(selObj.ObjectId);
//                }
//            }
//            return objectIds;
//        }
//        // 获取包含文字对象的网格对象ID列表
//        private static List<ObjectId> GetGridObjectsFromSelection(List<ObjectId> allObjectIds, List<DBText> rebarTexts, Transaction trans)
//        {
//            List<ObjectId> gridObjects = new List<ObjectId>();
//            // 使用并行处理来提高效率
//            Parallel.ForEach(allObjectIds, objId =>
//            {
//                DBObject obj = trans.GetObject(objId, OpenMode.ForRead);
//                // 检查对象是否为多段线或线
//                if (obj is Polyline || obj is Line)
//                {
//                    foreach (var text in rebarTexts)
//                    {
//                        // 如果文字对象位于网格对象内部，则添加网格对象ID到列表
//                        if (IsPointInsideEntity(obj, text.AlignmentPoint))
//                        {
//                            lock (gridObjects)
//                            {
//                                gridObjects.Add(objId);
//                            }
//                            break;
//                        }
//                    }
//                }
//            });
//            return gridObjects;
//        }
//        // 检查点是否在实体内部
//        private static bool IsPointInsideEntity(DBObject entity, Point3d point)
//        {
//            if (entity is Polyline polyline)
//            {
//                return IsPointInsidePolyline(polyline, new Point2d(point.X, point.Y));
//            }
//            else if (entity is Line line)
//            {
//                return IsPointOnLine(line, point);
//            }
//            return false;
//        }
//        // 检查点是否在多段线内部
//        private static bool IsPointInsidePolyline(Polyline polyline, Point2d testPoint)
//        {
//            int n = polyline.NumberOfVertices;
//            bool inside = false;
//            for (int i = 0, j = n - 1; i < n; j = i++)
//            {
//                Point2d pi = polyline.GetPoint2dAt(i);
//                Point2d pj = polyline.GetPoint2dAt(j);
//                if (((pi.Y > testPoint.Y) != (pj.Y > testPoint.Y)) &&
//                     (testPoint.X < (pj.X - pi.X) * (testPoint.Y - pi.Y) / (pj.Y - pi.Y) + pi.X))
//                {
//                    inside = !inside;
//                }
//            }
//            return inside;
//        }
//        private static bool IsPointInsideBounds(Point2d point, Point2d minPoint, Point2d maxPoint)
//        {
//            return point.X >= minPoint.X && point.X <= maxPoint.X &&
//                   point.Y >= minPoint.Y && point.Y <= maxPoint.Y;
//        }
//        // 检查点是否在线上
//        private static bool IsPointOnLine(Line line, Point3d point)
//        {
//            // 检查点是否在直线上
//            Vector3d lineVec = line.EndPoint - line.StartPoint;
//            Vector3d pointVec = point - line.StartPoint;
//            double crossProduct = lineVec.CrossProduct(pointVec).Length;
//            return crossProduct < Tolerance.Global.EqualPoint * lineVec.Length;
//        }
//        // 高亮显示选择的文字对象和网格对象
//        private static void HighlightSelection(Editor ed, List<DBText> rebarTexts, List<ObjectId> gridObjects)
//        {
//            List<ObjectId> ids = new List<ObjectId>();
//            foreach (var text in rebarTexts)
//            {
//                ids.Add(text.ObjectId);
//            }
//            ids.AddRange(gridObjects);
//            if (ids.Count > 0)
//            {
//                ed.SetImpliedSelection(ids.ToArray());
//                ed.WriteMessage($"\n高亮显示 {ids.Count} 个对象。");
//            }
//            else
//            {
//                ed.WriteMessage("\n未找到需要高亮显示的对象。");
//            }
//        }
//    }
//}
