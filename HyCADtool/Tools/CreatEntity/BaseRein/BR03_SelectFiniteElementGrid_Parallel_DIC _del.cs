using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Log;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
[assembly: CommandClass(typeof(HyCADTool.BaseRein))]
namespace HyCADTool
{
    public static partial class BaseRein
    {
        // CommandMethod 特性表明这是一个 AutoCAD 命令        
        public static Dictionary<DBText, ObjectId> SelectFiniteElementGrid()
        {
            // 创建一个 Stopwatch 来测量时间
            var stopwatch = new System.Diagnostics.Stopwatch();
            // 获取当前文档、编辑器和数据库
            stopwatch.Start();
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            stopwatch.Stop();
            SimpleLogger.Log($"获取当前文档、编辑器和数据库耗时: {stopwatch.ElapsedMilliseconds} ms");
            // 提示用户选择有限元网格中的对象
            stopwatch.Restart();
            PromptSelectionOptions selOpts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择有限元网格中的对象: "
            };
            PromptSelectionResult selRes = ed.GetSelection(selOpts);
            stopwatch.Stop();
            SimpleLogger.Log($"提示用户选择有限元网格中的对象耗时: {stopwatch.ElapsedMilliseconds} ms");
            // 如果用户取消选择，输出消息并返回空字典
            if (selRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n用户取消了选择。");
                return new Dictionary<DBText, ObjectId>();
            }
            // 获取选择集
            stopwatch.Restart();
            SelectionSet selSet = selRes.Value;
            stopwatch.Stop();
            SimpleLogger.Log($"获取选择集耗时: {stopwatch.ElapsedMilliseconds} ms");
            // 使用事务获取所有对象并存储在字典中
            stopwatch.Restart();
            Dictionary<ObjectId, DBObject> objectDict = new Dictionary<ObjectId, DBObject>();
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
                stopwatch.Stop();
                SimpleLogger.Log($"事务获取所有对象并存储在字典中耗时: {stopwatch.ElapsedMilliseconds} ms");
                // 提取包含数值的文字对象
                stopwatch.Restart();
                PillarPiers = objectDict.Values
                    .OfType<Polyline>()
                    .Where(x => x.Layer == "柱" && x.Linetype == "ByLayer")
                    .ToList();
                List<DBText> rebarTexts = objectDict.Values.OfType<DBText>().Where(textObj => double.TryParse(textObj.TextString, out _)).ToList();
                stopwatch.Stop();
                SimpleLogger.Log($"提取包含数值的文字对象耗时: {stopwatch.ElapsedMilliseconds} ms");
                // 并行处理找到每个文字对象对应的网格对象ID
                stopwatch.Restart();
                ConcurrentDictionary<DBText, ObjectId> result = new ConcurrentDictionary<DBText, ObjectId>();
                Parallel.ForEach(rebarTexts, text =>
                {
                    foreach (var kvp in objectDict)
                    {
                        if (kvp.Value is Polyline polyline && IsPointInsidePolyline(polyline, text.AlignmentPoint))
                        {
                            result[text] = kvp.Key;
                            break;
                        }
                        //else if (kvp.Value is Line line && IsPointOnLine(line, text.AlignmentPoint))
                        //{
                        //    result[text] = kvp.Key;
                        //    break;
                        //}
                    }
                });
                stopwatch.Stop();
                SimpleLogger.Log($"并行处理找到每个文字对象对应的网格对象ID耗时: {stopwatch.ElapsedMilliseconds} ms");
                // 删除未被选中的有限元网格和板元图层上的对象
                stopwatch.Restart();
                using (DocumentLock docLock = doc.LockDocument())
                {
                    // 预先计算一次已包含的 ObjectId 列表
                    var resultValues = new HashSet<ObjectId>(result.Values);
                    foreach (var kvp in objectDict)
                    {
                        // 首先检查是否不在结果集中，且对象是 Polyline 或 Line，且图层是 "板元"
                        if (!resultValues.Contains(kvp.Key) && kvp.Value is Entity entity && entity.Layer == "板元")
                        {
                            if (entity is Polyline)
                            {
                                entity.UpgradeOpen();
                                entity.Erase();
                            }
                        }
                    }
                }
                stopwatch.Stop();
                SimpleLogger.Log($"删除未被选中的有限元网格和板元图层上的对象耗时: {stopwatch.ElapsedMilliseconds} ms");
                trans.Commit();
                // 转换为普通字典
                stopwatch.Restart();
                Dictionary<DBText, ObjectId> resultDict = result.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                stopwatch.Stop();
                SimpleLogger.Log($"转换为普通字典耗时: {stopwatch.ElapsedMilliseconds} ms");
                // 高亮显示结果
                stopwatch.Restart();
                HighlightSelection(ed, resultDict);
                stopwatch.Stop();
                SimpleLogger.Log($"高亮显示结果耗时: {stopwatch.ElapsedMilliseconds} ms");
                return resultDict;
            }
        }
        // 检查点是否在多段线内部
        private static bool IsPointInsidePolyline(Polyline polyline, Point3d testPoint)
        {
            int n = polyline.NumberOfVertices;
            bool inside = false;
            Point2d testPoint2d = new Point2d(testPoint.X, testPoint.Y);
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Point2d pi = polyline.GetPoint2dAt(i);
                Point2d pj = polyline.GetPoint2dAt(j);
                if (((pi.Y > testPoint2d.Y) != (pj.Y > testPoint2d.Y)) &&
                     (testPoint2d.X < (pj.X - pi.X) * (testPoint2d.Y - pi.Y) / (pj.Y - pi.Y) + pi.X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }
        // 检查点是否在线上
        private static bool IsPointOnLine(Line line, Point3d point)
        {
            // 检查点是否在直线上
            Vector3d lineVec = line.EndPoint - line.StartPoint;
            Vector3d pointVec = point - line.StartPoint;
            double crossProduct = lineVec.CrossProduct(pointVec).Length;
            return crossProduct < Tolerance.Global.EqualPoint * lineVec.Length;
        }
        // 高亮显示选择的文字对象和网格对象
        private static void HighlightSelection(Editor ed, Dictionary<DBText, ObjectId> result)
        {
            List<ObjectId> ids = new List<ObjectId>();
            foreach (var kvp in result)
            {
                ids.Add(kvp.Key.ObjectId);
                ids.Add(kvp.Value);
            }
            if (ids.Count > 0)
            {
                ed.SetImpliedSelection(ids.ToArray());
                ed.WriteMessage($"\n高亮显示 {ids.Count} 个对象。");
            }
            else
            {
                ed.WriteMessage("\n未找到需要高亮显示的对象。");
            }
        }
    }
}
