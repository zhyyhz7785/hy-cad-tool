using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System;

namespace HyCADTool.Refactored.Domain.Services.OffsetAlgorithms
{
    /// <summary>
    /// 偏移法线计算
    /// 代码逻辑源自 Clipper2 库
    /// Original: Clipper2.ClipperOffset.GetUnitNormal()
    /// License: Boost Software License 1.0
    /// Author: Angus Johnson (Clipper2)
    /// Adapted by: HyCADTool Team
    /// Date: 2025-10-27
    /// </summary>
    internal static class OffsetNormals
    {
        /// <summary>
        /// 获取边的单位法线向量
        /// 参考：Clipper2.GetUnitNormal()
        /// 
        /// 逻辑：
        /// 1. 计算边的方向向量 (dx, dy)
        /// 2. 逆时针旋转90度得到法线 (-dy, dx)
        /// 3. 归一化为单位向量
        /// </summary>
        public static Point2D GetEdgeNormal(Point2D start, Point2D end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            
            // 法线 = 逆时针旋转90度
            Point2D normal = new Point2D(-dy, dx);
            
            return OffsetMath.Normalize(normal);
        }
        
        /// <summary>
        /// 计算角平分线方向
        /// 参考：Clipper2 偏移点计算逻辑
        /// 
        /// 公式（来自 Clipper2）：
        /// bisector = normalize(normal1 + normal2)
        /// factor = distance / sin(angle/2)
        /// sin(angle/2) = sqrt((1 - cos(angle)) / 2)
        /// cos(angle) = dot(edge1_unit, edge2_unit)
        /// </summary>
        public static (Point2D bisector, double factor) CalculateBisector(
            Point2D prev, Point2D curr, Point2D next, double distance)
        {
            // 计算两条边的单位法线
            var normal1 = GetEdgeNormal(prev, curr);
            var normal2 = GetEdgeNormal(curr, next);
            
            // 角平分线 = 两个法线的和（归一化）
            var bisector = new Point2D(normal1.X + normal2.X, normal1.Y + normal2.Y);
            double bisectorLength = Math.Sqrt(bisector.X * bisector.X + bisector.Y * bisector.Y);
            
            // 处理共线情况（180度）
            if (bisectorLength < 1e-10)
            {
                bisector = normal1;
                return (bisector, distance);
            }
            
            // 归一化角平分线
            bisector = new Point2D(bisector.X / bisectorLength, bisector.Y / bisectorLength);
            
            // 计算夹角的余弦值
            var edge1 = new Point2D(curr.X - prev.X, curr.Y - prev.Y);
            var edge2 = new Point2D(next.X - curr.X, next.Y - curr.Y);
            edge1 = OffsetMath.Normalize(edge1);
            edge2 = OffsetMath.Normalize(edge2);
            
            double cosAngle = OffsetMath.DotProduct(edge1, edge2);
            
            // Clipper2 公式：sin(θ/2) = sqrt((1 - cos(θ)) / 2)
            double sinHalfAngle = Math.Sqrt((1.0 - cosAngle) / 2.0);
            
            // 防止除以零
            if (sinHalfAngle < 1e-6)
                sinHalfAngle = 1e-6;
            
            // 实际偏移距离 = distance / sin(半角)
            double factor = distance / sinHalfAngle;
            
            // 限制最大偏移（防止尖角过度偏移）
            double maxFactor = Math.Abs(distance) * 10.0;
            if (Math.Abs(factor) > maxFactor)
                factor = Math.Sign(factor) * maxFactor;
            
            return (bisector, factor);
        }
    }
}












