using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.Services.MathAlgorithms;

namespace HyCADTool.Refactored.Domain.Services
{
    /// <summary>
    /// 线段清理服务 - 基础 OVERKILL 功能
    /// 
    /// 核心功能：
    /// 1. 删除完全重复的线段
    /// 2. 合并部分重叠的共线线段
    /// 3. 合并端点接触的共线线段
    /// 4. 删除近距离平行线段（保留端点连接性更好的）
    /// 
    /// 设计原则：
    /// - 只做基础清理，不做复杂操作（如 FILLET、分割等）
    /// - 使用投影区间法准确判断共线线段重叠
    /// - 平行线删除基于端点连接性评分
    /// </summary>
    public class LineOverKillService
    {
        /// <summary>
        /// 清理线段集合 - 主要入口方法
        /// </summary>
        /// <param name="lines">输入线段集合</param>
        /// <param name="tolerance">几何容差</param>
        /// <param name="parallelDistanceThreshold">平行线删除距离阈值（默认1.0）</param>
        /// <returns>清理后的线段列表</returns>
        public List<Line2D> CleanLines(IEnumerable<Line2D> lines, double tolerance, double parallelDistanceThreshold = 1.0)
        {
            var lineList = lines.ToList();
            
            // 只做一件事：合并共线线段（包含去重功能）
            // MergeOverlappingLines 已经通过投影区间法实现了：
            // - 完全重复的线段 → 合并成1条
            // - 部分重叠的共线线段 → 合并
            // - 端点接触的共线线段 → 合并
            // - 有间隙的共线线段 → 不合并
            // - 近距离平行线段 → 删除一条
            return MergeOverlappingLines(lineList, tolerance, parallelDistanceThreshold);
        }

        /// <summary>
        /// 合并重叠线段
        /// </summary>
        /// <param name="lines">输入线段集合</param>
        /// <param name="tolerance">几何容差</param>
        /// <param name="parallelDistanceThreshold">平行线删除距离阈值</param>
        /// <returns>合并后的线段列表</returns>
        public List<Line2D> MergeOverlappingLines(
            IEnumerable<Line2D> lines, 
            double tolerance,
            double parallelDistanceThreshold)
        {
            var lineList = lines.ToList();
            var result = new List<Line2D>();
            var processed = new HashSet<int>();
            
            for (int i = 0; i < lineList.Count; i++)
            {
                if (processed.Contains(i)) continue;
                
                Line2D current = lineList[i];
                processed.Add(i);
                
                // 查找与当前线段重叠的其他线段
                for (int j = i + 1; j < lineList.Count; j++)
                {
                    if (processed.Contains(j)) continue;
                    
                    if (CheckAndMergeOverlap(current, lineList[j], tolerance, parallelDistanceThreshold, out Line2D merged))
                    {
                        current = merged;
                        processed.Add(j);
                    }
                }
                
                result.Add(current);
            }
            
            return result;
        }
        
        /// <summary>
        /// 检查两条线段是否共线且有重叠或端点接触，如果满足条件则合并
        /// 修复：使用投影区间判断，避免合并有间隙的共线线段
        /// </summary>
        private bool CheckAndMergeOverlap(
            Line2D line1, 
            Line2D line2, 
            double tolerance,
            double parallelDistanceThreshold,
            out Line2D merged)
        {
            merged = default;
            
            // 检查是否共线
            if (line1.IsCollinear(line2, tolerance))
            {
                // 共线线段：使用投影区间判断是否有重叠或接触
                return CheckCollinearMerge(line1, line2, tolerance, out merged);
            }
            
                return false;
        }
        
