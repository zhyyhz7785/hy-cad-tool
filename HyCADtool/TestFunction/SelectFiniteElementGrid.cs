//using System;
//using System.Collections.Generic;
//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using Autodesk.AutoCAD.Runtime;
//namespace HyCADTool
//{
//    public static partial class TestFunction
//    {
//        [CommandMethod("SelectFEGrid")]
//        public static void SelectFiniteElementGrid()
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Editor ed = doc.Editor;
//            Database db = doc.Database;
//            // 提示用户选择有限元网格中的对象
//            PromptSelectionOptions selOpts = new PromptSelectionOptions();
//            selOpts.MessageForAdding = "\n请选择有限元网格中的对象: ";
//            PromptSelectionResult selRes = ed.GetSelection(selOpts);
//            if (selRes.Status != PromptStatus.OK)
//            {
//                ed.WriteMessage("\n用户取消了选择。");
//                return;
//            }
//            SelectionSet selSet = selRes.Value;
//            using (Transaction trans = db.TransactionManager.StartTransaction())
//            {
//                // 获取包含计算配筋文字的文字对象
//                List<DBText> rebarTexts = GetRebarTextsFromSelection(selSet, trans);
//                // 获取所有选择的对象
//                List<ObjectId> allObjectIds = GetAllObjectIdsFromSelection(selSet);
//                // 获取包含这些文字对象的网格对象
//                List<ObjectId> gridObjects = GetGridObjectsFromSelection(allObjectIds, rebarTexts, trans);
//                // 输出获取到的文字对象的信息
//                ed.WriteMessage($"\n共找到 {rebarTexts.Count} 个包含计算配筋的文字对象。");
//                foreach (var text in rebarTexts)
//                {
//                    ed.WriteMessage($"\n文字内容: {text.TextString}, 位置: ({text.Position.X}, {text.Position.Y}, {text.Position.Z})");
//                }
//                // 高亮显示这些文字对象和网格对象
//                HighlightSelection(ed, rebarTexts, gridObjects);
//                trans.Commit();
//            }
//        }
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
//        private static List<ObjectId> GetGridObjectsFromSelection(List<ObjectId> allObjectIds, List<DBText> rebarTexts, Transaction trans)
//        {
//            List<ObjectId> gridObjects = new List<ObjectId>();
//            foreach (ObjectId objId in allObjectIds)
//            {
//                DBObject obj = trans.GetObject(objId, OpenMode.ForRead);
//                // 假设网格对象是由多段线（Polyline）或线（Line）组成的
//                if (obj is Polyline || obj is Line)
//                {
//                    foreach (var text in rebarTexts)
//                    {
//                        if (IsPointInsideEntity(obj, text.Position))
//                        {
//                            gridObjects.Add(objId);
//                            break;
//                        }
//                    }
//                }
//            }
//            return gridObjects;
//        }
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
//        private static bool IsPointOnLine(Line line, Point3d point)
//        {
//            // 检查点是否在直线上
//            Vector3d lineVec = line.EndPoint - line.StartPoint;
//            Vector3d pointVec = point - line.StartPoint;
//            double crossProduct = lineVec.CrossProduct(pointVec).Length;
//            return crossProduct < Tolerance.Global.EqualPoint * lineVec.Length;
//        }
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
