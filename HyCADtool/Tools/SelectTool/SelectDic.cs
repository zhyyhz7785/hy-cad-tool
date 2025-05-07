using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Log;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace HyCADTool.Tools
{
    public static partial class EtGpt
    {
        public static Dictionary<ObjectId, DBObject> SelectToDic(SelectionFilter filter, string message)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            Dictionary<ObjectId, DBObject> objectDict = new Dictionary<ObjectId, DBObject>();
            PromptSelectionOptions selOpts = new PromptSelectionOptions
            {
                MessageForAdding = message
            };
            PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
            if (selRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n用户取消了选择。");
                return new Dictionary<ObjectId, DBObject>();
            }
            SelectionSet selSet = selRes.Value;
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject selObj in selSet)
                {
                    if (selObj != null)
                    {
                        DBObject obj = trans.GetObject(selObj.ObjectId, OpenMode.ForRead);
                        objectDict[selObj.ObjectId] = obj;
                    }
                }
            }
            return objectDict;
        }
        public static Dictionary<DBText, ObjectId> GetFiniteElementResult(Dictionary<ObjectId, DBObject> objectDict)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            SimpleLogger.StartTiming("rebarTexts");
            List<DBText> rebarTexts = objectDict.Values
                .OfType<DBText>()
                .Where(textObj => double.TryParse(textObj.TextString, out _))
                .ToList();
            SimpleLogger.StopTiming("rebarTexts");
            SimpleLogger.StartTiming("FiniteElementGrid");
            List<Polyline> finiteElementGrid = objectDict.Values
                .OfType<Polyline>()
                .Where(x => x.Layer == "板元")
                .ToList();
            SimpleLogger.StopTiming("FiniteElementGrid");
            // 创建映射关系，使用并行处理来提升效率
            SimpleLogger.StartTiming("ParallelMapping");
            ConcurrentDictionary<DBText, ObjectId> result = new ConcurrentDictionary<DBText, ObjectId>();
            Parallel.ForEach(rebarTexts, text =>
            {
                foreach (var poly in finiteElementGrid)
                {
                    if (IsPointInsidePolyline(poly, text.AlignmentPoint))
                    {
                        result[text] = poly.ObjectId;
                        break;
                    }
                }
            });
            SimpleLogger.StopTiming("ParallelMapping");
            // 转换为普通字典并设置高亮
            SimpleLogger.StartTiming("SetImpliedSelection");
            Dictionary<DBText, ObjectId> resultDict = result.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            ed.SetImpliedSelection(resultDict.Values.ToArray());
            SimpleLogger.StopTiming("SetImpliedSelection");
            return resultDict;
        }
        public static void GetNoUseFiniteElementResult(Dictionary<ObjectId, DBObject> objectDict)
        {
            SimpleLogger.StartTiming("GetNoUseFiniteElementResult");
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            SimpleLogger.StartTiming("rebarTexts");
            List<DBText> rebarTexts = objectDict.Values
                .OfType<DBText>()
                .Where(textObj => double.TryParse(textObj.TextString, out _))
                .ToList();
            SimpleLogger.StopTiming("rebarTexts");
            SimpleLogger.StartTiming("FiniteElementGrid");
            List<Polyline> finiteElementGrid = objectDict.Values
                .OfType<Polyline>()
                .Where(x => x.Layer == "板元")
                .ToList();
            SimpleLogger.StopTiming("FiniteElementGrid");
            // 使用并行处理并减少不必要的计算
            SimpleLogger.StartTiming("ParallelFiltering");
            ConcurrentBag<ObjectId> polylinesWithoutNumbers = new ConcurrentBag<ObjectId>();
            Parallel.ForEach(finiteElementGrid, polyline =>
             {
                 bool containsNumber = rebarTexts.Any(text => IsPointInsidePolyline(polyline, text.AlignmentPoint));
                 if (!containsNumber)
                 {
                     polylinesWithoutNumbers.Add(polyline.ObjectId);
                 }
             });
            SimpleLogger.StopTiming("ParallelFiltering");
            SimpleLogger.StartTiming("SetImpliedSelection");
            ed.SetImpliedSelection(polylinesWithoutNumbers.ToArray());
            SimpleLogger.StopTiming("SetImpliedSelection");
        }
    }
}