        /// <summary>
        /// 检查共线线段是否可以合并
        /// </summary>
        private bool CheckCollinearMerge(Line2D line1, Line2D line2, double tolerance, out Line2D merged)
        {
            merged = default;
            
            // 检查线段长度，避免零长度线段导致归一化错误
            if (line1.Length < tolerance || line2.Length < tolerance)
            {
                // 如果有零长度线段，选择非零长度的
                if (line1.Length >= tolerance)
                {
                    merged = line1;
                    return true;
                }
                else if (line2.Length >= tolerance)
                {
                    merged = line2;
                    return true;
                }
                return false; // 两条都是零长度，不合并
            }
            
            // 使用投影区间判断是否有间隙（更可靠的方法）
            // 将线段投影到主轴方向上
            Vector2D direction = line1.Direction.Normalize();
            Point2D basePoint = line1.StartPoint;
            
            // 计算4个端点在投影轴上的位置
            double proj1Start = 0;  // line1.StartPoint 作为原点
            double proj1End = direction.Dot(new Vector2D(
                line1.EndPoint.X - basePoint.X, 
                line1.EndPoint.Y - basePoint.Y));
            double proj2Start = direction.Dot(new Vector2D(
                line2.StartPoint.X - basePoint.X, 
                line2.StartPoint.Y - basePoint.Y));
            double proj2End = direction.Dot(new Vector2D(
                line2.EndPoint.X - basePoint.X, 
                line2.EndPoint.Y - basePoint.Y));
            
            // 确保每条线段的投影区间是 [min, max]
            double min1 = Math.Min(proj1Start, proj1End);
            double max1 = Math.Max(proj1Start, proj1End);
            double min2 = Math.Min(proj2Start, proj2End);
            double max2 = Math.Max(proj2Start, proj2End);
            
            // 检查两个区间是否有重叠或接触
            // 有间隙的判断：max1 < min2 - tolerance 或 max2 < min1 - tolerance
            bool hasGap = (max1 < min2 - tolerance) || (max2 < min1 - tolerance);
            
            if (hasGap)
            {
                return false;  // 有间隙，不合并
            }
            
            // 合并（取4个端点中投影最小和最大的点）
            var points = new[] { 
                new { Point = line1.StartPoint, Proj = proj1Start },
                new { Point = line1.EndPoint, Proj = proj1End },
                new { Point = line2.StartPoint, Proj = proj2Start },
                new { Point = line2.EndPoint, Proj = proj2End }
            };
            
            var sorted = points.OrderBy(p => p.Proj).ToList();
            merged = new Line2D(sorted.First().Point, sorted.Last().Point);
            return true;
        }
        
