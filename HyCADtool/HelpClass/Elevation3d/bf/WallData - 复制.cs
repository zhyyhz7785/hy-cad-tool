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
//                var wallBuffers = new List<Polyline>();
//                var wallDataList = new List<WallData>();
//                foreach (var geomData in GeometryDatas)
//                {
//                    foreach (var wall in geomData.Walls)
//                    {
//                        if (wall.Thickness <= 0 || !wall.Boundary.IsWall)
//                            continue;
//                        Polyline buffer = GeometryExtensions.CreateWallBuffer(wall.Edge, wall.Thickness);
//                        wallBuffers.Add(buffer);
//                        wallDataList.Add(wall);
//                        btr.AppendEntity(buffer);
//                        tr.AddNewlyCreatedDBObject(buffer, true);
//                    }
//                }
//                for (int i = 0; i < wallBuffers.Count - 1; i++)
//                {
//                    var q1 = wallDataList[i].Edge;
//                    var q2 = wallDataList[i + 1].Edge;
//                    var thickness1 = wallDataList[i].Thickness;
//                    var thickness2 = wallDataList[i + 1].Thickness;
//                    if (q1.EndPoint.IsEqualTo(q2.StartPoint, new Tolerance(0.001, 0.001)))
//                    {
//                        Polyline qb1 = wallBuffers[i];
//                        Polyline qb2 = wallBuffers[i + 1];
//                        GeometryExtensions.HandleWallConnection(q1, q2, thickness1, thickness2, ref qb1, ref qb2);
//                        wallBuffers[i] = qb1;
//                        wallBuffers[i + 1] = qb2;
//                    }
//                }
//                //for (int i = 0; i < wallBuffers.Count; i++)
//                //{
//                //    var wall = wallDataList[i];
//                //    try
//                //    {
//                //        ExtrudeWallRegion(wallBuffers[i], wall.InnerElevation, wall.OuterElevation, btr, tr);
//                //    }
//                //    catch (Autodesk.AutoCAD.Runtime.Exception ex)
//                //    {
//                //        doc.Editor.WriteMessage($"\n拉伸墙体失败 (Wall {i}): {ex.Message}");
//                //    }
//                //}
//                //tr.Commit();
//            }
//        }
//        private void ExtrudeWallRegion(Polyline wallRegion, double innerElevation, double outerElevation, BlockTableRecord btr, Transaction tr)
//        {
//            // 检查是否闭合
//            if (!wallRegion.Closed)
//            {
//                throw new InvalidOperationException("墙体区域未闭合，无法拉伸。");
//            }
//            // 检查高度差
//            double height = outerElevation - innerElevation;
//            if (Math.Abs(height) < Tolerance.Global.EqualPoint)
//            {
//                throw new InvalidOperationException($"拉伸高度无效: InnerElevation={innerElevation}, OuterElevation={outerElevation}");
//            }
//            // 设置基底高度
//            wallRegion.Elevation = innerElevation;
//            // 调试信息
//            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
//            ed.WriteMessage($"\n拉伸墙体: Vertices={wallRegion.NumberOfVertices}, Closed={wallRegion.Closed}, Height={height}");
//            // 创建拉伸实体
//            using (Solid3d solid = new Solid3d())
//            {
//                solid.CreateExtrudedSolid(wallRegion, new Vector3d(0, 0, height), new SweepOptions());
//                btr.AppendEntity(solid);
//                tr.AddNewlyCreatedDBObject(solid, true);
//            }
//        }
//    }
//}