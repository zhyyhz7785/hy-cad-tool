using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using System.Collections.Generic;
[assembly: CommandClass(typeof(HyCADTool.BaseRein))]
namespace HyCADTool
{
    public static partial class BaseRein
    {
        // 主方法：找出一个Polyline的包围框，并将它转换为新的多边形
        public static Dictionary<DBText, ObjectId> DrawBoundingPolyline(Dictionary<DBText, ObjectId> inputDict)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            Dictionary<DBText, ObjectId> newDict = new Dictionary<DBText, ObjectId>();
            // 创建图层
            EtGpt.SetCurrentLayer("00_hy_配筋轮廓");
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    foreach (var kvp in inputDict)
                    {
                        Polyline oldPolyline = trans.GetObject(kvp.Value, OpenMode.ForWrite) as Polyline;
                        if (oldPolyline != null)
                        {
                            // 创建包围框多边形
                            Polyline newPolyline = CreateBoundingBoxPolyline(oldPolyline);
                            // 设置图层和颜色
                            newPolyline.Layer = "00_hy_配筋轮廓";
                            // 将新的多边形添加到模型空间
                            BlockTable bt = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
                            BlockTableRecord btr = (BlockTableRecord)trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                            btr.AppendEntity(newPolyline);
                            trans.AddNewlyCreatedDBObject(newPolyline, true);
                            // 将新的Polyline的ObjectId添加到新的字典中
                            newDict.Add(kvp.Key, newPolyline.ObjectId);
                            // 删除原有多边形
                            oldPolyline.Erase(true);
                        }
                    }
                    trans.Commit();
                }
            }
            return newDict;
        }
        // 辅助方法：创建表示包围框的多边形
        private static Polyline CreateBoundingBoxPolyline(Polyline polyline)
        {
            Polyline newPolyline = new Polyline();
            Extents3d boundingBox = polyline.GeometricExtents;
            Point3d minPoint = boundingBox.MinPoint;
            Point3d maxPoint = boundingBox.MaxPoint;
            newPolyline.AddVertexAt(0, new Point2d(minPoint.X, minPoint.Y), 0, 0, 0);
            newPolyline.AddVertexAt(1, new Point2d(maxPoint.X, minPoint.Y), 0, 0, 0);
            newPolyline.AddVertexAt(2, new Point2d(maxPoint.X, maxPoint.Y), 0, 0, 0);
            newPolyline.AddVertexAt(3, new Point2d(minPoint.X, maxPoint.Y), 0, 0, 0);
            newPolyline.Closed = true;
            return newPolyline;
        }
    }
}
