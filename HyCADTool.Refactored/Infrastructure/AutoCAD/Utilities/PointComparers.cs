using Autodesk.AutoCAD.Geometry;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Utilities
{
    /// <summary>
    /// Point3d 相等性比较器（Point3d Equality Comparer）
    /// 用于基于容差的点比较
    /// </summary>
    public class Point3dEqualityComparer : IEqualityComparer<Point3d>
    {
        private readonly double _tolerance;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="tolerance">容差（Tolerance），默认 0.01</param>
        public Point3dEqualityComparer(double tolerance = 1e-2)
        {
            _tolerance = tolerance;
        }

        /// <summary>
        /// 判断两个 Point3d 是否相等（在容差范围内）
        /// </summary>
        public bool Equals(Point3d p1, Point3d p2)
        {
            return p1.IsEqualTo(p2, new Tolerance(_tolerance, _tolerance));
        }

        /// <summary>
        /// 计算 Point3d 的哈希码
        /// </summary>
        public int GetHashCode(Point3d point)
        {
            int hashX = Math.Round(point.X / _tolerance).GetHashCode();
            int hashY = Math.Round(point.Y / _tolerance).GetHashCode();
            int hashZ = Math.Round(point.Z / _tolerance).GetHashCode();
            return hashX ^ hashY ^ hashZ;
        }
    }

    /// <summary>
    /// Point2d 相等性比较器（Point2d Equality Comparer）
    /// 用于基于容差的 2D 点比较
    /// </summary>
    public class Point2dEqualityComparer : IEqualityComparer<Point2d>
    {
        private readonly double _tolerance;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="tolerance">容差（Tolerance），默认 0.01</param>
        public Point2dEqualityComparer(double tolerance = 1e-2)
        {
            _tolerance = tolerance;
        }

        /// <summary>
        /// 判断两个 Point2d 是否相等（在容差范围内）
        /// </summary>
        public bool Equals(Point2d p1, Point2d p2)
        {
            return Math.Abs(p1.X - p2.X) < _tolerance && Math.Abs(p1.Y - p2.Y) < _tolerance;
        }

        /// <summary>
        /// 计算 Point2d 的哈希码
        /// </summary>
        public int GetHashCode(Point2d point)
        {
            int hashX = Math.Round(point.X / _tolerance).GetHashCode();
            int hashY = Math.Round(point.Y / _tolerance).GetHashCode();
            return hashX ^ hashY;
        }
    }

    /// <summary>
    /// Point3d 比较器（基于 2D 容差，用于聚类等场景）
    /// 从 Domain.Models.Cluster 迁移而来
    /// </summary>
    public class Point3dComparer : IEqualityComparer<Point3d>
    {
        private readonly double _tolerance;

        public Point3dComparer(double tolerance)
        {
            _tolerance = tolerance;
        }

        public bool Equals(Point3d p1, Point3d p2)
        {
            return Math.Abs(p1.X - p2.X) <= _tolerance
                && Math.Abs(p1.Y - p2.Y) <= _tolerance;
        }

        public int GetHashCode(Point3d p)
        {
            int hx = (int)(p.X / _tolerance);
            int hy = (int)(p.Y / _tolerance);
            return hx * 397 ^ hy;
        }
    }

    /// <summary>
    /// Coordinate 相等性比较器（用于 NetTopologySuite）
    /// </summary>
    public class CoordinateEqualityComparer : IEqualityComparer<Coordinate>
    {
        private readonly double _tolerance;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="tolerance">容差（Tolerance）</param>
        public CoordinateEqualityComparer(double tolerance)
        {
            _tolerance = tolerance;
        }

        /// <summary>
        /// 判断两个 Coordinate 是否相等（在容差范围内）
        /// </summary>
        public bool Equals(Coordinate c1, Coordinate c2)
        {
            return Math.Abs(c1.X - c2.X) < _tolerance && Math.Abs(c1.Y - c2.Y) < _tolerance;
        }

        /// <summary>
        /// 计算 Coordinate 的哈希码
        /// </summary>
        public int GetHashCode(Coordinate obj)
        {
            return obj.X.GetHashCode() ^ obj.Y.GetHashCode();
        }
    }
}

