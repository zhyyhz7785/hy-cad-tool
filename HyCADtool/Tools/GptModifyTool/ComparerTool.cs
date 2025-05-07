using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
namespace HyCADTool.Tools
{
    public static partial class EtGpt
    {
        /// <summary>
        /// 自定义的Point3d比较器，用于在HashSet中比较点坐标的相等性，考虑误差范围
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
                return Math.Abs(p1.X - p2.X) < _tolerance &&
                       Math.Abs(p1.Y - p2.Y) < _tolerance &&
                       Math.Abs(p1.Z - p2.Z) < _tolerance;
            }
            public int GetHashCode(Point3d point)
            {
                return point.X.GetHashCode() ^
                       point.Y.GetHashCode() ^
                       point.Z.GetHashCode();
            }
        }
    }
}
