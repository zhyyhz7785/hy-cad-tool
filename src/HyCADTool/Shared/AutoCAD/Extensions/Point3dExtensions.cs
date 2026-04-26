using Autodesk.AutoCAD.Geometry;
using System;

namespace HyCADTool.Shared.AutoCAD.Extensions
{
    /// <summary>
    /// Point3d 扩展方法 (Point3d Extension Methods)
    /// 提供 Point3d 常用操作的扩展方法
    /// </summary>
    public static class Point3dExtensions
    {
        #region 距离计算 (Distance Calculations)

        /// <summary>
        /// 计算到另一个点的距离 (Calculate Distance to Another Point)
        /// </summary>
        public static double DistanceTo(this Point3d point, Point3d other)
        {
            return point.DistanceTo(other);
        }

        /// <summary>
        /// 计算到直线的距离 (Calculate Distance to Line)
        /// </summary>
        public static double DistanceToLine(this Point3d point, Point3d lineStart, Point3d lineEnd)
        {
            var line = new Line3d(lineStart, lineEnd);
            return line.GetDistanceTo(point);
        }

        #endregion

        #region 点运算 (Point Operations)

        /// <summary>
        /// 计算两点的中点 (Calculate Midpoint)
        /// </summary>
        public static Point3d MidpointTo(this Point3d point, Point3d other)
        {
            return new Point3d(
                (point.X + other.X) / 2,
                (point.Y + other.Y) / 2,
                (point.Z + other.Z) / 2
            );
        }

        /// <summary>
        /// 偏移点 (Offset Point)
        /// </summary>
        public static Point3d Offset(this Point3d point, double dx, double dy, double dz = 0)
        {
            return new Point3d(point.X + dx, point.Y + dy, point.Z + dz);
        }

        /// <summary>
        /// 偏移点（使用向量） (Offset Point by Vector)
        /// </summary>
        public static Point3d Offset(this Point3d point, Vector3d vector)
        {
            return point + vector;
        }

        #endregion

        #region 几何变换 (Geometric Transformations)

        /// <summary>
        /// 绕指定点旋转 (Rotate Around Point)
        /// </summary>
        /// <param name="point">要旋转的点</param>
        /// <param name="center">旋转中心</param>
        /// <param name="angle">旋转角度（弧度）</param>
        /// <returns>旋转后的点</returns>
        public static Point3d RotateAround(this Point3d point, Point3d center, double angle)
        {
            var matrix = Matrix3d.Rotation(angle, Vector3d.ZAxis, center);
            return point.TransformBy(matrix);
        }

        /// <summary>
        /// 相对于直线镜像 (Mirror About Line)
        /// </summary>
        public static Point3d MirrorAboutLine(this Point3d point, Point3d lineStart, Point3d lineEnd)
        {
            var line = new Line3d(lineStart, lineEnd);
            var plane = new Plane(lineStart, line.Direction);
            var matrix = Matrix3d.Mirroring(plane);
            return point.TransformBy(matrix);
        }

        /// <summary>
        /// 缩放点（相对于基点） (Scale Point Relative to Base Point)
        /// </summary>
        public static Point3d ScaleFrom(this Point3d point, Point3d basePoint, double scale)
        {
            var matrix = Matrix3d.Scaling(scale, basePoint);
            return point.TransformBy(matrix);
        }

        #endregion

        #region 向量运算 (Vector Operations)

        /// <summary>
        /// 计算从该点到另一点的向量 (Calculate Vector to Another Point)
        /// </summary>
        public static Vector3d VectorTo(this Point3d point, Point3d other)
        {
            return other - point;
        }

        /// <summary>
        /// 计算从该点到另一点的方向（单位向量） (Calculate Direction to Another Point)
        /// </summary>
        public static Vector3d DirectionTo(this Point3d point, Point3d other)
        {
            var vector = other - point;
            return vector.GetNormal();
        }

        /// <summary>
        /// 计算从该点到另一点的角度（弧度） (Calculate Angle to Another Point in Radians)
        /// </summary>
        public static double AngleTo(this Point3d point, Point3d other)
        {
            var vector = other - point;
            return Math.Atan2(vector.Y, vector.X);
        }

        #endregion

        #region 类型转换 (Type Conversions)

        /// <summary>
        /// 转换为 Point2d (Convert to Point2d)
        /// </summary>
        public static Point2d ToPoint2d(this Point3d point)
        {
            return new Point2d(point.X, point.Y);
        }

        /// <summary>
        /// 转换为数组 (Convert to Array)
        /// </summary>
        public static double[] ToArray(this Point3d point)
        {
            return new[] { point.X, point.Y, point.Z };
        }

        #endregion

        #region 比较与判断 (Comparison and Checking)

        /// <summary>
        /// 判断点是否在指定容差范围内相等 (Check if Points Are Equal Within Tolerance)
        /// </summary>
        public static bool IsEqualTo(this Point3d point, Point3d other, double tolerance)
        {
            return point.DistanceTo(other) <= tolerance;
        }

        /// <summary>
        /// 判断点是否在两点之间 (Check if Point is Between Two Points)
        /// </summary>
        public static bool IsBetween(this Point3d point, Point3d start, Point3d end, double tolerance = 0.001)
        {
            var dist1 = point.DistanceTo(start);
            var dist2 = point.DistanceTo(end);
            var totalDist = start.DistanceTo(end);
            
            return Math.Abs(dist1 + dist2 - totalDist) <= tolerance;
        }

        /// <summary>
        /// 判断点是否在原点 (Check if Point is at Origin)
        /// </summary>
        public static bool IsOrigin(this Point3d point, double tolerance = 1e-10)
        {
            return point.DistanceTo(Point3d.Origin) <= tolerance;
        }

        #endregion

        #region 投影与最近点 (Projection and Closest Point)

        /// <summary>
        /// 投影到 XY 平面 (Project to XY Plane)
        /// </summary>
        public static Point3d ProjectToXY(this Point3d point)
        {
            return new Point3d(point.X, point.Y, 0);
        }

        /// <summary>
        /// 投影到 XZ 平面 (Project to XZ Plane)
        /// </summary>
        public static Point3d ProjectToXZ(this Point3d point)
        {
            return new Point3d(point.X, 0, point.Z);
        }

        /// <summary>
        /// 投影到 YZ 平面 (Project to YZ Plane)
        /// </summary>
        public static Point3d ProjectToYZ(this Point3d point)
        {
            return new Point3d(0, point.Y, point.Z);
        }

        /// <summary>
        /// 获取在直线上的最近点 (Get Closest Point on Line)
        /// </summary>
        public static Point3d GetClosestPointOnLine(this Point3d point, Point3d lineStart, Point3d lineEnd)
        {
            var line = new Line3d(lineStart, lineEnd);
            return line.GetClosestPointTo(point).Point;
        }

        #endregion

        #region 格式化输出 (Formatted Output)

        /// <summary>
        /// 转换为格式化字符串 (Convert to Formatted String)
        /// </summary>
        public static string ToFormattedString(this Point3d point, int decimals = 3)
        {
            var format = $"F{decimals}";
            return $"({point.X.ToString(format)}, {point.Y.ToString(format)}, {point.Z.ToString(format)})";
        }

        #endregion
    }
}

