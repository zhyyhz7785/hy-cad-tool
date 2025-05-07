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
//            // 获取当前活动文档、数据库和编辑器对象，用于 AutoCAD 操作
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var db = doc.Database;
//            var ed = doc.Editor;
//            // 开启事务处理，确保所有操作可以回滚或提交
//            using (var tr = db.TransactionManager.StartTransaction())
//            {
//                // 设置缓冲区的图层为 "00_Hy_buffer"，颜色索引为 36，并将其添加到模型空间
//                // 创建或获取图层
//                // 获取块表（BlockTable）和模型空间（ModelSpace），用于存储和操作实体
//                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead); // 只读模式打开块表
//                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite); // 写入模式打开模型空间
//                // 设置 GeometryExtensions 的连接类型为 Miter（斜接），影响墙体连接的外观
//                GeometryExtensions.JoinType = JoinType.Miter;
//                // 遍历 GeometryDatas 中的每个几何数据对象，处理其中的墙体
//                foreach (var geomData in GeometryDatas)
//                {
//                    int wallCount = geomData.Walls.Count; // 获取当前几何数据的墙体数量
//                    if (wallCount == 0) continue; // 如果没有墙体，跳过本次循环
//                    // 初始化列表，用于存储有效的墙体数据和对应的缓冲区多段线
//                    List<Polyline> buffers = new List<Polyline>(); // 存储缓冲区多段线
//                    List<WallData> validWalls = new List<WallData>(); // 存储有效墙体数据
//                    // 遍历所有墙体，生成初始缓冲区并筛选有效墙体
//                    for (int i = 0; i < wallCount; i++)
//                    {
//                        var wall = geomData.Walls[i]; // 获取当前墙体数据
//                        // 验证墙体是否有效：厚度必须大于 0 且 Boundary.IsWall 为 true
//                        if (wall.Thickness <= 0 || !wall.Boundary.IsWall)
//                            continue; // 无效墙体跳过
//                        // 使用 GeometryExtensions.CreateWallBuffer 生成墙体的初始缓冲区多段线
//                        Polyline currentBuffer = GeometryExtensions.CreateWallBuffer(wall.Edge, wall.Thickness);
//                        currentBuffer.LayerId = ElevationModelGenerator.BufferId; // 设置图层
//                        btr.AppendEntity(currentBuffer); // 添加到模型空间
//                        tr.AddNewlyCreatedDBObject(currentBuffer, true); // 注册为新对象，确保事务提交时保存
//                        // 将生成的缓冲区和墙体数据添加到列表
//                        buffers.Add(currentBuffer);
//                        validWalls.Add(wall);
//                    }
//                    int validCount = validWalls.Count; // 获取有效墙体的数量
//                    if (validCount < 3) continue; // 如果有效墙体少于 3 个，无法处理 q1、q2、q3，跳过
//                    // 遍历有效墙体，处理它们之间的连接（包括闭合）
//                    for (int i = 0; i < validCount; i++)
//                    {
//                        // 计算当前墙体 (q1)、下一墙体 (q2) 和下下一墙体 (q3) 的索引，支持闭环
//                        int nextIndex = (i + 1) % validCount; // q2 的索引，使用取模实现闭合
//                        int nextNextIndex = (i + 2) % validCount; // q3 的索引
//                        // 获取 q1、q2、q3 对应的墙体数据
//                        var currentWall = validWalls[i]; // q1：前一个墙体
//                        var nextWall = validWalls[nextIndex]; // q2：当前处理的墙体
//                        var nextNextWall = validWalls[nextNextIndex]; // q3：后一个墙体
//                        // 获取 q1、q2、q3 对应的缓冲区多段线
//                        Polyline currentBuffer = buffers[i]; // qb1
//                        Polyline nextBuffer = buffers[nextIndex]; // qb2
//                        Polyline nextNextBuffer = buffers[nextNextIndex]; // qb3
//                        // 调用 HandleWallConnection 处理 q1、q2、q3 的连接
//                        GeometryExtensions.HandleWallConnection(
//                            currentWall.Edge, nextWall.Edge, nextNextWall.Edge, // 传递 q1、q2、q3 的边线
//                            currentWall.Thickness, nextWall.Thickness, nextNextWall.Thickness, // 传递厚度
//                            ref currentBuffer, ref nextBuffer, ref nextNextBuffer, // 传递缓冲区引用，可能被修改
//                            nextWall, geomData.Walls, i, // nextWall 作为当前墙体数据，传递所有墙体列表和索引
//                            out bool skipConnection); // 输出参数，指示是否跳过了 q2 的连接处理
//                        // 更新 buffers 列表，因为 ref 参数可能在 HandleWallConnection 中被修改
//                        buffers[i] = currentBuffer;
//                        buffers[nextIndex] = nextBuffer;
//                        buffers[nextNextIndex] = nextNextBuffer;
//                        // 输出调试信息，显示处理的墙体索引和跳过状态
//                        ed.WriteMessage($"\nProcessed walls {i}, {nextIndex}, {nextNextIndex}. SkipConnection: {skipConnection}\n");
//                    }
//                }
//                // 提交事务，将所有更改保存到数据库
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