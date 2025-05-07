//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.Geometry;
//using System;
//using System.Collections.Generic;
//namespace HyCADTool.HelpClass.CreatBase
//{
//    public partial class ElevationModelGenerator
//    {
//        public void ExtendWallsAndGenerateRegions()
//        {
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var db = doc.Database;
//            using (var tr = db.TransactionManager.StartTransaction())
//            {
//                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
//                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
//                foreach (var geomData in GeometryDatas)
//                {
//                    foreach (var wall in geomData.Walls)
//                    {
//                        if (wall.Thickness <= 0 || !wall.Boundary.IsWall)
//                            continue; // 跳过非墙体或厚度无效的墙
//                        // 计算延伸后的墙体区域
//                        Polyline extendedWallRegion = CreateExtendedWallRegion(wall);
//                        // 添加新的墙体区域到模型空间
//                        btr.AppendEntity(extendedWallRegion);
//                        tr.AddNewlyCreatedDBObject(extendedWallRegion, true);
//                        // 拉伸墙体区域
//                        ExtrudeWallRegion(extendedWallRegion, wall.InnerElevation, wall.OuterElevation, btr, tr);
//                    }
//                }
//                tr.Commit();
//            }
//        }
//        private Polyline CreateExtendedWallRegion(WallData wall)
//        {
//            Line edge = wall.Edge;
//            double thickness = wall.Thickness;
//            // 获取线的方向向量
//            Vector3d direction = edge.EndPoint - edge.StartPoint;
//            Vector3d normal = direction.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis); // 法向量，向外旋转90度
//            // 计算四个顶点
//            Point3d p1 = edge.StartPoint - direction.GetNormal() * thickness; // 端头延伸
//            Point3d p2 = edge.StartPoint + normal * thickness;
//            Point3d p3 = edge.EndPoint + normal * thickness;
//            Point3d p4 = edge.EndPoint + direction.GetNormal() * thickness; // 端头延伸
//            // 创建新的闭合 Polyline 表示墙体区域
//            Polyline extendedWall = new Polyline();
//            extendedWall.AddVertexAt(0, new Point2d(p1.X, p1.Y), 0, 0, 0);
//            extendedWall.AddVertexAt(1, new Point2d(p2.X, p2.Y), 0, 0, 0);
//            extendedWall.AddVertexAt(2, new Point2d(p3.X, p3.Y), 0, 0, 0);
//            extendedWall.AddVertexAt(3, new Point2d(p4.X, p4.Y), 0, 0, 0);
//            extendedWall.Closed = true;
//            return extendedWall;
//        }
//        private void ExtrudeWallRegion(Polyline wallRegion, double innerElevation, double outerElevation, BlockTableRecord btr, Transaction tr)
//        {
//            // 设置基底高度为 InnerElevation
//            wallRegion.Elevation = innerElevation;
//            // 创建 Solid3d 并拉伸
//            using (Solid3d solid = new Solid3d())
//            {
//                solid.CreateExtrudedSolid(wallRegion, new Vector3d(0, 0, outerElevation - innerElevation), new SweepOptions());
//                btr.AppendEntity(solid);
//                tr.AddNewlyCreatedDBObject(solid, true);
//            }
//        }
//    }
//}