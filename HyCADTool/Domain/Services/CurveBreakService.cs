using HyCADTool.Shared.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Domain.Services
{
    /// <summary>
    /// 曲线打断服务 - 处理各种曲线类型的交点打断
    /// </summary>
    public class CurveBreakService
    {
        private const double TOLERANCE = 0.001;
        private const double MIN_LENGTH_THRESHOLD = 0.1;

        /// <summary>
        /// 打断线段集合 - 在所有交点处打断
        /// </summary>
        /// <param name="lines">输入线段集合</param>
        /// <param name="minLength">最小线段长度阈值</param>
        /// <returns>打断后的线段集合</returns>
        public List<Line2D> BreakLinesAtIntersections(IEnumerable<Line2D> lines, double minLength = MIN_LENGTH_THRESHOLD)
        {
            var lineList = lines.ToList();
            if (lineList.Count == 0) return new List<Line2D>();

            // 存储每条线段的打断参数
            var breakParameters = new Dictionary<int, HashSet<double>>();
            
            // 初始化
            for (int i = 0; i < lineList.Count; i++)
            {
                breakParameters[i] = new HashSet<double>();
            }

            // 计算所有线段对之间的交点
            for (int i = 0; i < lineList.Count; i++)
            {
                for (int j = i + 1; j < lineList.Count; j++)
                {
                    var line1 = lineList[i];
                    var line2 = lineList[j];

                    // 计算两条线段的交点
                    var intersection = line1.GetIntersection(line2, TOLERANCE);
                    if (intersection != null)
                    {
                        // 计算交点在两条线段上的参数（0到1之间）
                        double param1 = GetParameterAtPoint(line1, intersection);
                        double param2 = GetParameterAtPoint(line2, intersection);

                        // 如果参数在有效范围内（不在端点），添加到打断参数列表
                        if (param1 > TOLERANCE && param1 < 1.0 - TOLERANCE)
                        {
                            breakParameters[i].Add(param1);
                        }
                        if (param2 > TOLERANCE && param2 < 1.0 - TOLERANCE)
                        {
                            breakParameters[j].Add(param2);
                        }
                    }
                }
            }

            // 根据打断参数创建新线段
            var result = new List<Line2D>();
            for (int i = 0; i < lineList.Count; i++)
            {
                var line = lineList[i];
                var parameters = breakParameters[i];

                if (parameters.Count == 0)
                {
                    // 没有交点，保留原线段（如果长度足够）
                    if (line.Length >= minLength)
                    {
                        result.Add(line);
                    }
                }
                else
                {
                    // 有交点，按参数打断线段
                    var sortedParams = new List<double> { 0.0 };
                    sortedParams.AddRange(parameters.OrderBy(p => p));
                    sortedParams.Add(1.0);

                    for (int j = 0; j < sortedParams.Count - 1; j++)
                    {
                        double param1 = sortedParams[j];
                        double param2 = sortedParams[j + 1];

                        if (Math.Abs(param2 - param1) > TOLERANCE)
                        {
                            var segment = CreateSegment(line, param1, param2);
                            if (segment != null && segment.Length >= minLength)
                            {
                                result.Add(segment);
                            }
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 计算点在线段上的参数（0到1之间）
        /// </summary>
        private double GetParameterAtPoint(Line2D line, Point2D point)
        {
            var direction = line.Direction;
            var toPoint = new Vector2D(point.X - line.StartPoint.X, point.Y - line.StartPoint.Y);
            
            // 计算投影长度
            double projection = toPoint.X * direction.X + toPoint.Y * direction.Y;
            double parameter = projection / line.Length;

            return parameter;
        }

        /// <summary>
        /// 根据参数创建线段的子段
        /// </summary>
        private Line2D CreateSegment(Line2D line, double param1, double param2)
        {
            if (param1 > param2)
            {
                var temp = param1;
                param1 = param2;
                param2 = temp;
            }

            var direction = line.Direction;
            var startPoint = new Point2D(
                line.StartPoint.X + direction.X * line.Length * param1,
                line.StartPoint.Y + direction.Y * line.Length * param1
            );
            var endPoint = new Point2D(
                line.StartPoint.X + direction.X * line.Length * param2,
                line.StartPoint.Y + direction.Y * line.Length * param2
            );

            return new Line2D(startPoint, endPoint);
        }
    }
}

