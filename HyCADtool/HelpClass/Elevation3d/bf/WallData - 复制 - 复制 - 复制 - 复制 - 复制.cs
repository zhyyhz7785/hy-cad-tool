using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Tools;
using Microsoft.FSharp.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.HelpClass.CreatBase
{
    public partial class ElevationModelGenerator
    {
        // 主方法
        public void ExtendWallsAndGenerateRegions()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                GeometryExtensions.JoinType = JoinType.Miter;
                ed.WriteMessage("\n开始处理墙体扩展和区域生成...\n");
                foreach (var geomData in GeometryDatas)
                {
                    int wallCount = geomData.Walls.Count;
                    ed.WriteMessage($"\n处理几何数据 - 墙体数量: {wallCount}\n");
                    if (wallCount == 0)
                    {
                        ed.WriteMessage("无墙体数据，跳过处理\n");
                        continue;
                    }
                    geomData.GetWallSegments(); // 无返回值，直接更新属性
                    ed.WriteMessage($"墙体段类型: {geomData.SegmentType}\n");
                    switch (geomData.SegmentType)
                    {
                        case WallSegmentType.ClosedCurve:
                            ProcessClosedCurve(geomData, btr, tr);
                            break;
                        case WallSegmentType.SingleOpenCurve:
                            ProcessOpenCurves(geomData, btr, tr);
                            break;
                        case WallSegmentType.MultipleOpenCurves:
                            ProcessOpenCurves(geomData, btr, tr);
                            break;
                        default:
                            ed.WriteMessage("未定义的墙体类型，跳过处理\n");
                            continue;
                    }
                }
                ed.WriteMessage("\n墙体处理完成，提交事务\n");
                tr.Commit();
            }
        }
        // 处理封闭空间
        private void ProcessClosedCurve(GeometryData geomData, BlockTableRecord btr, Transaction tr)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            ed.WriteMessage("\n开始处理封闭曲线\n");
            if (!geomData.Polygon.Closed || !geomData.Polygon.StartPoint.IsEqualTo(geomData.Polygon.EndPoint))
            {
                ed.WriteMessage("\n警告: 多边形未闭合\n检测到非封闭状态，已跳过处理\n");
                return;
            }
            List<Polyline> buffers = new List<Polyline>();
            List<WallData> validWalls = new List<WallData>();
            for (int i = 0; i < geomData.ClosedCurve.Count; i++)
            {
                var wall = geomData.ClosedCurve[i];
                if (wall.Thickness <= 0 || !wall.Boundary.IsWall)
                {
                    ed.WriteMessage($"\n警告: 墙体 {i} 无效 (厚度: {wall.Thickness}, IsWall: {wall.Boundary.IsWall})\n");
                    continue;
                }
                Polyline qb1 = GeometryExtensions.CreateWallBuffer(wall.Edge, wall.Thickness);
                qb1.LayerId = ElevationModelGenerator.Bufferid1;
                buffers.Add(qb1);
                validWalls.Add(wall);
            }
            int validCount = validWalls.Count;
            ed.WriteMessage($"有效墙体数量: {validCount}\n");
            if (validCount < 3)
            {
                ed.WriteMessage("有效墙体数量不足以形成封闭空间，跳过处理\n");
                return;
            }
            for (int i = 0; i < validCount; i++)
            {
                int nextIndex = (i + 1) % validCount;
                var wall1 = validWalls[i];
                var wall2 = validWalls[nextIndex];
                Polyline qb1 = buffers[i];
                Polyline qb2 = buffers[nextIndex];
                GeometryExtensions.HandleWallConnection(
                    wall1.Edge, wall2.Edge,
                    wall1.Thickness, wall2.Thickness,
                    ref qb1, ref qb2);
                buffers[i] = qb1;
                buffers[nextIndex] = qb2;
                qb1.LayerId = ElevationModelGenerator.Bufferid2;
                qb2.LayerId = ElevationModelGenerator.Bufferid2;
                // 调试：检查连接后缓冲区形状
                ed.WriteMessage($"连接 {i} -> {nextIndex}: qb1 Vertices={qb1.NumberOfVertices}, Closed={qb1.Closed}\n");
                ed.WriteMessage($"连接 {i} -> {nextIndex}: qb2 Vertices={qb2.NumberOfVertices}, Closed={qb2.Closed}\n");
            }
            // 调试：检查所有缓冲区
            ed.WriteMessage("\n所有缓冲区状态：\n");
            for (int i = 0; i < buffers.Count; i++)
            {
                ed.WriteMessage($"Buffer[{i}]: Vertices={buffers[i].NumberOfVertices}, Closed={buffers[i].Closed}\n");
            }
            buffers.ToSpace();
        }
        // 处理非封闭空间
        private void ProcessOpenCurves(GeometryData geomData, BlockTableRecord btr, Transaction tr)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            ed.WriteMessage("\n开始处理非封闭曲线\n");
            List<Polyline> buffers = new List<Polyline>();
            List<WallData> validWalls = new List<WallData>();
            var layerId = Bufferid4; // 假设这是已定义的图层 ID
            // 获取需要处理的墙体列表
            List<WallData> wallsToProcess = geomData.SegmentType == WallSegmentType.SingleOpenCurve
                ? geomData.SingleOpenCurve
                : geomData.MultipleOpenCurves.SelectMany(x => x).ToList();
            // 生成所有有效墙体的缓冲区
            for (int i = 0; i < wallsToProcess.Count; i++)
            {
                var wall = wallsToProcess[i];
                if (wall.Thickness <= 0 || !wall.Boundary.IsWall)
                {
                    ed.WriteMessage($"\n警告: 墙体 {i} 无效 (厚度: {wall.Thickness}, IsWall: {wall.Boundary.IsWall})\n");
                    continue;
                }
                Polyline qb;
                if (i == 0 || i == wallsToProcess.Count - 1) // 处理端点墙体
                {
                    bool isStart = i == 0;
                    EndType endType = wall.EndType; // 假设 WallData 中有 EndType 属性
                    qb = GeometryExtensions.HandleEndpoint(wall, isStart, endType);
                }
                else
                {
                    qb = GeometryExtensions.CreateWallBuffer(wall.Edge, wall.Thickness);
                }
                qb.LayerId = layerId;
                buffers.Add(qb);
                validWalls.Add(wall);
            }
            buffers.ToSpace();
            int validCount = validWalls.Count;
            ed.WriteMessage($"有效墙体数量: {validCount}\n");
            if (validCount < 1)
            {
                ed.WriteMessage("无有效墙体，跳过处理\n");
                return;
            }
            // 处理墙体之间的连接
            for (int i = 0; i < validCount - 1; i++)
            {
                var wall1 = validWalls[i];
                var wall2 = validWalls[i + 1];
                Polyline qb1 = buffers[i];
                Polyline qb2 = buffers[i + 1];
                GeometryExtensions.HandleWallConnection(
                    wall1.Edge, wall2.Edge,
                    wall1.Thickness, wall2.Thickness,
                    ref qb1, ref qb2);
                buffers[i] = qb1;
                buffers[i + 1] = qb2;
                qb1.LayerId = ElevationModelGenerator.Bufferid3; // 假设这是连接后的图层 ID
                qb2.LayerId = ElevationModelGenerator.Bufferid3;
            }
            // 确保最后一个缓冲区的图层正确
            if (validCount > 0)
            {
                buffers[validCount - 1].LayerId = ElevationModelGenerator.Bufferid3;
            }
            // 调试输出
            ed.WriteMessage($"\n最终 buffers 数量: {buffers.Count}\n");
            for (int i = 0; i < buffers.Count; i++)
            {
                ed.WriteMessage($"buffers[{i}] 图层: {buffers[i].Layer}\n");
            }
            // 将缓冲区写入空间
            // 使用特定命名空间的 ToSpace 方法
            buffers.ToSpace();
        }
        // 计算多边形中心点
        private Point3d GetPolylineCenter(Polyline polyline)
        {
            if (polyline == null || polyline.NumberOfVertices == 0)
                return Point3d.Origin;
            double xSum = 0, ySum = 0, zSum = 0;
            int vertexCount = polyline.NumberOfVertices;
            for (int i = 0; i < vertexCount; i++)
            {
                Point3d vertex = polyline.GetPoint3dAt(i);
                xSum += vertex.X;
                ySum += vertex.Y;
                zSum += vertex.Z;
            }
            return new Point3d(xSum / vertexCount, ySum / vertexCount, zSum / vertexCount);
        }
        // 拉伸墙体区域
        private void ExtrudeWallRegion(Polyline wallRegion, double innerElevation, double outerElevation, BlockTableRecord btr, Transaction tr)
        {
            if (!wallRegion.Closed)
            {
                throw new InvalidOperationException("墙体区域未闭合，无法拉伸。");
            }
            double height = outerElevation - innerElevation;
            if (Math.Abs(height) < Tolerance.Global.EqualPoint)
            {
                throw new InvalidOperationException($"拉伸高度无效: InnerElevation={innerElevation}, OuterElevation={outerElevation}");
            }
            wallRegion.Elevation = innerElevation;
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage($"\n拉伸墙体: Vertices={wallRegion.NumberOfVertices}, Closed={wallRegion.Closed}, Height={height}");
            using (Solid3d solid = new Solid3d())
            {
                solid.CreateExtrudedSolid(wallRegion, new Vector3d(0, 0, height), new SweepOptions());
                btr.AppendEntity(solid);
                tr.AddNewlyCreatedDBObject(solid, true);
            }
        }
    }
}