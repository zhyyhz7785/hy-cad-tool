using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Domain.Services.GeometryAlgorithms
{
    /// <summary>
    /// 多边形算法服务（平台无关）
    /// 提供多边形相关的几何算法
    /// </summary>
    public static class PolygonAlgorithms
    {
        /// <summary>
        /// 确保多边形顶点逆时针排列
        /// 如果是顺时针则反转
        /// </summary>
        public static Polygon2D EnsureCounterClockwise(Polygon2D polygon)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));

            // 如果已经是逆时针，直接返回
            if (polygon.IsCounterClockwise())
                return polygon;

            // 反转顶点顺序
            return polygon.Reverse();
        }

        /// <summary>
        /// 确保多边形顶点顺时针排列
        /// 如果是逆时针则反转
        /// </summary>
        public static Polygon2D EnsureClockwise(Polygon2D polygon)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));

            // 如果已经是顺时针，直接返回
            if (!polygon.IsCounterClockwise())
                return polygon;

            // 反转顶点顺序
            return polygon.Reverse();
        }

        /// <summary>
        /// 去除多边形中重复的顶点
        /// </summary>
        public static Polygon2D RemoveDuplicateVertices(Polygon2D polygon, double tolerance = 1e-6)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));

            var uniqueVertices = new List<Point2D>();
            var vertices = polygon.Vertices;

            for (int i = 0; i < vertices.Count; i++)
            {
                Point2D current = vertices[i];
                Point2D next = vertices[(i + 1) % vertices.Count];

                // 如果当前点与下一个点不重复，添加当前点
                if (current.DistanceTo(next) > tolerance)
                {
                    uniqueVertices.Add(current);
                }
            }

            // 确保至少有3个顶点
            if (uniqueVertices.Count < 3)
                return polygon;

            return new Polygon2D(uniqueVertices, polygon.IsClosed);
        }

        /// <summary>
        /// 用线段分割多边形
        /// 返回分割后的多个多边形
        /// </summary>
        public static IEnumerable<Polygon2D> SplitByLine(Polygon2D polygon, Line2D line)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));

            // 找出所有与线段相交的边
            var intersections = new List<(int edgeIndex, Point2D point)>();

            var edges = polygon.GetEdges().ToList();
            for (int i = 0; i < edges.Count; i++)
            {
                var intersection = edges[i].GetIntersection(line);
                if (intersection != default)
                {
                    intersections.Add((i, intersection));
                }
            }

            // 如果没有交点或只有一个交点，无法分割
            if (intersections.Count < 2)
            {
                yield return polygon;
                yield break;
            }

            // 简化实现：只处理两个交点的情况
            if (intersections.Count == 2)
            {
                var vertices = polygon.Vertices.ToList();
                int idx1 = intersections[0].edgeIndex;
                int idx2 = intersections[1].edgeIndex;
                Point2D pt1 = intersections[0].point;
                Point2D pt2 = intersections[1].point;

                // 构建第一个多边形
                var polygon1Vertices = new List<Point2D>();
                for (int i = 0; i <= idx1; i++)
                {
                    polygon1Vertices.Add(vertices[i]);
                }
                polygon1Vertices.Add(pt1);
                polygon1Vertices.Add(pt2);
                for (int i = idx2 + 1; i < vertices.Count; i++)
                {
                    polygon1Vertices.Add(vertices[i]);
                }

                // 构建第二个多边形
                var polygon2Vertices = new List<Point2D> { pt1 };
                for (int i = idx1 + 1; i <= idx2; i++)
                {
                    polygon2Vertices.Add(vertices[i]);
                }
                polygon2Vertices.Add(pt2);

                if (polygon1Vertices.Count >= 3)
                    yield return new Polygon2D(polygon1Vertices);
                if (polygon2Vertices.Count >= 3)
                    yield return new Polygon2D(polygon2Vertices);
            }
            else
            {
                // 多个交点情况：返回原多边形
                yield return polygon;
            }
        }

        /// <summary>
        /// 计算多边形的最小边界矩形（轴对齐）
        /// </summary>
        public static Polygon2D GetAxisAlignedBoundingRectangle(Polygon2D polygon)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));

            var bbox = polygon.GetBoundingBox();

            var vertices = new[]
            {
                bbox.MinPoint,
                new Point2D(bbox.MaxPoint.X, bbox.MinPoint.Y),
                bbox.MaxPoint,
                new Point2D(bbox.MinPoint.X, bbox.MaxPoint.Y)
            };

            return new Polygon2D(vertices);
        }

        /// <summary>
        /// 判断多边形是否自交
        /// </summary>
        public static bool HasSelfIntersection(Polygon2D polygon, double tolerance = 1e-6)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));

            var edges = polygon.GetEdges().ToList();

            // 检查每对不相邻的边是否相交
            for (int i = 0; i < edges.Count; i++)
            {
                for (int j = i + 2; j < edges.Count; j++)
                {
                    // 跳过首尾边的检查（它们可能共享顶点）
                    if (i == 0 && j == edges.Count - 1)
                        continue;

                    if (edges[i].GetIntersection(edges[j], tolerance) != default)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}

