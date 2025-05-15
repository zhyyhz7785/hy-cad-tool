//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.Geometry;
//using HyCADTool.Models.Cluster;
//using HyCADTool.Tools;
//using System.Collections.Generic;

//namespace HyCADTool.Models.Annotation
//{
//    /// <summary>
//    /// 专用于将 DimPointsAndAxis 分类的点集绘制到模型空间中。
//    /// </summary>
//    public static class DimPointDrawer
//    {
//        public static void Draw(DimPointsAndAxis data)
//        {
//            if (data == null) return;

//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;

//            using (var docLock = doc.LockDocument())
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

//                DrawPointsToLayer(data.BPs, "00_hy_基础_点_基础轮廓", 152, db, tr, btr);
//                DrawPointsToLayer(data.A_APs, "00_hy_基础_点_轨迹交点", 12, db, tr, btr);
//                DrawPointsToLayer(data.B_APs, "00_hy_基础_点_轨迹基础", 32, db, tr, btr);
//                DrawPointsToLayer(data.ABs, "00_hy_基础_点_螺栓", 157, db, tr, btr);
//                DrawPointsToLayer(data.SteelPlatePs, "00_hy_基础_点_预埋钢板", 42, db, tr, btr);

//                tr.Commit();
//            }
//        }

//        private static void DrawPointsToLayer(List<Point3d> points, string layerName, short colorIndex, Database db, Transaction tr, BlockTableRecord btr)
//        {
//            if (points == null || points.Count == 0) return;

//            var layerId = EtGpt.CreateLayer(layerName, colorIndex, db);
//            foreach (var pt in points)
//            {
//                DBPoint dbPt = new DBPoint(pt)
//                {
//                    Layer = layerName
//                };
//                btr.AppendEntity(dbPt);
//                tr.AddNewlyCreatedDBObject(dbPt, true);
//            }
//        }
//    }
//}
