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
//                GeometryExtensions.JoinType = JoinType.Miter;
//                foreach (var geomData in GeometryDatas)
//                {
//                    int wallCount = geomData.Walls.Count;
//                    if (wallCount == 0) continue;
//                    List<Polyline> buffers;
//                    // 判断是否闭合
//                    if (geomData.Polygon.Closed && geomData.Polygon.StartPoint.IsEqualTo(geomData.Polygon.EndPoint))
//                    {
//                        // 闭合的 Polyline
//                        buffers = GeometryExtensions.GenerateWallBuffers(geomData, isClosed: true);
//                    }
//                    else
//                    {
//                        // 不闭合的连续线段
//                        buffers = GeometryExtensions.GenerateWallBuffers(geomData, isClosed: false);
//                    }
//                    if (buffers.Count == 0) continue;
//                    var layerId = EtGpt.CreateLayer("00_Hy_buffer", 36);
//                    foreach (var buffer in buffers)
//                    {
//                        if (buffer.Database == null)
//                        {
//                            buffer.LayerId = layerId;
//                            btr.AppendEntity(buffer);
//                            tr.AddNewlyCreatedDBObject(buffer, true);
//                        }
//                        else
//                        {
//                            ed.WriteMessage($"\n警告: Polyline 已存在于数据库中，跳过添加。\n");
//                        }
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