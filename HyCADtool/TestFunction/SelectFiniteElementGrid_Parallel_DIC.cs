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
//        public static Dictionary<DBText, ObjectId> SelectFiniteElementGrid()
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
//            // 如果用户取消选择，输出消息并返回空字典
//            if (selRes.Status != PromptStatus.OK)
//            {
//                ed.WriteMessage("\n用户取消了选择。");
//                return new Dictionary<DBText, ObjectId>();
//            }
//            // 获取选择集
//            SelectionSet selSet = selRes.Value;
//            // 使用事务获取所有对象并存储在字典中
//            Dictionary<ObjectId, DBObject> objectDict = new Dictionary<ObjectId, DBObject>();
//            using (Transaction trans = db.TransactionManager.StartTransaction())
//            {
//                foreach (SelectedObject selObj in selSet)
//                {
//                    if (selObj != null)
//                    {
//                        DBObject obj = trans.GetObject(selObj.ObjectId, OpenMode.ForRead);
//                        objectDict[selObj.ObjectId] = obj;
//                    }
//                }
//                trans.Commit();
//            }
//            // 提取包含数值的文字对象
//            List<DBText> rebarTexts = objectDict.Values.OfType<DBText>().Where(textObj => double.TryParse(textObj.TextString, out _)).ToList();
//            // 并行处理找到每个文字对象对应的网格对象ID
//            ConcurrentDictionary<DBText, ObjectId> result = new ConcurrentDictionary<DBText, ObjectId>();
//            Parallel.ForEach(rebarTexts, text =>
//            {
//                foreach (var kvp in objectDict)
//                {
//                    if (kvp.Value is Polyline polyline && IsPointInsidePolyline(polyline, text.AlignmentPoint))
//                    {
//                        result[text] = kvp.Key;
//                        break;
//                    }
//                    else if (kvp.Value is Line line && IsPointOnLine(line, text.AlignmentPoint))
//                    {
//                        result[text] = kvp.Key;
//                        break;
//                    }
//                }
//            });
//            // 转换为普通字典
//            Dictionary<DBText, ObjectId> resultDict = result.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
//            // 高亮显示结果
//            HighlightSelection(ed, resultDict);
//            return resultDict;
//        }
//        // 检查点是否在多段线内部
//        private static bool IsPointInsidePolyline(Polyline polyline, Point3d testPoint)
//        {
//            int n = polyline.NumberOfVertices;
//            bool inside = false;
//            Point2d testPoint2d = new Point2d(testPoint.X, testPoint.Y);
//            for (int i = 0, j = n - 1; i < n; j = i++)
//            {
//                Point2d pi = polyline.GetPoint2dAt(i);
//                Point2d pj = polyline.GetPoint2dAt(j);
//                if (((pi.Y > testPoint2d.Y) != (pj.Y > testPoint2d.Y)) &&
//                     (testPoint2d.X < (pj.X - pi.X) * (testPoint2d.Y - pi.Y) / (pj.Y - pi.Y) + pi.X))
//                {
//                    inside = !inside;
//                }
//            }
//            return inside;
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
//        private static void HighlightSelection(Editor ed, Dictionary<DBText, ObjectId> result)
//        {
//            List<ObjectId> ids = new List<ObjectId>();
//            foreach (var kvp in result)
//            {
//                ids.Add(kvp.Key.ObjectId);
//                ids.Add(kvp.Value);
//            }
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