        /// <summary>
        /// 只删除完全重复的线段（不合并共线线段）
        /// </summary>
        public List<Line2D> RemoveDuplicateLines(IEnumerable<Line2D> lines, double tolerance)
        {
            var lineList = lines.ToList();
            var result = new List<Line2D>();
            var processed = new HashSet<int>();
            
            for (int i = 0; i < lineList.Count; i++)
            {
                if (processed.Contains(i)) continue;

                result.Add(lineList[i]);
                processed.Add(i);

                // 查找并标记完全重复的线段
                for (int j = i + 1; j < lineList.Count; j++)
                {
                    if (processed.Contains(j)) continue;

                    // 检查是否完全重复（起点终点都相同，或反向相同）
                    bool sameDirection = lineList[i].StartPoint.DistanceTo(lineList[j].StartPoint) < tolerance &&
                                        lineList[i].EndPoint.DistanceTo(lineList[j].EndPoint) < tolerance;

                    bool reverseDirection = lineList[i].StartPoint.DistanceTo(lineList[j].EndPoint) < tolerance &&
                                           lineList[i].EndPoint.DistanceTo(lineList[j].StartPoint) < tolerance;

                    if (sameDirection || reverseDirection)
                    {
                        processed.Add(j);
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// FILLET 功能 - 第一步：打断相交直线，删除短线段
        /// </summary>
        /// <param name="lines">输入线段集合</param>
        /// <param name="tolerance">几何容差</param>
        /// <param name="minLength">最小线段长度阈值（小于此值的线段将被删除）</param>
        /// <returns>处理后的线段列表</returns>
        public List<Line2D> BreakAndCleanLines(IEnumerable<Line2D> lines, double tolerance, double minLength = 1.0)
        {
            var lineList = lines.ToList();
            
            // 第一步：找到所有交点并打断线段
            var brokenLines = SplitAtIntersections(lineList, tolerance);
            
            // 第二步：删除长度小于阈值的线段（包括零长度线段）
            // 使用 tolerance 作为最小长度，避免零长度线段
            double effectiveMinLength = Math.Max(minLength, tolerance);
            var result = brokenLines.Where(line => line.Length >= effectiveMinLength).ToList();
            
            return result;
        }

        /// <summary>
        /// FILLET 功能 - 第二步：延伸端点距离很近的线段到它们的交点
        /// </summary>
        /// <param name="lines">输入线段集合</param>
        /// <param name="tolerance">几何容差</param>
        /// <param name="maxDistance">最大端点距离阈值</param>
        /// <returns>处理后的线段列表</returns>
        public List<Line2D> ExtendNearEndpoints(IEnumerable<Line2D> lines, double tolerance, double maxDistance = 10.0)
        {
            var lineList = lines.ToList();
            var result = new List<Line2D>();
            var processed = new HashSet<int>();

            for (int i = 0; i < lineList.Count; i++)
            {
                if (processed.Contains(i))
                {
                    result.Add(lineList[i]);
                    continue;
                }
                
                Line2D currentLine = lineList[i];
                bool extended = false;
                
                // 检查与其他线段的端点距离
                for (int j = 0; j < lineList.Count; j++)
                {
                    if (i == j || processed.Contains(j)) continue;
                    
                    Line2D otherLine = lineList[j];
                    
                    // 检查四种端点组合
                    var nearPairs = new[]
                    {
                        (currentLine.EndPoint, otherLine.StartPoint, true, true),    // current.End - other.Start
                        (currentLine.EndPoint, otherLine.EndPoint, true, false),     // current.End - other.End
                        (currentLine.StartPoint, otherLine.StartPoint, false, true), // current.Start - other.Start
                        (currentLine.StartPoint, otherLine.EndPoint, false, false)   // current.Start - other.End
                    };
                    
                    foreach (var (p1, p2, isCurrentEnd, isOtherStart) in nearPairs)
                    {
                        double distance = p1.DistanceTo(p2);
                        
                        if (distance > tolerance && distance <= maxDistance)
                        {
                            // 计算无限延长线的交点
                            var intersection = currentLine.GetIntersectionWithInfiniteLine(otherLine, tolerance);
                            
                            if (intersection != default)
                            {
                                // 延伸当前线段到交点
                                if (isCurrentEnd)
                                {
                                    currentLine = new Line2D(currentLine.StartPoint, intersection);
                                }
                                else
                                {
                                    currentLine = new Line2D(intersection, currentLine.EndPoint);
                                }
                                
                                // 延伸另一条线段到交点
                                if (isOtherStart)
                                {
                                    lineList[j] = new Line2D(intersection, otherLine.EndPoint);
                                }
                                else
                                {
                                    lineList[j] = new Line2D(otherLine.StartPoint, intersection);
                                }
                                
                                extended = true;
                                break;
                            }
                        }
                    }
                    
                    if (extended) break;
                }
                
                    result.Add(currentLine);
                processed.Add(i);
            }

            return result;
        }

        /// <summary>
        /// FILLET 功能 - 第三步：端点延伸到线段，并在交点处打断线段
        /// </summary>
        /// <param name="lines">输入线段集合</param>
        /// <param name="tolerance">几何容差</param>
        /// <param name="maxDistance">最大端点到线段距离阈值</param>
        /// <returns>处理后的线段列表</returns>
        public List<Line2D> ExtendEndpointToLine(IEnumerable<Line2D> lines, double tolerance, double maxDistance = 10.0)
        {
            var lineList = lines.ToList();
            var result = new List<Line2D>();

            for (int i = 0; i < lineList.Count; i++)
            {
                Line2D currentLine = lineList[i];
                bool extended = false;
                
                // 检查当前线段的两个端点到其他线段的距离
                for (int j = 0; j < lineList.Count; j++)
                {
                    if (i == j) continue;
                    
                    Line2D otherLine = lineList[j];
                    
                    // 检查起点到 otherLine 的距离
                    double distStart = DistanceCalculator.PointToLineSegmentDistance(
                        currentLine.StartPoint, otherLine.StartPoint, otherLine.EndPoint);
                    
                    // 检查终点到 otherLine 的距离
                    double distEnd = DistanceCalculator.PointToLineSegmentDistance(
                        currentLine.EndPoint, otherLine.StartPoint, otherLine.EndPoint);
                    
                    // 处理起点
                    if (distStart > tolerance && distStart <= maxDistance)
                    {
                        // 计算交点（currentLine 的无限延长线与 otherLine 的交点）
                        var intersection = currentLine.GetIntersectionWithInfiniteLine(otherLine, tolerance);
                        
                        if (intersection != default && otherLine.IsPointOnSegment(intersection, tolerance))
                        {
                            // 延伸当前线段的起点到交点
                            currentLine = new Line2D(intersection, currentLine.EndPoint);
                            
                            // 在交点处打断 otherLine
                            double param = GetProjectionParameter(otherLine, intersection);
                            if (param > 0.01 && param < 0.99)
                            {
                                // 分割成两段
                                Line2D segment1 = new Line2D(otherLine.StartPoint, intersection);
                                Line2D segment2 = new Line2D(intersection, otherLine.EndPoint);
                                
                                // 替换 otherLine
                                lineList[j] = segment1;
                                lineList.Add(segment2);
                            }
                            
                            extended = true;
                            break;
                        }
                    }
                    
                    // 处理终点
                    if (!extended && distEnd > tolerance && distEnd <= maxDistance)
                    {
                        // 计算交点（currentLine 的无限延长线与 otherLine 的交点）
                        var intersection = currentLine.GetIntersectionWithInfiniteLine(otherLine, tolerance);
                        
                        if (intersection != default && otherLine.IsPointOnSegment(intersection, tolerance))
                        {
                            // 延伸当前线段的终点到交点
                            currentLine = new Line2D(currentLine.StartPoint, intersection);
                            
                            // 在交点处打断 otherLine
                            double param = GetProjectionParameter(otherLine, intersection);
                            if (param > 0.01 && param < 0.99)
                            {
                                // 分割成两段
                                Line2D segment1 = new Line2D(otherLine.StartPoint, intersection);
                                Line2D segment2 = new Line2D(intersection, otherLine.EndPoint);
                                
                                // 替换 otherLine
                                lineList[j] = segment1;
                                lineList.Add(segment2);
                            }
                            
                            extended = true;
                            break;
                        }
                    }
                    
                    if (extended) break;
                }
                
                result.Add(currentLine);
            }

            return result;
        }

        /// <summary>
        /// 在交点处分割线段
        /// </summary>
        private List<Line2D> SplitAtIntersections(List<Line2D> lines, double tolerance)
        {
            // 存储每条线段的分割点（使用投影参数）
            var splitParams = new Dictionary<int, List<double>>();
            
            // 初始化
            for (int i = 0; i < lines.Count; i++)
            {
                splitParams[i] = new List<double>();
            }
            
            // 计算所有交点
            int totalIntersections = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                for (int j = i + 1; j < lines.Count; j++)
                {
                    var intersection = lines[i].GetIntersection(lines[j], tolerance);
                    if (intersection != default)
                    {
                        totalIntersections++;
                        
                        // 计算交点在两条线段上的投影参数
                        double param1 = GetProjectionParameter(lines[i], intersection);
                        double param2 = GetProjectionParameter(lines[j], intersection);
                        
                        // 只添加在线段内部的交点（不在端点）
                        // 使用更宽松的端点判断：容差设为 0.01
                        if (param1 > 0.01 && param1 < 0.99)
                        {
                            splitParams[i].Add(param1);
                        }
                        
                        if (param2 > 0.01 && param2 < 0.99)
                        {
                            splitParams[j].Add(param2);
                        }
                    }
                }
            }
            
            // 统计有多少线段被打断
            int linesWithIntersections = splitParams.Count(kvp => kvp.Value.Count > 0);
            System.Diagnostics.Debug.WriteLine($"找到 {totalIntersections} 个交点，将打断 {linesWithIntersections} 条线段");
            
            // 分割线段
            var result = new List<Line2D>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (splitParams[i].Count == 0)
                {
                    // 没有交点，保留原线段
                    result.Add(lines[i]);
                }
                else
                {
                    // 有交点，分割线段
                    var segments = SplitLineAtParameters(lines[i], splitParams[i]);
                    result.AddRange(segments);
                }
            }

            return result;
        }

        /// <summary>
        /// 计算点在线段上的投影参数（0 = 起点，1 = 终点）
        /// </summary>
        private double GetProjectionParameter(Line2D line, Point2D point)
        {
            Vector2D lineVec = line.Direction;
            Vector2D pointVec = new Vector2D(point.X - line.StartPoint.X, point.Y - line.StartPoint.Y);
            
            double lineLength = line.Length;
            if (lineLength < 1e-10) return 0.0;
            
            double projection = lineVec.Dot(pointVec) / (lineLength * lineLength);
            return Math.Max(0.0, Math.Min(1.0, projection));
        }

        /// <summary>
        /// 在指定参数处分割线段
        /// </summary>
        private List<Line2D> SplitLineAtParameters(Line2D line, List<double> parameters)
        {
            // 添加起点和终点参数
            var allParams = new List<double> { 0.0 };
            allParams.AddRange(parameters);
            allParams.Add(1.0);
            
            // 排序并去重
            allParams = allParams.Distinct().OrderBy(p => p).ToList();
            
            // 创建线段
            var segments = new List<Line2D>();
            for (int i = 0; i < allParams.Count - 1; i++)
            {
                double t1 = allParams[i];
                double t2 = allParams[i + 1];
                
                if (Math.Abs(t2 - t1) > 1e-10)
                {
                    Point2D p1 = line.StartPoint.Add(line.Direction * t1);
                    Point2D p2 = line.StartPoint.Add(line.Direction * t2);
                    segments.Add(new Line2D(p1, p2));
                }
            }
            
            return segments;
        }
    }
}
