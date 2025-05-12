using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;
using HyCADTool.Tools;
namespace HyCADTool.Annotation
{
    public static class AutoCADDimensionHelper
    {
        /// <summary>
        /// 添加 X 向标注（下侧）
        /// </summary>
        public static void AddXDimension(List<Point3d> points, Point3d dimLinePoint)
        {
            if (points == null || points.Count < 2) return;
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                // ✅ 创建图层
                string layerName = "00_hy_3公共_标注2_内x";
                EtGpt.CreateLayer(layerName, 93, db, ed); // 蓝色
                points.Sort((a, b) => a.X.CompareTo(b.X));
                for (int i = 0; i < points.Count - 1; i++)
                {
                    Point3d p1 = points[i];
                    Point3d p2 = points[i + 1];
                    Point3d dimLineLocation = new Point3d(p1.X, dimLinePoint.Y, p1.Z);
                    AlignedDimension dim = new AlignedDimension(p1, p2, dimLineLocation, "", db.Dimstyle);
                    dim.Layer = layerName;
                    btr.AppendEntity(dim);
                    tr.AddNewlyCreatedDBObject(dim, true);
                }
                tr.Commit();
            }
        }
        /// <summary>
        /// 添加 Y 向标注（左侧）
        /// </summary>
        public static void AddYDimension(List<Point3d> points, Point3d dimLinePoint)
        {
            if (points == null || points.Count < 2) return;
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                // ✅ 创建图层
                string layerName = "00_hy_3公共_标注2_内y";
                EtGpt.CreateLayer(layerName, 45, db, ed); // 绿色
                points.Sort((a, b) => a.Y.CompareTo(b.Y));
                for (int i = 0; i < points.Count - 1; i++)
                {
                    Point3d p1 = points[i];
                    Point3d p2 = points[i + 1];
                    Point3d dimLineLocation = new Point3d(dimLinePoint.X, p1.Y, p1.Z);
                    AlignedDimension dim = new AlignedDimension(p1, p2, dimLineLocation, "", db.Dimstyle);
                    dim.Layer = layerName;
                    btr.AppendEntity(dim);
                    tr.AddNewlyCreatedDBObject(dim, true);
                }
                tr.Commit();
            }
        }
    }
}
