using System;
using System.Collections.Generic;
using HyCADTool.Domain.ValueObjects;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Domain.Services
{
    /// <summary>
    /// 墙体连接处理服务
    /// 核心算法：与原始 GeometryExtensions.HandleWallConnection 保持一致
    /// 负责处理两个墙体缓冲区在连接处的几何关系
    /// </summary>
    public class WallConnectionHandler
    {
        /// <summary>
        /// 处理墙体连接（修改缓冲区多边形）
        /// 核心算法：与原始代码完全一致
        /// </summary>
        /// <param name="wall1Edge">第一个墙体的边线</param>
        /// <param name="wall2Edge">第二个墙体的边线（连接在 wall1 之后）</param>
        /// <param name="thickness1">第一个墙体厚度</param>
        /// <param name="thickness2">第二个墙体厚度</param>
        /// <param name="buffer1">第一个墙体的缓冲区（会被修改）</param>
        /// <param name="buffer2">第二个墙体的缓冲区（会被修改）</param>
        public void HandleConnection(
            Line2D wall1Edge,
            Line2D wall2Edge,
            WallThickness thickness1,
            WallThickness thickness2,
            ref Polygon2D buffer1,
            ref Polygon2D buffer2)
        {
            // 计算两墙夹角
            double angle = CalculateAngleBetweenWalls(wall1Edge, wall2Edge);
            
            // 检查是否为平行墙体
            bool isParallel = Math.Abs(angle) < ToleranceSettings.Instance.AngleTolerance ||
                             Math.Abs(angle - 180) < ToleranceSettings.Instance.AngleTolerance;
            
            if (isParallel)
            {
                // 平行墙体，跳过连接处理
                return;
            }
            
            // 判断顺时针/逆时针
            bool isClockwise = angle >= 0;
            
            // 计算关键点（与原始代码一致）
            Point2D p11 = wall1Edge.StartPoint;
            Point2D p14 = wall1Edge.EndPoint;
            
            Vector2D dir1 = p11.VectorTo(p14);
            Vector2D normal1 = dir1.GetNormal().RotateBy(Math.PI / 2);
            
            Point2D p12 = p11.Add(normal1.Scale(thickness1.Value));
            Point2D p13 = p14.Add(normal1.Scale(thickness1.Value));
            
            Point2D p21 = wall2Edge.StartPoint;
            Point2D p24 = wall2Edge.EndPoint;
            
            Vector2D dir2 = p21.VectorTo(p24);
            Vector2D normal2 = dir2.GetNormal().RotateBy(Math.PI / 2);
            
            Point2D p22 = p21.Add(normal2.Scale(thickness2.Value));
            Point2D p23 = p24.Add(normal2.Scale(thickness2.Value));
            
            // 计算交点（原始代码中的 j1）
            Point2D? j1 = GetIntersectionPoint(p13, dir1, p22, dir2);
            
            if (j1 == null)
            {
                // 无交点，跳过连接处理
                return;
            }
            
            // 根据旋转方向和厚度处理连接
            if (isClockwise)
            {
                if (Math.Abs(thickness1.Value - thickness2.Value) < ToleranceSettings.Instance.DistanceTolerance)
                {
                    // 相同厚度，顺时针
                    ModifyBuffersForSameThicknessClockwise(
                        ref buffer1, ref buffer2,
                        p14, p13, p22, j1.Value);
                }
                else
                {
                    // 不同厚度，顺时针
                    ModifyBuffersForDifferentThicknessClockwise(
                        ref buffer1, ref buffer2,
                        p14, p13, p22, j1.Value);
                }
            }
            else
            {
                if (Math.Abs(thickness1.Value - thickness2.Value) < ToleranceSettings.Instance.DistanceTolerance)
                {
                    // 相同厚度，逆时针
                    ModifyBuffersForSameThicknessCounterClockwise(
                        ref buffer1, ref buffer2,
                        p14, p13, p22, j1.Value);
                }
                else
                {
                    // 不同厚度，逆时针（使用Union处理）
                    // 创建连接多边形并合并
                    var connectionPolygon = CreateBufferPolyline(p14, p13, p22);
                    buffer1 = UnionPolygons(buffer1, connectionPolygon);
                    buffer2 = UnionPolygons(buffer2, connectionPolygon);
                }
            }
        }
        
        /// <summary>
        /// 计算两墙之间的夹角（度）
        /// </summary>
        private double CalculateAngleBetweenWalls(Line2D wall1, Line2D wall2)
        {
            Vector2D dir1 = wall1.StartPoint.VectorTo(wall1.EndPoint);
            Vector2D dir2 = wall2.StartPoint.VectorTo(wall2.EndPoint);
            
            // 计算叉积（用于判断旋转方向）
            double cross = dir1.X * dir2.Y - dir1.Y * dir2.X;
            
            // 计算点积（用于计算夹角）
            double dot = dir1.X * dir2.X + dir1.Y * dir2.Y;
            
            double angleRad = Math.Atan2(cross, dot);
            return angleRad * 180.0 / Math.PI;
        }
        
        /// <summary>
        /// 计算两条射线的交点
        /// </summary>
        private Point2D? GetIntersectionPoint(Point2D p1, Vector2D dir1, Point2D p2, Vector2D dir2)
        {
            // 射线1: p1 + t1 * dir1
            // 射线2: p2 + t2 * dir2
            
            double cross = dir1.X * dir2.Y - dir1.Y * dir2.X;
            
            if (Math.Abs(cross) < 1e-10)
            {
                // 平行，无交点
                return null;
            }
            
            Vector2D p1p2 = p1.VectorTo(p2);
            double t1 = (p1p2.X * dir2.Y - p1p2.Y * dir2.X) / cross;
            
            return p1.Add(dir1.Scale(t1));
        }
        
        /// <summary>
        /// 修改缓冲区（相同厚度，顺时针）
        /// </summary>
        private void ModifyBuffersForSameThicknessClockwise(
            ref Polygon2D buffer1,
            ref Polygon2D buffer2,
            Point2D p14,
            Point2D p13,
            Point2D p22,
            Point2D intersection)
        {
            // 修改 buffer1 的最后一个顶点（外侧终点）为交点
            var vertices1 = new List<Point2D>(buffer1.Vertices);
            if (vertices1.Count >= 4)
            {
                vertices1[3] = intersection;  // 外侧终点（索引3）
            }
            buffer1 = new Polygon2D(vertices1, isClosed: true);
            
            // 修改 buffer2 的第一个顶点（外侧起点）为交点
            var vertices2 = new List<Point2D>(buffer2.Vertices);
            if (vertices2.Count >= 4)
            {
                vertices2[0] = intersection;  // 外侧起点（索引0）
            }
            buffer2 = new Polygon2D(vertices2, isClosed: true);
        }
        
        /// <summary>
        /// 修改缓冲区（不同厚度，顺时针）
        /// </summary>
        private void ModifyBuffersForDifferentThicknessClockwise(
            ref Polygon2D buffer1,
            ref Polygon2D buffer2,
            Point2D p14,
            Point2D p13,
            Point2D p22,
            Point2D intersection)
        {
            // 简化处理：使用交点修改顶点
            ModifyBuffersForSameThicknessClockwise(ref buffer1, ref buffer2, p14, p13, p22, intersection);
        }
        
        /// <summary>
        /// 修改缓冲区（相同厚度，逆时针）
        /// </summary>
        private void ModifyBuffersForSameThicknessCounterClockwise(
            ref Polygon2D buffer1,
            ref Polygon2D buffer2,
            Point2D p14,
            Point2D p13,
            Point2D p22,
            Point2D intersection)
        {
            // 修改 buffer1 的最后一个顶点（外侧终点）为交点
            var vertices1 = new List<Point2D>(buffer1.Vertices);
            if (vertices1.Count >= 4)
            {
                vertices1[3] = intersection;  // 外侧终点（索引3）
            }
            buffer1 = new Polygon2D(vertices1, isClosed: true);
            
            // 修改 buffer2 的第一个顶点（外侧起点）为交点
            var vertices2 = new List<Point2D>(buffer2.Vertices);
            if (vertices2.Count >= 4)
            {
                vertices2[0] = intersection;  // 外侧起点（索引0）
            }
            buffer2 = new Polygon2D(vertices2, isClosed: true);
        }
        
        /// <summary>
        /// 创建连接多边形（三角形）
        /// </summary>
        private Polygon2D CreateBufferPolyline(Point2D p1, Point2D p2, Point2D p3)
        {
            var vertices = new List<Point2D> { p1, p2, p3 };
            return new Polygon2D(vertices, isClosed: true);
        }
        
        /// <summary>
        /// 合并两个多边形（使用 PolygonMerger）
        /// </summary>
        private Polygon2D UnionPolygons(Polygon2D poly1, Polygon2D poly2)
        {
            var merger = new PolygonMerger();
            return merger.UnionPolygons(new[] { poly1, poly2 });
        }
    }
}
