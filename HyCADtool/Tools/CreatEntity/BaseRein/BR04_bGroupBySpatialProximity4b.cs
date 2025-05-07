using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
[assembly: CommandClass(typeof(HyCADTool.BaseRein))]
namespace HyCADTool
{
    public static partial class BaseRein
    {
        // 主方法：根据空间相邻关系对对象进行分组
        // 创建多边形方法
        public static List<Dictionary<DBText, ObjectId>> GroupBySpatialProximity(Dictionary<DBText, ObjectId> inputDict, double proximityThreshold)
        {
            List<Dictionary<DBText, ObjectId>> groups = new List<Dictionary<DBText, ObjectId>>();
            var visitedTexts = new ConcurrentDictionary<DBText, bool>();
            var toProcess = new ConcurrentQueue<DBText>();
            foreach (var kvp in inputDict)
            {
                if (!visitedTexts.ContainsKey(kvp.Key))
                {
                    Dictionary<DBText, ObjectId> group = new Dictionary<DBText, ObjectId>();
                    toProcess.Enqueue(kvp.Key);
                    visitedTexts.TryAdd(kvp.Key, true);
                    while (toProcess.TryDequeue(out DBText currentText))
                    {
                        if (currentText == null || !inputDict.ContainsKey(currentText))
                        {
                            continue;
                        }
                        group[currentText] = inputDict[currentText];
                        Parallel.ForEach(inputDict, otherKvp =>
                        {
                            if (!visitedTexts.ContainsKey(otherKvp.Key) && IsWithinProximity(currentText, otherKvp.Key, proximityThreshold))
                            {
                                if (visitedTexts.TryAdd(otherKvp.Key, true))
                                {
                                    toProcess.Enqueue(otherKvp.Key);
                                }
                            }
                        });
                    }
                    groups.Add(group);
                }
            }
            return groups;
        }
        // 辅助方法：判断两个DBText对象是否在指定的距离阈值内
        private static bool IsWithinProximity(DBText text1, DBText text2, double threshold)
        {
            Point3d position1 = text1.Position;
            Point3d position2 = text2.Position;
            return position1.DistanceTo(position2) <= threshold;
        }
        // 创建图层方法
        private static void CreateLayer(Database db, string layerName, short colorIndex)
        {
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)trans.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName))
                {
                    lt.UpgradeOpen();
                    LayerTableRecord ltr = new LayerTableRecord
                    {
                        Name = layerName,
                        Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
                    };
                    lt.Add(ltr);
                    trans.AddNewlyCreatedDBObject(ltr, true);
                }
                trans.Commit();
            }
        }
        // 创建多边形方法
    }
}
