using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Shared.Geometry.Algorithms
{
    /// <summary>
    /// 几何计算工具类 (性能优化版)
    /// 提供平台无关的几何计算方法
    /// 所有方法使用 MethodImpl(AggressiveInlining) 优化
    /// </summary>
    public static class GeometryUtils
    {
        /// <summary>
        /// 判断点是否在多边形内部（射线法）
        /// </summary>
        /// <param name="point">要检查的点</param>
        /// <param name="polygon">多边形</param>
        /// <returns>true 表示点在多边形内部</returns>
        public static bool IsPointInsidePolygon(Point2D point, Polygon2D polygon)
        {
            if (!polygon.IsClosed)
                return false;
            
            int crossings = 0;
            var vertices = polygon.Vertices;
            
            for (int i = 0; i < vertices.Count; i++)
            {
                var p1 = vertices[i];
                var p2 = vertices[(i + 1) % vertices.Count];
                
                // 射线法：从点向右发射水平射线，计算与多边形边的交点数
                if (((p1.Y > point.Y) != (p2.Y > point.Y)) &&
                    (point.X < (p2.X - p1.X) * (point.Y - p1.Y) / (p2.Y - p1.Y) + p1.X))
                {
                    crossings++;
                }
            }
            
            // 奇数个交点 = 在内部
            return (crossings % 2) == 1;
        }
        
        /// <summary>
        /// 判断两个点是否接近（在容差范围内）
        /// 性能优化：避免 Sqrt，使用平方距离比较
        /// </summary>
        /// <param name="p1">第一个点</param>
        /// <param name="p2">第二个点</param>
        /// <param name="tolerance">容差（默认 1.0mm）</param>
        /// <returns>true 表示两点接近</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ArePointsClose(Point2D p1, Point2D p2, double tolerance = 1.0)
        {
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            double distSq = dx * dx + dy * dy;
            double toleranceSq = tolerance * tolerance;
            return distSq < toleranceSq;
        }
        
        /// <summary>
        /// 判断两条边是否相等（考虑方向相反）
        /// 性能优化：短路评估，优先判断更可能失败的条件
        /// </summary>
        /// <param name="edge1">第一条边</param>
        /// <param name="edge2">第二条边</param>
        /// <param name="tolerance">容差（默认 1.0mm）</param>
        /// <returns>true 表示两边相等</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreEdgesEqual(Line2D edge1, Line2D edge2, double tolerance = 1.0)
        {
            // 性能优化：预计算容差平方，避免重复计算
            double toleranceSq = tolerance * tolerance;
            
            // 方向1：起点-起点，终点-终点
            double dx1 = edge1.StartPoint.X - edge2.StartPoint.X;
            double dy1 = edge1.StartPoint.Y - edge2.StartPoint.Y;
            if (dx1 * dx1 + dy1 * dy1 < toleranceSq)
            {
                double dx2 = edge1.EndPoint.X - edge2.EndPoint.X;
                double dy2 = edge1.EndPoint.Y - edge2.EndPoint.Y;
                if (dx2 * dx2 + dy2 * dy2 < toleranceSq)
                    return true;
            }
            
            // 方向2：起点-终点，终点-起点
            dx1 = edge1.StartPoint.X - edge2.EndPoint.X;
            dy1 = edge1.StartPoint.Y - edge2.EndPoint.Y;
            if (dx1 * dx1 + dy1 * dy1 < toleranceSq)
            {
                double dx2 = edge1.EndPoint.X - edge2.StartPoint.X;
                double dy2 = edge1.EndPoint.Y - edge2.StartPoint.Y;
                if (dx2 * dx2 + dy2 * dy2 < toleranceSq)
                    return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 计算多边形的有向面积（用于判断绕向）
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <returns>有向面积（正值=逆时针，负值=顺时针）</returns>
        public static double GetSignedArea(Polygon2D polygon)
        {
            double signedArea = 0;
            var vertices = polygon.Vertices;
            
            for (int i = 0; i < vertices.Count; i++)
            {
                var p1 = vertices[i];
                var p2 = vertices[(i + 1) % vertices.Count];
                signedArea += (p2.X - p1.X) * (p2.Y + p1.Y);
            }
            
            return signedArea;
        }
        
        /// <summary>
        /// 判断多边形是否逆时针
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <returns>true 表示逆时针</returns>
        public static bool IsCounterClockwise(Polygon2D polygon)
        {
            return GetSignedArea(polygon) > 0;
        }
        
        /// <summary>
        /// 计算边的外侧法向量
        /// </summary>
        /// <param name="edge">边</param>
        /// <param name="polygon">边所属的多边形</param>
        /// <returns>外侧法向量（单位向量）</returns>
        public static Vector2D CalculateOutwardNormal(Line2D edge, Polygon2D polygon)
        {
            // 边的方向向量
            var edgeVec = new Vector2D(
                edge.EndPoint.X - edge.StartPoint.X, 
                edge.EndPoint.Y - edge.StartPoint.Y);
            
            double length = System.Math.Sqrt(edgeVec.X * edgeVec.X + edgeVec.Y * edgeVec.Y);
            
            if (length < 1e-10)
                return new Vector2D(0, 0);
            
            var unitDir = new Vector2D(edgeVec.X / length, edgeVec.Y / length);
            
            // 左侧法向量（逆时针90度）
            var leftNormal = new Vector2D(-unitDir.Y, unitDir.X);
            
            // 判断多边形绕向
            bool isCCW = IsCounterClockwise(polygon);
            
            // 外侧法向量
            return isCCW ? leftNormal : new Vector2D(-leftNormal.X, -leftNormal.Y);
        }
        
        /// <summary>
        /// 计算向量的长度
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double VectorLength(Vector2D vector)
        {
            return System.Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
        }
        
        /// <summary>
        /// 计算向量的长度平方（避免 Sqrt，性能更高）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double VectorLengthSquared(Vector2D vector)
        {
            return vector.X * vector.X + vector.Y * vector.Y;
        }
        
        /// <summary>
        /// 归一化向量
        /// 性能优化：避免重复计算长度
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2D Normalize(Vector2D vector)
        {
            double lengthSq = vector.X * vector.X + vector.Y * vector.Y;
            if (lengthSq < 1e-20) // 使用平方比较
                return new Vector2D(0, 0);
            
            double invLength = 1.0 / System.Math.Sqrt(lengthSq);
            return new Vector2D(vector.X * invLength, vector.Y * invLength);
        }
        
        /// <summary>
        /// 计算两点之间的距离
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Distance(Point2D p1, Point2D p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return System.Math.Sqrt(dx * dx + dy * dy);
        }
        
        /// <summary>
        /// 计算两点之间的距离平方（避免 Sqrt，性能更高）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double DistanceSquared(Point2D p1, Point2D p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return dx * dx + dy * dy;
        }
    }
}

