using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
[assembly: CommandClass(typeof(HyCADTool.Command.DrawBaseReinforcement))]
namespace HyCADTool.Command
{
    public static partial class DrawBaseReinforcement
    {
        // 主方法：根据空间相邻关系对对象进行分组
        public static List<Dictionary<DBText, ObjectId>> GroupBySpatialProximity(Dictionary<DBText, ObjectId> inputDict, double proximityThreshold)
        {
            List<Dictionary<DBText, ObjectId>> groups = new List<Dictionary<DBText, ObjectId>>();
            HashSet<DBText> visitedTexts = new HashSet<DBText>();
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            foreach (var kvp in inputDict)
            {
                if (!visitedTexts.Contains(kvp.Key))
                {
                    Dictionary<DBText, ObjectId> group = new Dictionary<DBText, ObjectId>();
                    Queue<DBText> toProcess = new Queue<DBText>();
                    toProcess.Enqueue(kvp.Key);
                    visitedTexts.Add(kvp.Key);
                    while (toProcess.Count > 0)
                    {
                        DBText currentText = toProcess.Dequeue();
                        group[currentText] = inputDict[currentText];
                        foreach (var otherKvp in inputDict)
                        {
                            if (!visitedTexts.Contains(otherKvp.Key) && IsWithinProximity(currentText, otherKvp.Key, proximityThreshold))
                            {
                                toProcess.Enqueue(otherKvp.Key);
                                visitedTexts.Add(otherKvp.Key);
                            }
                        }
                    }
                    groups.Add(group);
                }
            }
            int groupIndex = 1;
            foreach (var group in groups)
            {
                ed.WriteMessage($"\n组 {groupIndex++} 包含 {group.Count} 个文字对象。");
                CreatePolylineFromGroup(group, db, "00_hy_形心连线", 5);
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
        private static void CreatePolylineFromGroup(Dictionary<DBText, ObjectId> group, Database db, string layerName, short colorIndex)
        {
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                // 创建图层
                CreateLayer(db, layerName, colorIndex);
                // 创建多边形
                Polyline polyline = new Polyline();
                int vertexIndex = 0;
                foreach (var kvp in group)
                {
                    DBText text = trans.GetObject(kvp.Key.ObjectId, OpenMode.ForRead) as DBText;
                    if (text != null)
                    {
                        Point3d position = text.Position;
                        polyline.AddVertexAt(vertexIndex++, new Point2d(position.X, position.Y), 0, 0, 0);
                    }
                }
                polyline.Closed = true;
                polyline.Layer = layerName;
                // 将多边形添加到模型空间
                BlockTable bt = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                btr.AppendEntity(polyline);
                trans.AddNewlyCreatedDBObject(polyline, true);
                trans.Commit();
            }
        }
    }
}
