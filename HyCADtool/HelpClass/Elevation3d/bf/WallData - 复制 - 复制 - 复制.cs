//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.Geometry;
//using HyCADTool.Tools;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//namespace HyCADTool.HelpClass.CreatBase
//{
//    public partial class ElevationModelGenerator
//    {
//        public void ExtendWallsAndGenerateRegions()
//        {
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var db = doc.Database;
//            var ed = doc.Editor;
//            using (var tr = db.TransactionManager.StartTransaction())
//            {
//                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
//                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
//                GeometryExtensions.JoinType = JoinType.Miter; // 设置连接类型为斜接（Miter）
//                // 逐个处理每个 geomData
//                foreach (var geomData in GeometryDatas)
//                {
//                    int wallCount = geomData.Walls.Count;
//                    if (wallCount == 0) continue;
//                    // 存储所有有效墙体的缓冲区和数据
//                    List<Polyline> buffers = new List<Polyline>();
//                    List<WallData> validWalls = new List<WallData>();
//                    // 首先生成所有有效墙体的缓冲区
//                    for (int i = 0; i < wallCount; i++)
//                    {
//                        var wall = geomData.Walls[i];
//                        // 添加条件验证
//                        if (wall.Thickness <= 0 || !wall.Boundary.IsWall
//                           )
//                            continue;
//                        // 生成当前墙体的缓冲区
//                        Polyline currentBuffer = GeometryExtensions.CreateWallBuffer(wall.Edge, wall.Thickness);
//                        // 设置图层并添加到绘图空间
//                        var id = EtGpt.CreateLayer("00_Hy_buffer", 36);
//                        currentBuffer.LayerId = id;
//                        btr.AppendEntity(currentBuffer);
//                        tr.AddNewlyCreatedDBObject(currentBuffer, true);
//                        buffers.Add(currentBuffer);
//                        validWalls.Add(wall);
//                    }
//                    int validCount = validWalls.Count;
//                    if (validCount < 2) continue; // 至少需要两个墙体才能形成连接
//                    // 处理所有墙体之间的连接，包括闭合
//                    for (int i = 0; i < validCount; i++)
//                    {
//                        int nextIndex = (i + 1) % validCount; // 取模操作处理闭合
//                        var currentWall = validWalls[i];
//                        var nextWall = validWalls[nextIndex];
//                        Polyline currentBuffer = buffers[i];
//                        Polyline nextBuffer = buffers[nextIndex];
//                        GeometryExtensions.HandleWallConnection(
//                            currentWall.Edge, nextWall.Edge,
//                            currentWall.Thickness, nextWall.Thickness,
//                            ref currentBuffer, ref nextBuffer
//                            );
//                    }
//                }
//                tr.Commit();
//            }
//        }
//        // 计算多边形中心点（保持不变）
//        private Point3d GetPolylineCenter(Polyline polyline)
//        {
//            if (polyline == null || polyline.NumberOfVertices == 0)
//                return Point3d.Origin;
//            double xSum = 0, ySum = 0, zSum = 0;
//            int vertexCount = polyline.NumberOfVertices;
//            for (int i = 0; i < vertexCount; i++)
//            {
//                Point3d vertex = polyline.GetPoint3dAt(i);
//                xSum += vertex.X;
//                ySum += vertex.Y;
//                zSum += vertex.Z;
//            }
//            return new Point3d(xSum / vertexCount, ySum / vertexCount, zSum / vertexCount);
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