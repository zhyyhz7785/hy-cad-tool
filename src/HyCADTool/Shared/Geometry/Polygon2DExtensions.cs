using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;

namespace HyCADTool.Shared.Geometry
{
    /// <summary>
    /// Polygon2D 扩展方法
    /// </summary>
    public static class Polygon2DExtensions
    {
        /// <summary>
        /// 多边形偏移（外扩或内缩）
        /// 
        /// 使用 Clipper2.InflatePaths 实现，完整支持自交处理
        /// Original: Clipper2/CPP/Clipper2Lib/src/Clipper.Offset.cpp
        /// </summary>
        /// <param name="polygon">原始多边形</param>
        /// <param name="distance">偏移距离（mm）</param>
        /// <param name="isOutward">true=外扩，false=内缩</param>
        /// <returns>偏移后的多边形</returns>
        public static Polygon2D Offset(this Polygon2D polygon, double distance, bool isOutward)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));
            
            if (distance < 0)
                throw new ArgumentException("偏移距离必须为正值", nameof(distance));
            
            if (polygon.VertexCount < 3)
                throw new InvalidOperationException("多边形至少需要3个顶点");
            
            // 转换为 Clipper2 PathD
            var path = new PathD();
            foreach (var vertex in polygon.Vertices)
            {
                path.Add(new PointD(vertex.X, vertex.Y));
            }
            
            // 使用 Clipper2.InflatePaths 进行偏移（完整实现，包含自交处理）
            double offsetDelta = isOutward ? distance : -distance;
            var solution = Clipper.InflatePaths(
                new PathsD { path },
                offsetDelta,
                Clipper2Lib.JoinType.Miter,      // 直角连接（保持原始尖角）
                Clipper2Lib.EndType.Polygon,     // 封闭多边形
                2.0                               // miterLimit（限制尖角长度，防止过度延伸）
            );
            
            // 检查结果
            if (solution == null || solution.Count == 0)
            {
                throw new InvalidOperationException("偏移失败：Clipper2 返回空结果");
            }
            
            // 取第一个路径（通常只有一个）
            var offsetPath = solution[0];
            
            if (offsetPath == null || offsetPath.Count < 3)
            {
                throw new InvalidOperationException($"偏移失败：结果多边形顶点数不足（{offsetPath?.Count ?? 0}）");
            }
            
            // 转换回 Polygon2D
            var offsetPoints = offsetPath.Select(pt => new Point2D(pt.x, pt.y)).ToList();
            
            return new Polygon2D(offsetPoints, polygon.IsClosed);
        }
        
        /// <summary>
        /// 向量归一化
        /// </summary>
        private static Point2D Normalize(Point2D vector)
        {
            double length = System.Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
            
            if (length < 1e-10)
                return new Point2D(0, 0);
            
            return new Point2D(vector.X / length, vector.Y / length);
        }
        
        /// <summary>
        /// 计算多边形面积（带符号）
        /// 正值=逆时针，负值=顺时针
        /// </summary>
        public static double GetSignedArea(this Polygon2D polygon)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));
            
            double area = 0;
            
            for (int i = 0; i < polygon.VertexCount; i++)
            {
                var p1 = polygon.Vertices[i];
                var p2 = polygon.Vertices[(i + 1) % polygon.VertexCount];
                
                area += (p1.X * p2.Y - p2.X * p1.Y);
            }
            
            return area / 2.0;
        }
        
        /// <summary>
        /// 判断多边形方向
        /// </summary>
        public static bool IsCounterClockwise(this Polygon2D polygon)
        {
            return polygon.GetSignedArea() > 0;
        }
        
        /// <summary>
        /// 反转多边形顶点顺序
        /// </summary>
        public static Polygon2D Reverse(this Polygon2D polygon)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));
            
            var reversedVertices = polygon.Vertices.Reverse().ToList();
            return new Polygon2D(reversedVertices, polygon.IsClosed);
        }
    }
}

