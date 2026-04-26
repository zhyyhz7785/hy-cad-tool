using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Shared.AutoCAD.Extensions
{
    /// <summary>
    /// Polyline 扩展方法 (Polyline Extension Methods)
    /// 提供 Polyline 常用操作的扩展方法
    /// </summary>
    public static class PolylineExtensions
    {
        #region 顶点操作 (Vertex Operations)

        /// <summary>
        /// 获取所有 3D 顶点 (Get All 3D Vertices)
        /// </summary>
        public static Point3d[] GetAllVertices(this Polyline poly)
        {
            var vertices = new Point3d[poly.NumberOfVertices];
            for (int i = 0; i < poly.NumberOfVertices; i++)
            {
                vertices[i] = poly.GetPoint3dAt(i);
            }
            return vertices;
        }

        /// <summary>
        /// 获取所有 2D 顶点 (Get All 2D Vertices)
        /// </summary>
        public static Point2d[] GetAllVertices2d(this Polyline poly)
        {
            var vertices = new Point2d[poly.NumberOfVertices];
            for (int i = 0; i < poly.NumberOfVertices; i++)
            {
                vertices[i] = poly.GetPoint2dAt(i);
            }
            return vertices;
        }

        #endregion

        #region 线段操作 (Segment Operations)

        /// <summary>
        /// 转换为 Line 数组 (Convert to Line Array)
        /// </summary>
        public static Line[] ToLines(this Polyline poly)
        {
            var lines = new List<Line>();
            int segments = poly.Closed ? poly.NumberOfVertices : poly.NumberOfVertices - 1;
            
            for (int i = 0; i < segments; i++)
            {
                var seg = poly.GetLineSegment2dAt(i);
                var elevation = poly.Elevation;
                lines.Add(new Line(
                    new Point3d(seg.StartPoint.X, seg.StartPoint.Y, elevation),
                    new Point3d(seg.EndPoint.X, seg.EndPoint.Y, elevation)
                ));
            }
            
            return lines.ToArray();
        }

        /// <summary>
        /// 获取所有线段角度（弧度） (Get All Segment Angles in Radians)
        /// </summary>
        public static double[] GetSegmentAngles(this Polyline poly)
        {
            int segments = poly.Closed ? poly.NumberOfVertices : poly.NumberOfVertices - 1;
            var angles = new double[segments];
            
            for (int i = 0; i < segments; i++)
            {
                var seg = poly.GetLineSegment2dAt(i);
                angles[i] = seg.Direction.Angle;
            }
            
            return angles;
        }

        /// <summary>
        /// 获取所有线段长度 (Get All Segment Lengths)
        /// </summary>
        public static double[] GetSegmentLengths(this Polyline poly)
        {
            int segments = poly.Closed ? poly.NumberOfVertices : poly.NumberOfVertices - 1;
            var lengths = new double[segments];
            
            for (int i = 0; i < segments; i++)
            {
                var seg = poly.GetLineSegment2dAt(i);
                lengths[i] = seg.Length;
            }
            
            return lengths;
        }

        /// <summary>
        /// 获取指定索引的线段 (Get Line Segment at Index)
        /// </summary>
        public static Line GetLineAt(this Polyline poly, int index)
        {
            if (index < 0 || index >= poly.NumberOfVertices - (!poly.Closed ? 1 : 0))
                throw new ArgumentOutOfRangeException(nameof(index));

            var seg = poly.GetLineSegment2dAt(index);
            var elevation = poly.Elevation;
            
            return new Line(
                new Point3d(seg.StartPoint.X, seg.StartPoint.Y, elevation),
                new Point3d(seg.EndPoint.X, seg.EndPoint.Y, elevation)
            );
        }

        #endregion

        #region 几何属性 (Geometric Properties)

        /// <summary>
        /// 获取面积（多段线必须闭合） (Get Area - Polyline Must Be Closed)
        /// </summary>
        public static double GetArea(this Polyline poly)
        {
            if (!poly.Closed)
                return 0;
            return Math.Abs(poly.Area);
        }

        /// <summary>
        /// 获取周长 (Get Perimeter)
        /// </summary>
        public static double GetPerimeter(this Polyline poly)
        {
            return poly.Length;
        }

        /// <summary>
        /// 获取质心（中心点） (Get Centroid)
        /// </summary>
        public static Point3d GetCentroid(this Polyline poly)
        {
            if (!poly.Closed || poly.NumberOfVertices < 3)
            {
                // 如果不闭合，返回所有顶点的平均值
                var vertices = poly.GetAllVertices();
                double sumX = 0, sumY = 0, sumZ = 0;
                foreach (var v in vertices)
                {
                    sumX += v.X;
                    sumY += v.Y;
                    sumZ += v.Z;
                }
                return new Point3d(
                    sumX / vertices.Length,
                    sumY / vertices.Length,
                    sumZ / vertices.Length
                );
            }

            // 使用几何中心
            var extents = poly.GeometricExtents;
            return new Point3d(
                (extents.MinPoint.X + extents.MaxPoint.X) / 2,
                (extents.MinPoint.Y + extents.MaxPoint.Y) / 2,
                (extents.MinPoint.Z + extents.MaxPoint.Z) / 2
            );
        }

        #endregion

        #region 几何变换 (Geometric Transformations)

        /// <summary>
        /// 偏移多段线 (Offset Polyline)
        /// </summary>
        /// <param name="poly">多段线</param>
        /// <param name="distance">偏移距离（正值向外，负值向内）</param>
        /// <returns>偏移后的多段线数组</returns>
        public static Polyline[] Offset(this Polyline poly, double distance)
        {
            try
            {
                var offsetCurves = poly.GetOffsetCurves(distance);
                var result = new List<Polyline>();
                
                foreach (DBObject obj in offsetCurves)
                {
                    if (obj is Polyline offsetPoly)
                    {
                        result.Add(offsetPoly);
                    }
                }
                
                offsetCurves.Dispose();
                return result.ToArray();
            }
            catch
            {
                return new Polyline[0];
            }
        }

        /// <summary>
        /// 反转多段线方向 (Reverse Polyline Direction)
        /// </summary>
        public static Polyline Reverse(this Polyline poly)
        {
            var newPoly = poly.Clone() as Polyline;
            newPoly.ReverseCurve();
            return newPoly;
        }

        #endregion

        #region 点与多段线关系 (Point-Polyline Relationship)

        /// <summary>
        /// 检查点是否在多段线内（多段线必须闭合） (Check if Point Inside - Polyline Must Be Closed)
        /// 使用射线法判断
        /// </summary>
        public static bool ContainsPoint(this Polyline poly, Point3d point, double tolerance = 0.001)
        {
            if (!poly.Closed)
                return false;

            var point2d = new Point2d(point.X, point.Y);
            int intersections = 0;
            
            // 使用射线法：从点向右发射一条射线，计算与多边形边的交点数
            for (int i = 0; i < poly.NumberOfVertices; i++)
            {
                var p1 = poly.GetPoint2dAt(i);
                var p2 = poly.GetPoint2dAt((i + 1) % poly.NumberOfVertices);
                
                // 检查射线与边是否相交
                if (RayIntersectsSegment(point2d, p1, p2))
                {
                    intersections++;
                }
            }
            
            // 奇数个交点表示点在多边形内
            return (intersections % 2) == 1;
        }

        /// <summary>
        /// 检查点是否在多段线边界上 (Check if Point on Polyline Boundary)
        /// </summary>
        public static bool IsPointOnBoundary(this Polyline poly, Point3d point, double tolerance = 0.001)
        {
            var point2d = new Point2d(point.X, point.Y);
            
            for (int i = 0; i < poly.NumberOfVertices; i++)
            {
                int nextIndex = (i + 1) % poly.NumberOfVertices;
                if (nextIndex == 0 && !poly.Closed)
                    break;

                var seg = poly.GetLineSegment2dAt(i);
                var closestPoint = seg.GetClosestPointTo(point2d);
                var distance = closestPoint.Point.GetDistanceTo(point2d);
                
                if (distance <= tolerance)
                {
                    // 检查最近点是否在线段上
                    var param = seg.GetParameterOf(closestPoint.Point);
                    if (param >= 0 && param <= 1)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }

        #endregion

        #region 辅助方法 (Helper Methods)

        /// <summary>
        /// 射线与线段相交判断（射线法内部使用）
        /// </summary>
        private static bool RayIntersectsSegment(Point2d point, Point2d p1, Point2d p2)
        {
            // 射线从点向右（X正方向）发射
            // 检查线段是否与射线相交
            
            if (p1.Y == p2.Y) // 水平线段
                return false;
            
            if (point.Y < Math.Min(p1.Y, p2.Y) || point.Y >= Math.Max(p1.Y, p2.Y))
                return false;
            
            // 计算交点的X坐标
            double xIntersect = p1.X + (point.Y - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y);
            
            return xIntersect >= point.X;
        }

        #endregion

        #region 判断与转换 (Checking and Conversion)

        /// <summary>
        /// 检查是否为矩形 (Check if Rectangle)
        /// </summary>
        public static bool IsRectangle(this Polyline poly, double tolerance = 0.001)
        {
            if (!poly.Closed || poly.NumberOfVertices != 4)
                return false;

            var angles = poly.GetSegmentAngles();
            
            // 检查是否有四个直角
            for (int i = 0; i < 4; i++)
            {
                double angle1 = angles[i];
                double angle2 = angles[(i + 1) % 4];
                double diff = Math.Abs(angle2 - angle1);
                
                // 归一化到 [0, 2π]
                while (diff > Math.PI * 2) diff -= Math.PI * 2;
                while (diff < 0) diff += Math.PI * 2;
                
                // 检查是否接近 90度 (π/2)
                if (Math.Abs(diff - Math.PI / 2) > tolerance && Math.Abs(diff - Math.PI * 3 / 2) > tolerance)
                {
                    return false;
                }
            }
            
            return true;
        }

        /// <summary>
        /// 获取包围盒的四个角点 (Get Bounding Box Corner Points)
        /// </summary>
        public static Point3d[] GetBoundingBoxCorners(this Polyline poly)
        {
            var extents = poly.GeometricExtents;
            var min = extents.MinPoint;
            var max = extents.MaxPoint;
            
            return new[]
            {
                new Point3d(min.X, min.Y, min.Z),
                new Point3d(max.X, min.Y, min.Z),
                new Point3d(max.X, max.Y, max.Z),
                new Point3d(min.X, max.Y, max.Z)
            };
        }

        #endregion

        #region 阶段 6: 几何工具整合 - Polyline 操作增强

        /// <summary>
        /// 移除多段线重复顶点（通常是首尾重复）
        /// </summary>
        /// <param name="poly">多段线对象</param>
        /// <param name="tolerance">判断重复的容差</param>
        /// <returns>去重后的多段线（原对象）</returns>
        public static Polyline RemoveDuplicateVertices(
            this Polyline poly,
            double tolerance = 1e-6)
        {
            if (poly == null || poly.NumberOfVertices < 2)
                return poly;

            var first = poly.GetPoint3dAt(0);
            var last = poly.GetPoint3dAt(poly.NumberOfVertices - 1);
            var tol = new Tolerance(tolerance, tolerance);

            if (first.IsEqualTo(last, tol))
            {
                poly.RemoveVertexAt(poly.NumberOfVertices - 1);
            }

            return poly;
        }

        /// <summary>
        /// 确保多段线为顺时针方向（负面积，AutoCAD 坐标系下）
        /// </summary>
        /// <param name="poly">多段线对象</param>
        /// <returns>顺时针的多段线（原对象）</returns>
        public static Polyline EnsureClockwise(this Polyline poly)
        {
            if (poly == null || !poly.Closed)
                return poly;

            // 使用代数面积判断方向
            double area = poly.GetAlgebraicArea();
            if (area > 0) // 逆时针（正面积）
            {
                poly.ReverseCurve();
            }

            return poly;
        }

        /// <summary>
        /// 确保多段线为逆时针方向（正面积，AutoCAD 坐标系下）
        /// </summary>
        /// <param name="poly">多段线对象</param>
        /// <returns>逆时针的多段线（原对象）</returns>
        public static Polyline EnsureCounterclockwise(this Polyline poly)
        {
            if (poly == null || !poly.Closed)
                return poly;

            // 使用代数面积判断方向
            double area = poly.GetAlgebraicArea();
            if (area < 0) // 顺时针（负面积）
            {
                poly.ReverseCurve();
            }

            return poly;
        }

        /// <summary>
        /// 计算多段线的代数面积（Shoelace 公式）
        /// </summary>
        /// <param name="poly">多段线对象</param>
        /// <returns>代数面积（正值=逆时针，负值=顺时针）</returns>
        /// <remarks>
        /// 使用 Shoelace 公式计算有向面积。
        /// 在 AutoCAD 坐标系（Y 轴向上）：
        /// - 正值：逆时针（Counter-clockwise）
        /// - 负值：顺时针（Clockwise）
        /// </remarks>
        public static double GetAlgebraicArea(this Polyline poly)
        {
            if (poly == null || poly.NumberOfVertices < 3)
                return 0;

            double sum = 0;
            int n = poly.NumberOfVertices;

            for (int i = 0; i < n; i++)
            {
                var p1 = poly.GetPoint2dAt(i);
                var p2 = poly.GetPoint2dAt((i + 1) % n);
                sum += (p1.X * p2.Y - p2.X * p1.Y);
            }

            return sum / 2.0;
        }

        #endregion
    }
}

