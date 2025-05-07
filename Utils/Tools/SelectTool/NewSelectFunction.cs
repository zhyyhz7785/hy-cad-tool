//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.Runtime;
//using Autodesk.AutoCAD.Colors;
//using System.Windows.Controls;
//using System.Collections.Generic;
//using System;
//using System.Threading.Tasks;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using System.Collections.Concurrent;
//using System.Linq;
//namespace CadUtils
//{
//    public static partial class EtGpt
//    {
//        public static void Select(string message)
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Editor ed = doc.Editor;
//            Database db = doc.Database;
//            PromptSelectionResult selRes = PromptUserSelection(ed,message);
//            if (selRes.Status != PromptStatus.OK)
//            {
//                ed.WriteMessage("\n用户取消了选择。");
//                return;
//            }
//            SelectionSet selSet = selRes.Value;
//            var objectDict = GetObjectsFromSelectionSet(db, selSet);
//            var rebarTexts = ExtractRebarTexts(objectDict);
//            var result = FindCorrespondingGridObjects(objectDict, rebarTexts);
//            RemoveUnselectedGridObjects(db, objectDict, result);
//            HighlightSelection(ed, result);
//        }
//        private static PromptSelectionResult PromptUserSelection(this Editor ed,string Message)
//        {
//            PromptSelectionOptions selOpts = new PromptSelectionOptions
//            {
//                MessageForAdding =$"\n+{Message}: "
//            };
//            return ed.GetSelection(selOpts);
//        }
//        private static Dictionary<ObjectId, DBObject> GetObjectsFromSelectionSet(Database db, SelectionSet selSet)
//        {
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
//            return objectDict;
//        }
//        private static List<DBText> ExtractRebarTexts(Dictionary<ObjectId, DBObject> objectDict)
//        {
//            return objectDict.Values.OfType<DBText>()
//                .Where(textObj => double.TryParse(textObj.TextString, out _)).ToList();
//        }
//        private static ConcurrentDictionary<DBText, ObjectId> FindCorrespondingGridObjects(
//            Dictionary<ObjectId, DBObject> objectDict, List<DBText> rebarTexts)
//        {
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
//            return result;
//        }
//        private static void RemoveUnselectedGridObjects(
//            Database db, Dictionary<ObjectId, DBObject> objectDict, ConcurrentDictionary<DBText, ObjectId> result)
//        {
//            using (Transaction trans = db.TransactionManager.StartTransaction())
//            {
//                foreach (var kvp in objectDict)
//                {
//                    if (!result.Values.Contains(kvp.Key) && (kvp.Value is Polyline || kvp.Value is Line))
//                    {
//                        if ((kvp.Value as Entity).Layer == "板元")
//                        {
//                            kvp.Value.UpgradeOpen();
//                            kvp.Value.Erase();
//                        }
//                    }
//                }
//                trans.Commit();
//            }
//        }
//        private static void HighlightSelection(Editor ed, ConcurrentDictionary<DBText, ObjectId> result)
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
//        private static bool IsPointOnLine(Line line, Point3d point)
//        {
//            Vector3d lineVec = line.EndPoint - line.StartPoint;
//            Vector3d pointVec = point - line.StartPoint;
//            double crossProduct = lineVec.CrossProduct(pointVec).Length;
//            return crossProduct < Tolerance.Global.EqualPoint * lineVec.Length;
//        }
//    }
//}
