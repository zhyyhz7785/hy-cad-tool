using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions
{
    /// <summary>
    /// Line 实体扩展方法
    /// 提供线段连接、排序等实用功能
    /// </summary>
    public static class LineExtensions
    {
        #region 线段连接与排序

        /// <summary>
        /// 按连接性排序线段（首尾相连）
        /// </summary>
        /// <param name="lines">输入线段集合</param>
        /// <param name="tolerance">判断点重合的容差</param>
        /// <returns>排序后的线段列表（保证首尾相连）</returns>
        /// <remarks>
        /// 如果线段不能完全连接，返回尽可能长的连接序列。
        /// 如果需要反转线段方向以保证连接性，会创建反转的线段。
        /// </remarks>
        public static List<Line> SortByConnectivity(
            this IEnumerable<Line> lines,
            double tolerance = 1e-4)
        {
            var input = lines.ToList();
            if (input.Count == 0)
                return new List<Line>();

            var used = new HashSet<int>();
            var result = new List<Line>();
            var current = input[0];

            result.Add(current);
            used.Add(0);

            while (result.Count < input.Count)
            {
                var end = current.EndPoint;
                bool found = false;
                var tol = new Tolerance(tolerance, tolerance);

                for (int i = 0; i < input.Count; i++)
                {
                    if (used.Contains(i))
                        continue;

                    var candidate = input[i];

                    if (candidate.StartPoint.IsEqualTo(end, tol))
                    {
                        result.Add(candidate);
                        used.Add(i);
                        current = candidate;
                        found = true;
                        break;
                    }
                    else if (candidate.EndPoint.IsEqualTo(end, tol))
                    {
                        // 需要反转线段
                        var reversed = new Line(
                            candidate.EndPoint,
                            candidate.StartPoint);
                        result.Add(reversed);
                        used.Add(i);
                        current = reversed;
                        found = true;
                        break;
                    }
                }

                if (!found)
                    break;
            }

            return result;
        }

        /// <summary>
        /// 将连续线段连接为多段线
        /// </summary>
        /// <param name="lines">输入线段集合</param>
        /// <param name="tolerance">判断连接性的容差</param>
        /// <param name="requireClosed">是否要求闭合（首尾相连）</param>
        /// <returns>连接后的多段线，如果线段数量不足或不满足闭合要求则返回 null</returns>
        public static Polyline JoinToPolyline(
            this IEnumerable<Line> lines,
            double tolerance = 1e-4,
            bool requireClosed = true)
        {
            var sorted = lines.SortByConnectivity(tolerance);

            if (sorted.Count < 2)
                return null;

            // 检查是否闭合
            var tol = new Tolerance(tolerance, tolerance);
            bool isClosed = sorted[sorted.Count - 1].EndPoint.IsEqualTo(
                sorted[0].StartPoint, tol);

            if (requireClosed && !isClosed)
                return null;

            var poly = new Polyline();
            for (int i = 0; i < sorted.Count; i++)
            {
                var pt = sorted[i].StartPoint;
                poly.AddVertexAt(i,
                    new Point2d(pt.X, pt.Y), 0, 0, 0);
            }

            poly.Closed = isClosed;
            return poly;
        }

        /// <summary>
        /// 将单条线段转换为多段线
        /// </summary>
        /// <param name="line">输入线段</param>
        /// <returns>包含该线段的多段线（不闭合）</returns>
        public static Polyline ToPolyline(this Line line)
        {
            if (line == null)
                return null;

            var poly = new Polyline();
            poly.AddVertexAt(0,
                new Point2d(line.StartPoint.X, line.StartPoint.Y),
                0, 0, 0);
            poly.AddVertexAt(1,
                new Point2d(line.EndPoint.X, line.EndPoint.Y),
                0, 0, 0);
            poly.Closed = false;

            return poly;
        }

        #endregion
    }
}

