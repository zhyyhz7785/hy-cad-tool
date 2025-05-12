using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Interop.Common;
using HyCADTool.Config;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.Tools
{
    public static partial class EtGpt
    {
        public static List<RotatedDimension> DeleteNearbyParallelDimOptimized(this IEnumerable<RotatedDimension> dims, double angleToleranceDegrees = 3.0, double d = 5000)
        {
            if (dims == null) return new List<RotatedDimension>();
            var horizontalDims = new List<(RotatedDimension Dim, double RefCoord)>(); // RefCoord 是 Y
            var verticalDims = new List<(RotatedDimension Dim, double RefCoord)>();   // RefCoord 是 X
            foreach (var dim in dims)
            {
                var baseLine = new Line(dim.XLine1Point, dim.XLine2Point);
                double angle = NormalizeAngle(baseLine.Angle * (180.0 / Math.PI));
                if (IsApproximatelyHorizontal(angle, angleToleranceDegrees))
                {
                    double refY = (baseLine.StartPoint.Y + baseLine.EndPoint.Y) / 2.0;
                    horizontalDims.Add((dim, refY));
                }
                else if (IsApproximatelyVertical(angle, angleToleranceDegrees))
                {
                    double refX = (baseLine.StartPoint.X + baseLine.EndPoint.X) / 2.0;
                    verticalDims.Add((dim, refX));
                }
            }
            var filteredDims = new List<RotatedDimension>();
            // 水平方向处理（比较Y，按Y排序）
            var groupedHorizontals = horizontalDims.GroupBy(h => RoundCoordinate(h.Dim, Axis.X));
            foreach (var group in groupedHorizontals)
            {
                var ordered = group.OrderBy(h => GetMinX(h.Dim)).ToList(); // 🛠 修正：水平时，按X最小排列
                filteredDims.AddRange(SelectWithThreshold(ordered, Axis.X, d));
            }
            // 垂直方向处理（比较X，按X排序）
            var groupedVerticals = verticalDims.GroupBy(v => RoundCoordinate(v.Dim, Axis.Y));
            foreach (var group in groupedVerticals)
            {
                var ordered = group.OrderBy(v => GetMinY(v.Dim)).ToList(); // 🛠 修正：垂直时，按Y最小排列
                filteredDims.AddRange(SelectWithThreshold(ordered, Axis.Y, d));
            }
            return filteredDims;
        }
        private static IEnumerable<RotatedDimension> SelectWithThreshold(List<(RotatedDimension Dim, double RefCoord)> orderedList, Axis axis, double threshold)
        {
            if (orderedList.Count == 0) yield break;
            double lastCoord = axis == Axis.X ? GetMinX(orderedList[0].Dim) : GetMinY(orderedList[0].Dim);
            yield return orderedList[0].Dim;
            for (int i = 1; i < orderedList.Count; i++)
            {
                double currentCoord = axis == Axis.X ? GetMinX(orderedList[i].Dim) : GetMinY(orderedList[i].Dim);
                if (Math.Abs(currentCoord - lastCoord) > threshold)
                {
                    yield return orderedList[i].Dim;
                    lastCoord = currentCoord;
                }
            }
        }
        private static double GetMinX(RotatedDimension dim)
        {
            return Math.Min(dim.XLine1Point.X, dim.XLine2Point.X);
        }
        private static double GetMinY(RotatedDimension dim)
        {
            return Math.Min(dim.XLine1Point.Y, dim.XLine2Point.Y);
        }
        private static double NormalizeAngle(double angle)
        {
            while (angle >= 360) angle -= 360;
            while (angle < 0) angle += 360;
            return angle;
        }
        private static bool IsApproximatelyHorizontal(double angle, double tolerance)
        {
            return Math.Abs(angle) <= tolerance || Math.Abs(angle - 180) <= tolerance;
        }
        private static bool IsApproximatelyVertical(double angle, double tolerance)
        {
            return Math.Abs(angle - 90) <= tolerance || Math.Abs(angle - 270) <= tolerance;
        }
        private enum Axis
        {
            X,
            Y
        }
        private static string RoundCoordinate(RotatedDimension dim, Axis axis, double tolerance = 1.0)
        {
            var pt1 = dim.XLine1Point;
            var pt2 = dim.XLine2Point;
            double key1 = axis == Axis.X ? pt1.X : pt1.Y;
            double key2 = axis == Axis.X ? pt2.X : pt2.Y;
            long k1 = (long)Math.Round(key1 / tolerance);
            long k2 = (long)Math.Round(key2 / tolerance);
            return $"{Math.Min(k1, k2)}_{Math.Max(k1, k2)}";
        }
    }
}
