using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;

namespace HyCADTool.Tools
{
    public static partial class GeometryUtils
    {


        public static bool CheckOverlap(Line a, Line b, Tolerance tol, double tolerance, List<Line> allLines,
    out Point3d newStart, out Point3d newEnd)
        {
            newStart = Point3d.Origin;
            newEnd = Point3d.Origin;

            // 先检查是否共线
            if (!IsCollinear(a, b, tol)) return false;

            Vector3d dirA = a.EndPoint - a.StartPoint;
            double aLength = dirA.Length;
            Vector3d dirB = b.EndPoint - b.StartPoint;
            double bLength = dirB.Length;
            double totalLength = (a.StartPoint - b.EndPoint).Length; // 两线段首尾相连时的总长度

            // 检查是否共线且总长度等于 aL + bL（首尾相连）
            //if (Math.Abs(totalLength - (aLength + bLength)) < tolerance)
            //{
            //    // 找到四个端点中相同的那个点（连接点）
            //    Point3d commonPoint = FindCommonPoint(a, b, tol);
            //    if (!commonPoint.IsEqualTo(Point3d.Origin, tol)) // 确保找到连接点
            //    {
            //        // 与所有直线的起点和终点对比
            //        foreach (Line line in allLines)
            //        {
            //            if (line == a || line == b) continue; // 跳过当前两线段

            //            if (commonPoint.IsEqualTo(line.StartPoint, tol) || commonPoint.IsEqualTo(line.EndPoint, tol))
            //            {
            //                return false; // 有其他直线与连接点相同，跳过重叠处理
            //            }
            //        }
            //    }
            //}

            // 如果没有跳过，继续处理重叠逻辑
            bool isHorizontal = Math.Abs(dirA.Y) < tolerance; // 判断是否水平
            bool isVertical = Math.Abs(dirA.X) < tolerance;   // 判断是否垂直

            if (isHorizontal)
                return CheckHorizontalOverlap(a, b, tolerance, out newStart, out newEnd);
            else if (isVertical)
                return CheckVerticalOverlap(a, b, tolerance, out newStart, out newEnd);
            else
                return CheckDiagonalOverlap(a, b, tolerance, out newStart, out newEnd);
        }

        // 辅助方法：找到两线段的公共端点
        private static Point3d FindCommonPoint(Line a, Line b, Tolerance tol)
        {
            if (a.StartPoint.IsEqualTo(b.StartPoint, tol)) return a.StartPoint;
            if (a.StartPoint.IsEqualTo(b.EndPoint, tol)) return a.StartPoint;
            if (a.EndPoint.IsEqualTo(b.StartPoint, tol)) return a.EndPoint;
            if (a.EndPoint.IsEqualTo(b.EndPoint, tol)) return a.EndPoint;
            return Point3d.Origin; // 未找到公共点
        }

        // 检查两线段是否共线
        public static bool IsCollinear(Line a, Line b, Tolerance tol)
        {
            Vector3d dirA = a.EndPoint - a.StartPoint; // a的方向向量
            Vector3d dirB = b.EndPoint - b.StartPoint; // b的方向向量

            // 检查方向是否平行（叉积接近零）
            if (!dirA.CrossProduct(dirB).IsZeroLength(tol)) return false;

            // 检查点是否在同一直线上（b的起点到a的起点的向量与a的方向向量的叉积接近零）
            return (b.StartPoint - a.StartPoint).CrossProduct(dirA).IsZeroLength(tol);
        }

