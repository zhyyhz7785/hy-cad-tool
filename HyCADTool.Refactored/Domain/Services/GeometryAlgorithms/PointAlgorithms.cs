using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System;

namespace HyCADTool.Refactored.Domain.Services.GeometryAlgorithms
{
    /// <summary>
    /// 点算法服务 - 纯数学计算，平台无关
    /// Point Algorithm Service - Pure mathematical calculations, platform-independent
    /// </summary>
    public static class PointAlgorithms
    {
        /// <summary>
        /// 计算两点距离
        /// Calculate distance between two points
        /// </summary>
        /// <param name="p1">第一个点 First point</param>
        /// <param name="p2">第二个点 Second point</param>
        /// <returns>距离 Distance</returns>
        public static double Distance(Point2D p1, Point2D p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 计算中点
        /// Calculate midpoint
        /// </summary>
        /// <param name="p1">第一个点 First point</param>
        /// <param name="p2">第二个点 Second point</param>
        /// <returns>中点 Midpoint</returns>
        public static Point2D Midpoint(Point2D p1, Point2D p2)
        {
            return new Point2D(
                (p1.X + p2.X) / 2,
                (p1.Y + p2.Y) / 2
            );
        }

        /// <summary>
        /// 线性插值
        /// Linear interpolation
        /// </summary>
        /// <param name="p1">起点 Start point</param>
        /// <param name="p2">终点 End point</param>
        /// <param name="t">插值参数 [0,1] Interpolation parameter [0,1]</param>
        /// <returns>插值点 Interpolated point</returns>
        public static Point2D Lerp(Point2D p1, Point2D p2, double t)
        {
            return new Point2D(
                p1.X + (p2.X - p1.X) * t,
                p1.Y + (p2.Y - p1.Y) * t
            );
        }

        /// <summary>
        /// 点到直线的距离
        /// Distance from point to line
        /// </summary>
        /// <param name="point">点 Point</param>
        /// <param name="line">直线 Line</param>
        /// <returns>距离 Distance</returns>
        public static double DistanceToLine(Point2D point, Line2D line)
        {
            // 使用公式: |ax + by + c| / sqrt(a^2 + b^2)
            // Using formula: |ax + by + c| / sqrt(a^2 + b^2)
            double dx = line.EndPoint.X - line.StartPoint.X;
            double dy = line.EndPoint.Y - line.StartPoint.Y;
            
            double numerator = Math.Abs(
                dy * point.X - dx * point.Y + 
                line.EndPoint.X * line.StartPoint.Y - 
                line.EndPoint.Y * line.StartPoint.X
            );
            
            double denominator = Math.Sqrt(dx * dx + dy * dy);
            
            if (denominator < 1e-10)
                return Distance(point, line.StartPoint);
            
            return numerator / denominator;
        }

        /// <summary>
        /// 点在直线上的投影
        /// Project point onto line
        /// </summary>
        /// <param name="point">点 Point</param>
        /// <param name="line">直线 Line</param>
        /// <returns>投影点 Projected point</returns>
        public static Point2D ProjectToLine(Point2D point, Line2D line)
        {
            double dx = line.EndPoint.X - line.StartPoint.X;
            double dy = line.EndPoint.Y - line.StartPoint.Y;
            
            double lineLengthSquared = dx * dx + dy * dy;
            
            if (lineLengthSquared < 1e-10)
                return line.StartPoint;
            
            double t = ((point.X - line.StartPoint.X) * dx + 
                       (point.Y - line.StartPoint.Y) * dy) / lineLengthSquared;
            
            return new Point2D(
                line.StartPoint.X + t * dx,
                line.StartPoint.Y + t * dy
            );
        }

        /// <summary>
        /// 旋转点（绕原点）
        /// Rotate point around origin
        /// </summary>
        /// <param name="point">点 Point</param>
        /// <param name="angle">角度（弧度）Angle in radians</param>
        /// <returns>旋转后的点 Rotated point</returns>
        public static Point2D Rotate(Point2D point, double angle)
        {
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            
            return new Point2D(
                point.X * cos - point.Y * sin,
                point.X * sin + point.Y * cos
            );
        }

        /// <summary>
        /// 旋转点（绕指定中心）
        /// Rotate point around specified center
        /// </summary>
        /// <param name="point">点 Point</param>
        /// <param name="center">旋转中心 Rotation center</param>
        /// <param name="angle">角度（弧度）Angle in radians</param>
        /// <returns>旋转后的点 Rotated point</returns>
        public static Point2D RotateAround(Point2D point, Point2D center, double angle)
        {
            // 平移到原点，旋转，再平移回去
            // Translate to origin, rotate, translate back
            var translated = new Point2D(point.X - center.X, point.Y - center.Y);
            var rotated = Rotate(translated, angle);
            return new Point2D(rotated.X + center.X, rotated.Y + center.Y);
        }

        /// <summary>
        /// 缩放点（相对于原点）
        /// Scale point relative to origin
        /// </summary>
        /// <param name="point">点 Point</param>
        /// <param name="scaleX">X 方向缩放比例 X scale factor</param>
        /// <param name="scaleY">Y 方向缩放比例 Y scale factor</param>
        /// <returns>缩放后的点 Scaled point</returns>
        public static Point2D Scale(Point2D point, double scaleX, double scaleY)
        {
            return new Point2D(point.X * scaleX, point.Y * scaleY);
        }

        /// <summary>
        /// 缩放点（相对于指定中心）
        /// Scale point relative to specified center
        /// </summary>
        /// <param name="point">点 Point</param>
        /// <param name="center">缩放中心 Scale center</param>
        /// <param name="scaleX">X 方向缩放比例 X scale factor</param>
        /// <param name="scaleY">Y 方向缩放比例 Y scale factor</param>
        /// <returns>缩放后的点 Scaled point</returns>
        public static Point2D ScaleAround(Point2D point, Point2D center, double scaleX, double scaleY)
        {
            var translated = new Point2D(point.X - center.X, point.Y - center.Y);
            var scaled = Scale(translated, scaleX, scaleY);
            return new Point2D(scaled.X + center.X, scaled.Y + center.Y);
        }

        /// <summary>
        /// 镜像点（相对于 X 轴）
        /// Mirror point relative to X axis
        /// </summary>
        /// <param name="point">点 Point</param>
        /// <returns>镜像后的点 Mirrored point</returns>
        public static Point2D MirrorX(Point2D point)
        {
            return new Point2D(point.X, -point.Y);
        }

        /// <summary>
        /// 镜像点（相对于 Y 轴）
        /// Mirror point relative to Y axis
        /// </summary>
        /// <param name="point">点 Point</param>
        /// <returns>镜像后的点 Mirrored point</returns>
        public static Point2D MirrorY(Point2D point)
        {
            return new Point2D(-point.X, point.Y);
        }

        /// <summary>
        /// 镜像点（相对于直线）
        /// Mirror point relative to line
        /// </summary>
        /// <param name="point">点 Point</param>
        /// <param name="line">镜像线 Mirror line</param>
        /// <returns>镜像后的点 Mirrored point</returns>
        public static Point2D MirrorByLine(Point2D point, Line2D line)
        {
            // 计算投影点
            var projection = ProjectToLine(point, line);
            
            // 点关于投影点的对称点
            return new Point2D(
                2 * projection.X - point.X,
                2 * projection.Y - point.Y
            );
        }

        /// <summary>
        /// 判断两点是否相等（在指定容差内）
        /// Check if two points are equal within tolerance
        /// </summary>
        /// <param name="p1">第一个点 First point</param>
        /// <param name="p2">第二个点 Second point</param>
        /// <param name="tolerance">容差 Tolerance</param>
        /// <returns>是否相等 True if equal</returns>
        public static bool AreEqual(Point2D p1, Point2D p2, double tolerance = 1e-10)
        {
            return Math.Abs(p1.X - p2.X) < tolerance && 
                   Math.Abs(p1.Y - p2.Y) < tolerance;
        }

        /// <summary>
        /// 计算点到点的向量
        /// Calculate vector from point to point
        /// </summary>
        /// <param name="from">起点 From point</param>
        /// <param name="to">终点 To point</param>
        /// <returns>向量 Vector</returns>
        public static Vector2D VectorFromTo(Point2D from, Point2D to)
        {
            return new Vector2D(to.X - from.X, to.Y - from.Y);
        }

        /// <summary>
        /// 偏移点（沿指定向量）
        /// Offset point along vector
        /// </summary>
        /// <param name="point">点 Point</param>
        /// <param name="vector">偏移向量 Offset vector</param>
        /// <returns>偏移后的点 Offset point</returns>
        public static Point2D Offset(Point2D point, Vector2D vector)
        {
            return new Point2D(point.X + vector.X, point.Y + vector.Y);
        }

        /// <summary>
        /// 计算点的极坐标角度（相对于原点）
        /// Calculate polar angle of point relative to origin
        /// </summary>
        /// <param name="point">点 Point</param>
        /// <returns>角度（弧度）[-π, π] Angle in radians [-π, π]</returns>
        public static double PolarAngle(Point2D point)
        {
            return Math.Atan2(point.Y, point.X);
        }

        /// <summary>
        /// 计算点的极坐标角度（相对于指定中心）
        /// Calculate polar angle of point relative to center
        /// </summary>
        /// <param name="point">点 Point</param>
        /// <param name="center">中心 Center</param>
        /// <returns>角度（弧度）[-π, π] Angle in radians [-π, π]</returns>
        public static double PolarAngleFrom(Point2D point, Point2D center)
        {
            return Math.Atan2(point.Y - center.Y, point.X - center.X);
        }
    }
}

