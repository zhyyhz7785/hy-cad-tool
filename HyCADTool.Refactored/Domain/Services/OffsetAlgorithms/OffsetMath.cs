using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System;

namespace HyCADTool.Refactored.Domain.Services.OffsetAlgorithms
{
    /// <summary>
    /// 偏移算法数学工具类
    /// 代码逻辑源自 Clipper2 库
    /// Original: Clipper2/CPP/Clipper2Lib/src/Clipper.Offset.cpp
    /// License: Boost Software License 1.0
    /// Author: Angus Johnson (Clipper2)
    /// Adapted by: HyCADTool Team
    /// Date: 2025-10-27
    /// </summary>
    internal static class OffsetMath
    {
        /// <summary>
        /// 计算两点之间的距离平方
        /// 参考：Clipper2 内部实现
        /// </summary>
        public static double DistanceSquared(Point2D p1, Point2D p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return dx * dx + dy * dy;
        }
        
        /// <summary>
        /// 归一化向量
        /// 参考：Clipper2.GetUnitNormal()
        /// </summary>
        public static Point2D Normalize(Point2D vector)
        {
            double length = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
            if (length < 1e-10)
                return new Point2D(0, 0);
            return new Point2D(vector.X / length, vector.Y / length);
        }
        
        /// <summary>
        /// 计算点积
        /// 参考：Clipper2 内部向量计算
        /// </summary>
        public static double DotProduct(Point2D v1, Point2D v2)
        {
            return v1.X * v2.X + v1.Y * v2.Y;
        }
        
        /// <summary>
        /// 计算叉积（2D）
        /// 参考：Clipper2 内部实现
        /// </summary>
        public static double CrossProduct(Point2D v1, Point2D v2)
        {
            return v1.X * v2.Y - v1.Y * v2.X;
        }
    }
}