        // 检查水平线段重叠
        private static bool CheckHorizontalOverlap(Line a, Line b, double tolerance,
            out Point3d newStart, out Point3d newEnd)
        {
            newStart = Point3d.Origin;
            newEnd = Point3d.Origin;

            // 检查Y坐标是否在公差范围内
            if (Math.Abs(a.StartPoint.Y - b.StartPoint.Y) > tolerance) return false;

            // 计算X方向的最小和最大值
            double aMinX = Math.Min(a.StartPoint.X, a.EndPoint.X);
            double aMaxX = Math.Max(a.StartPoint.X, a.EndPoint.X);
            double bMinX = Math.Min(b.StartPoint.X, b.EndPoint.X);
            double bMaxX = Math.Max(b.StartPoint.X, b.EndPoint.X);

            // 检查X方向是否有重叠
            if (aMinX > bMaxX + tolerance || bMinX > aMaxX + tolerance) return false;

            // 计算合并后的范围
            double minX = Math.Min(aMinX, bMinX);
            double maxX = Math.Max(aMaxX, bMaxX);

            newStart = new Point3d(minX, a.StartPoint.Y, 0);
            newEnd = new Point3d(maxX, a.StartPoint.Y, 0);
            return true;
        }

        // 检查垂直线段重叠
        private static bool CheckVerticalOverlap(Line a, Line b, double tolerance,
            out Point3d newStart, out Point3d newEnd)
        {
            newStart = Point3d.Origin;
            newEnd = Point3d.Origin;

            // 检查X坐标是否在公差范围内
            if (Math.Abs(a.StartPoint.X - b.StartPoint.X) > tolerance) return false;

            // 计算Y方向的最小和最大值
            double aMinY = Math.Min(a.StartPoint.Y, a.EndPoint.Y);
            double aMaxY = Math.Max(a.StartPoint.Y, a.EndPoint.Y);
            double bMinY = Math.Min(b.StartPoint.Y, b.EndPoint.Y);
            double bMaxY = Math.Max(b.StartPoint.Y, b.EndPoint.Y);

            // 检查Y方向是否有重叠
            if (aMinY > bMaxY + tolerance || bMinY > aMaxY + tolerance) return false;

            // 计算合并后的范围
            double minY = Math.Min(aMinY, bMinY);
            double maxY = Math.Max(aMaxY, bMaxY);

            newStart = new Point3d(a.StartPoint.X, minY, 0);
            newEnd = new Point3d(a.StartPoint.X, maxY, 0);
            return true;
        }

        // 检查斜线段重叠
        private static bool CheckDiagonalOverlap(Line a, Line b, double tolerance,
            out Point3d newStart, out Point3d newEnd)
        {
            newStart = Point3d.Origin;
            newEnd = Point3d.Origin;

            Vector3d dir = a.EndPoint - a.StartPoint; // a的方向向量
            double[] paramsA = { 0.0, 1.0 }; // a的起点和终点参数化值
            // 将b的起点和终点投影到a的方向上
            double paramBStart = (b.StartPoint - a.StartPoint).DotProduct(dir) / dir.LengthSqrd;
            double paramBEnd = (b.EndPoint - a.StartPoint).DotProduct(dir) / dir.LengthSqrd;

            // 计算参数的最大和最小值
            double minParam = Math.Min(Math.Min(paramsA[0], paramsA[1]), Math.Min(paramBStart, paramBEnd));
            double maxParam = Math.Max(Math.Max(paramsA[0], paramsA[1]), Math.Max(paramBStart, paramBEnd));

            // 检查是否有重叠
            double aMinParam = Math.Min(paramsA[0], paramsA[1]);
            double aMaxParam = Math.Max(paramsA[0], paramsA[1]);
            double bMinParam = Math.Min(paramBStart, paramBEnd);
            double bMaxParam = Math.Max(paramBStart, paramBEnd);
            if (aMinParam > bMaxParam + tolerance || bMinParam > aMaxParam + tolerance) return false;

            // 计算合并后的起点和终点
            newStart = a.StartPoint + dir * minParam;
            newEnd = a.StartPoint + dir * maxParam;
            return true;
        }
        public static bool IsPointInside(this Polyline pline, Point3d point)
        {
            int intersections = 0;
            int nvert = pline.NumberOfVertices;

            for (int i = 0, j = nvert - 1; i < nvert; j = i++)
            {
                Point3d pi = pline.GetPoint3dAt(i);
                Point3d pj = pline.GetPoint3dAt(j);

                if (((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                    (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X))
                {
                    intersections++;
                }
            }
            return (intersections % 2) == 1;
        }

    }
}
