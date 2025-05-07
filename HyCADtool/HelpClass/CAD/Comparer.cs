using Autodesk.AutoCAD.Geometry;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
namespace HyCADTool.HelpClass
{
    public class Point3dEqualityComparer : IEqualityComparer<Point3d>
    {
        private readonly double _tolerance;
        public Point3dEqualityComparer(double tolerance = 1e-2)
        {
            _tolerance = tolerance;
        }
        public bool Equals(Point3d p1, Point3d p2)
        {
            return p1.IsEqualTo(p2, new Tolerance(_tolerance, _tolerance));
        }
        public int GetHashCode(Point3d point)
        {
            int hashX = Math.Round(point.X / _tolerance).GetHashCode();
            int hashY = Math.Round(point.Y / _tolerance).GetHashCode();
            int hashZ = Math.Round(point.Z / _tolerance).GetHashCode();
            return hashX ^ hashY ^ hashZ;
        }
    }
    public class Point2dEqualityComparer : IEqualityComparer<Point2d>
    {
        private readonly double _tolerance;
        // 构造函数，允许用户设置容忍度，默认为 1e-2
        public Point2dEqualityComparer(double tolerance = 1e-2)
        {
            _tolerance = tolerance;
        }
        // 判断两个 Point2d 是否相等
        public bool Equals(Point2d p1, Point2d p2)
        {
            // 使用容忍度判断两个点是否相等
            return Math.Abs(p1.X - p2.X) < _tolerance && Math.Abs(p1.Y - p2.Y) < _tolerance;
        }
        // 计算 Point2d 的哈希值
        public int GetHashCode(Point2d point)
        {
            // 对 X 和 Y 坐标进行四舍五入，然后计算哈希值
            int hashX = Math.Round(point.X / _tolerance).GetHashCode();
            int hashY = Math.Round(point.Y / _tolerance).GetHashCode();
            // 使用异或操作组合每个坐标的哈希值
            return hashX ^ hashY;
        }
    }
    public class CoordinateEqualityComparer : IEqualityComparer<Coordinate>
    {
        private readonly double _tolerance;
        public CoordinateEqualityComparer(double tolerance)
        {
            _tolerance = tolerance;
        }
        public bool Equals(Coordinate c1, Coordinate c2)
        {
            return Math.Abs(c1.X - c2.X) < _tolerance && Math.Abs(c1.Y - c2.Y) < _tolerance;
        }
        public int GetHashCode(Coordinate obj)
        {
            return obj.X.GetHashCode() ^ obj.Y.GetHashCode();
        }
    }
}
