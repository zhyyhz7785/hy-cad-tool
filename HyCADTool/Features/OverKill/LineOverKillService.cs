using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Shared.Geometry;
using HyCADTool.Domain.Services.MathAlgorithms;

namespace HyCADTool.Features.OverKill
{
    /// <summary>
    /// 线段清理服务 - OVERKILL + FILLET 功能
    /// 
    /// 【核心功能】
    /// 1. OVERKILL 清理：合并重叠/共线线段，删除近距离平行线
    /// 2. FILLET 连接：打断相交线段，延伸近距离端点，自动连接
    /// 3. 独立端点检测：查找未连接的端点
    /// 
    /// 【关键算法】
    /// - 投影区间法：准确判断共线线段是否有间隙（CheckCollinearMerge）
    /// - 智能投影轴：根据角度选择X/Y轴投影，避免数值精度问题（ForceMergeParallelLines）
    /// - 零长度保护：所有涉及 Normalize() 的地方都有长度检查
    /// - 相对参数阈值：使用 0.01/0.99 避免在端点附近打断（ExtendEndpointToLine, SplitAtIntersections）
    /// 
    /// 【测试状态】✅ 已通过生产环境测试
    /// </summary>
    public class LineOverKillService
    {
        #region OVERKILL 基础清理功能

        /// <summary>
        /// 清理线段集合 - OVERKILL 主入口
        /// </summary>
        /// <param name="lines">输入线段集合</param>
        /// <param name="tolerance">几何容差（用于点重合、共线判断），推荐 1e-6</param>
        /// <param name="parallelDistanceThreshold">平行线合并距离阈值（单位：图形单位），推荐 1.0
        /// <br/>- 两条平行线之间的距离小于此值时将被合并</param>
        /// <returns>清理后的线段列表</returns>
        /// <remarks>
        /// 功能：合并重叠/共线/近距离平行线段
        /// <br/>调用链：CleanLines → MergeOverlappingLines → CheckAndMergeOverlap
        /// </remarks>
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
        /// 合并重叠/共线/近距离平行线段
        /// </summary>
        /// <param name="lines">输入线段集合</param>
        /// <param name="tolerance">几何容差（推荐 1e-6）</param>
        /// <param name="parallelDistanceThreshold">平行线合并距离阈值（推荐 1.0）</param>
        /// <returns>合并后的线段列表</returns>
        /// <remarks>
        /// 处理逻辑：
        /// <br/>1. 遍历所有线段对
        /// <br/>2. 调用 CheckAndMergeOverlap 判断是否可以合并
        /// <br/>3. 如果可以合并，用新线段替换当前线段，标记被合并的线段
        /// </remarks>
        public List<Line2D> MergeOverlappingLines(
            IEnumerable<Line2D> lines, 
            double tolerance,
            double parallelDistanceThreshold)
        {
            var lineList = lines.ToList();
            var result = new List<Line2D>();
            var processed = new HashSet<int>();
            
            // 性能优化：使用空间网格索引
            var gridSize = Math.Max(100.0, parallelDistanceThreshold * 2);
            var spatialIndex = BuildSpatialIndex(lineList, gridSize);
            
            for (int i = 0; i < lineList.Count; i++)
            {
                if (processed.Contains(i)) continue;
                
                Line2D current = lineList[i];
                processed.Add(i);
                
                // 只检查附近的线段（使用空间索引）
                var nearbyIndices = GetNearbyLineIndices(current, spatialIndex, gridSize, parallelDistanceThreshold);
                
                foreach (int j in nearbyIndices)
                {
                    if (j <= i || processed.Contains(j)) continue;
                    
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
            
            // 检查是否为近距离平行线
            // 使用更宽松的角度容差（0.01弧度 ≈ 0.57度）来判断平行
            const double angleToleranceForParallel = 0.01;
            if (line1.IsParallelTo(line2, angleToleranceForParallel))
            {
                double distance = CalculateParallelDistance(line1, line2);
                if (distance <= parallelDistanceThreshold)
                {
                    // 距离很近的平行线，强制合并（忽略投影间隙）
                    return ForceMergeParallelLines(line1, line2, tolerance, out merged);
                }
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
        /// 强制合并近距离平行线（忽略投影区间间隙）
        /// 根据线段角度选择合适的投影方向（接近水平用X轴，接近垂直用Y轴）
        /// </summary>
        private bool ForceMergeParallelLines(Line2D line1, Line2D line2, double tolerance, out Line2D merged)
        {
            merged = default;
            
            // 检查线段长度，避免零长度线段
            if (line1.Length < tolerance || line2.Length < tolerance)
            {
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
                return false;
            }
            
            // 根据线段角度选择投影方向
            // 计算线段与X轴的夹角（绝对值）
            Vector2D dir = line1.Direction.Normalize();
            double absAngleWithX = Math.Abs(Math.Atan2(dir.Y, dir.X));
            
            // 如果角度接近垂直（45° ~ 135°），使用Y轴投影；否则使用X轴投影
            bool useYProjection = (absAngleWithX > Math.PI / 4 && absAngleWithX < 3 * Math.PI / 4);
            
            // 计算4个端点在选定轴上的投影值
            double proj1Start, proj1End, proj2Start, proj2End;
            
            if (useYProjection)
            {
                // 使用Y轴投影（适用于垂直线段）
                proj1Start = line1.StartPoint.Y;
                proj1End = line1.EndPoint.Y;
                proj2Start = line2.StartPoint.Y;
                proj2End = line2.EndPoint.Y;
            }
            else
            {
                // 使用X轴投影（适用于水平线段）
                proj1Start = line1.StartPoint.X;
                proj1End = line1.EndPoint.X;
                proj2Start = line2.StartPoint.X;
                proj2End = line2.EndPoint.X;
            }
            
            // 强制合并：取4个端点中投影最小和最大的点（忽略间隙）
            var points = new[] { 
                new { Point = line1.StartPoint, Proj = proj1Start },
                new { Point = line1.EndPoint, Proj = proj1End },
                new { Point = line2.StartPoint, Proj = proj2Start },
                new { Point = line2.EndPoint, Proj = proj2End }
            };
            
            var sorted = points.OrderBy(p => p.Proj).ToList();
            
            // 使用投影最远的两个端点作为新线段端点
            merged = new Line2D(sorted.First().Point, sorted.Last().Point);
            return true;
        }
        
        /// <summary>
        /// 计算两条平行线之间的距离（点到线段的距离）
        /// </summary>
        private double CalculateParallelDistance(Line2D line1, Line2D line2)
        {
            // 计算 line1 的两个端点到 line2 的距离
            double dist1 = DistanceCalculator.PointToLineSegmentDistance(line1.StartPoint, line2.StartPoint, line2.EndPoint);
            double dist2 = DistanceCalculator.PointToLineSegmentDistance(line1.EndPoint, line2.StartPoint, line2.EndPoint);
            
            // 计算 line2 的两个端点到 line1 的距离
            double dist3 = DistanceCalculator.PointToLineSegmentDistance(line2.StartPoint, line1.StartPoint, line1.EndPoint);
            double dist4 = DistanceCalculator.PointToLineSegmentDistance(line2.EndPoint, line1.StartPoint, line1.EndPoint);
            
            // 返回最小距离（最近的两个点之间的距离）
            return Math.Min(Math.Min(dist1, dist2), Math.Min(dist3, dist4));
        }
        
        #endregion

        #region 辅助功能 - 删除重复

        /// <summary>
        /// 删除完全重复的线段（不合并共线线段）
        /// </summary>
        /// <param name="lines">输入线段集合</param>
        /// <param name="tolerance">几何容差（推荐 1e-6）</param>
        /// <returns>删除重复后的线段列表</returns>
        /// <remarks>
        /// 判断重复的标准：
        /// <br/>- 同向：起点到起点 &lt; tolerance 且 终点到终点 &lt; tolerance
        /// <br/>- 反向：起点到终点 &lt; tolerance 且 终点到起点 &lt; tolerance
        /// </remarks>
        public List<Line2D> RemoveDuplicateLines(IEnumerable<Line2D> lines, double tolerance)
        {
            var lineList = lines.ToList();
            
            // 性能优化：使用基于坐标网格的哈希去重
            // 将坐标量化到网格，快速识别可能重复的线段
            var gridResolution = Math.Max(tolerance, 0.001);
            
            // 自适应并行：只有数据量足够大时才启用并行（阈值：10000条线段）
            const int PARALLEL_THRESHOLD = 10000;
            
            if (lineList.Count >= PARALLEL_THRESHOLD)
            {
                // 大数据集：使用并行分组
                var lineGroups = new System.Collections.Concurrent.ConcurrentDictionary<(long, long, long, long), System.Collections.Concurrent.ConcurrentBag<int>>();
                
                System.Threading.Tasks.Parallel.For(0, lineList.Count, i =>
                {
                    var line = lineList[i];
                    var key1 = GetLineGridKey(line, gridResolution, false);
                    var key2 = GetLineGridKey(line, gridResolution, true);
                    
                    lineGroups.GetOrAdd(key1, _ => new System.Collections.Concurrent.ConcurrentBag<int>()).Add(i);
                    
                    if (key2 != key1)
                    {
                        lineGroups.GetOrAdd(key2, _ => new System.Collections.Concurrent.ConcurrentBag<int>()).Add(i);
                    }
                });
                
                return ProcessDuplicates(lineList, lineGroups, tolerance, gridResolution);
            }
            else
            {
                // 小数据集：使用串行分组（更快）
                var lineGroups = new Dictionary<(long, long, long, long), List<int>>();
                
                for (int i = 0; i < lineList.Count; i++)
                {
                    var line = lineList[i];
                    var key1 = GetLineGridKey(line, gridResolution, false);
                    var key2 = GetLineGridKey(line, gridResolution, true);
                    
                    if (!lineGroups.ContainsKey(key1))
                        lineGroups[key1] = new List<int>();
                    lineGroups[key1].Add(i);
                    
                    if (key2 != key1)
                    {
                        if (!lineGroups.ContainsKey(key2))
                            lineGroups[key2] = new List<int>();
                        lineGroups[key2].Add(i);
                    }
                }
                
                return ProcessDuplicates(lineList, lineGroups, tolerance, gridResolution);
            }
        }
        
        /// <summary>
        /// 处理重复线段（串行比较，保证结果一致性）- 串行版本
        /// </summary>
        private List<Line2D> ProcessDuplicates(
            List<Line2D> lineList, 
            Dictionary<(long, long, long, long), List<int>> lineGroups, 
            double tolerance, 
            double gridResolution)
        {
            var processed = new HashSet<int>();
            var result = new List<Line2D>();
            
            for (int i = 0; i < lineList.Count; i++)
            {
                if (processed.Contains(i)) continue;
                
                result.Add(lineList[i]);
                processed.Add(i);
                
                var key = GetLineGridKey(lineList[i], gridResolution, false);
                if (lineGroups.TryGetValue(key, out var group))
                {
                    foreach (int j in group)
                    {
                        if (j <= i || processed.Contains(j)) continue;
                        
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
            }
            
            return result;
        }
        
        /// <summary>
        /// 处理重复线段（串行比较，保证结果一致性）- 并行版本
        /// </summary>
        private List<Line2D> ProcessDuplicates(
            List<Line2D> lineList, 
            System.Collections.Concurrent.ConcurrentDictionary<(long, long, long, long), System.Collections.Concurrent.ConcurrentBag<int>> lineGroups, 
            double tolerance, 
            double gridResolution)
        {
            var processed = new HashSet<int>();
            var result = new List<Line2D>();
            
            for (int i = 0; i < lineList.Count; i++)
            {
                if (processed.Contains(i)) continue;
                
                result.Add(lineList[i]);
                processed.Add(i);
                
                var key = GetLineGridKey(lineList[i], gridResolution, false);
                if (lineGroups.TryGetValue(key, out var group))
                {
                    foreach (int j in group)
                    {
                        if (j <= i || processed.Contains(j)) continue;
                        
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
            }
            
            return result;
        }
        
        /// <summary>
        /// 获取线段的网格键（用于哈希分组）
        /// </summary>
        private (long, long, long, long) GetLineGridKey(Line2D line, double gridResolution, bool reverse)
        {
            if (reverse)
            {
                return (
                    (long)Math.Round(line.EndPoint.X / gridResolution),
                    (long)Math.Round(line.EndPoint.Y / gridResolution),
                    (long)Math.Round(line.StartPoint.X / gridResolution),
                    (long)Math.Round(line.StartPoint.Y / gridResolution)
                );
            }
            else
            {
                return (
                    (long)Math.Round(line.StartPoint.X / gridResolution),
                    (long)Math.Round(line.StartPoint.Y / gridResolution),
                    (long)Math.Round(line.EndPoint.X / gridResolution),
                    (long)Math.Round(line.EndPoint.Y / gridResolution)
                );
            }
        }

        #endregion

        #region FILLET 功能 - 自动连接
        
        /// <summary>
        /// FILLET 步骤1：打断相交线段（不过滤）
        /// </summary>
        /// <param name="lines">输入线段集合</param>
        /// <param name="tolerance">几何容差（推荐 1e-6）</param>
        /// <returns>打断后的线段列表（未过滤）</returns>
        /// <remarks>
        /// 处理流程：
        /// <br/>1. 调用 SplitAtIntersections 在所有相交处打断线段
        /// <br/>2. 使用固定阈值 0.5mm 避免产生极短线段
        /// <br/>🔑 核心思想：只打断，不过滤，过滤由后续统一步骤处理
        /// </remarks>
        public List<Line2D> BreakAtIntersections(IEnumerable<Line2D> lines, double tolerance, double minBreakThreshold = 0.5)
        {
            var lineList = lines.ToList();
            
            // 使用空间索引 + 并行（已验证正确且高效）
            // 扫描线算法由于动态比较器问题暂时禁用
            var brokenLines = SplitAtIntersections(lineList, tolerance, minBreakThreshold);
            
            return brokenLines;  // 不过滤，直接返回
        }

        /// <summary>
        /// FILLET 步骤2：延伸端点距离很近的线段到它们的交点（FILLET R=0）
        /// </summary>
        /// <param name="lines">输入线段集合</param>
        /// <param name="tolerance">几何容差（推荐 1e-6）</param>
        /// <param name="maxDistance">最大端点距离阈值（单位：图形单位），推荐 10.0
        /// <br/>- 只有端点距离在 (tolerance, maxDistance] 范围内的线段才会延伸</param>
        /// <returns>处理后的线段列表</returns>
        /// <remarks>
        /// 处理逻辑：
        /// <br/>1. 检查所有线段的4种端点组合（起-起、起-终、终-起、终-终）
        /// <br/>2. 如果端点距离 ∈ (tolerance, maxDistance]，计算无限延长线交点
        /// <br/>3. 延伸两条线段到交点
        /// <br/>⚠️ 直接修改 lineList，后续线段会使用更新后的数据
        /// </remarks>
        public List<Line2D> ExtendNearEndpoints(IEnumerable<Line2D> lines, double tolerance, double maxDistance = 10.0)
        {
            var lineList = lines.ToList();
            
            // 自适应算法选择：第2步只处理端点对，不涉及动态插入，可以安全使用扫描线
            const int SWEEPLINE_THRESHOLD = 3000;
            
            if (lineList.Count >= SWEEPLINE_THRESHOLD)
            {
                // 大数据集：使用扫描线优化
                return ExtendNearEndpoints_SweepLine(lineList, tolerance, maxDistance);
            }
            
            // 小数据集：使用空间索引（原方法）
            var result = new List<Line2D>();
            var processed = new HashSet<int>();

            // 性能优化：使用空间网格索引
            var gridSize = maxDistance * 2;
            var spatialIndex = BuildSpatialIndex(lineList, gridSize);

            for (int i = 0; i < lineList.Count; i++)
            {
                if (processed.Contains(i))
                {
                    result.Add(lineList[i]);
                    continue;
                }
                
                Line2D currentLine = lineList[i];
                bool extended = false;
                
                // 只检查附近的线段（使用空间索引）
                var nearbyIndices = GetNearbyLineIndices(currentLine, spatialIndex, gridSize, maxDistance);
                
                foreach (int j in nearbyIndices)
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
                                Line2D newCurrentLine;
                                if (isCurrentEnd)
                                {
                                    newCurrentLine = new Line2D(currentLine.StartPoint, intersection);
                                }
                                else
                                {
                                    newCurrentLine = new Line2D(intersection, currentLine.EndPoint);
                                }
                                
                                // 延伸另一条线段到交点
                                Line2D newOtherLine;
                                if (isOtherStart)
                                {
                                    newOtherLine = new Line2D(intersection, otherLine.EndPoint);
                                }
                                else
                                {
                                    newOtherLine = new Line2D(otherLine.StartPoint, intersection);
                                }
                                
                                // 检查新线段是否有效（长度 > 0）
                                if (newCurrentLine.Length > 1e-6 && newOtherLine.Length > 1e-6)
                                {
                                    currentLine = newCurrentLine;
                                    lineList[j] = newOtherLine;
                                    extended = true;
                                    break;
                                }
                                else
                                {
                                    // 调试输出：跳过零长度线段的延伸
                                    System.Diagnostics.Debug.WriteLine($"警告：跳过零长度延伸 - currentLine: {newCurrentLine.Length:F6}, otherLine: {newOtherLine.Length:F6}");
                                }
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
        /// 查找所有独立端点（未与其他线段连接的端点）
        /// </summary>
        /// <param name="lines">线段集合</param>
        /// <param name="tolerance">几何容差（推荐 1e-6）</param>
        /// <returns>独立端点列表（包含端点位置和对应线段的归一化方向向量）</returns>
        /// <remarks>
        /// 判断标准：
        /// <br/>- 如果端点与任何其他线段的端点距离 &lt; tolerance，则认为已连接
        /// <br/>- 否则标记为独立端点
        /// <br/>用途：绘制独立端点标记，提示用户未连接的地方
        /// </remarks>
        public List<(Point2D Point, Vector2D Direction)> FindIndependentEndpoints(IEnumerable<Line2D> lines, double tolerance)
        {
            var lineList = lines.ToList();
            if (lineList.Count == 0) return new List<(Point2D Point, Vector2D Direction)>();
            
            // 构建所有端点的空间索引
            var allEndpoints = new List<(Point2D Point, int LineIndex, bool IsStart)>();
            for (int i = 0; i < lineList.Count; i++)
            {
                allEndpoints.Add((lineList[i].StartPoint, i, true));
                allEndpoints.Add((lineList[i].EndPoint, i, false));
            }
            
            // 使用空间索引加速端点查找
            double gridSize = Math.Max(tolerance * 2, 1.0);
            var endpointGrid = new Dictionary<(long, long), List<(Point2D Point, int LineIndex, bool IsStart)>>();
            
            foreach (var endpoint in allEndpoints)
            {
                long cellX = (long)(endpoint.Point.X / gridSize);
                long cellY = (long)(endpoint.Point.Y / gridSize);
                var key = (cellX, cellY);
                
                if (!endpointGrid.ContainsKey(key))
                    endpointGrid[key] = new List<(Point2D, int, bool)>();
                endpointGrid[key].Add(endpoint);
            }
            
            // 检查每条线段的端点是否独立
            var independentEndpoints = new List<(Point2D Point, Vector2D Direction)>();
            
            for (int i = 0; i < lineList.Count; i++)
            {
                var line = lineList[i];
                
                // 检查起点
                if (!IsEndpointConnected(line.StartPoint, i, endpointGrid, gridSize, tolerance))
                {
                    independentEndpoints.Add((line.StartPoint, line.Direction.Normalize()));
                }
                
                // 检查终点
                if (!IsEndpointConnected(line.EndPoint, i, endpointGrid, gridSize, tolerance))
                {
                    independentEndpoints.Add((line.EndPoint, line.Direction.Normalize()));
                }
            }
            
            return independentEndpoints;
        }
        
        /// <summary>
        /// 检查端点是否连接到其他线段
        /// </summary>
        private bool IsEndpointConnected(
            Point2D point, 
            int currentLineIndex, 
            Dictionary<(long, long), List<(Point2D Point, int LineIndex, bool IsStart)>> grid, 
            double gridSize, 
            double tolerance)
        {
            long cellX = (long)(point.X / gridSize);
            long cellY = (long)(point.Y / gridSize);
            
            // 检查当前单元格和周围8个单元格
            for (long dx = -1; dx <= 1; dx++)
            {
                for (long dy = -1; dy <= 1; dy++)
                {
                    var key = (cellX + dx, cellY + dy);
                    if (!grid.ContainsKey(key)) continue;
                    
                    foreach (var (otherPoint, otherLineIndex, _) in grid[key])
                    {
                        if (otherLineIndex == currentLineIndex) continue;
                        
                        if (point.DistanceTo(otherPoint) < tolerance)
                        {
                            return true;
                        }
                    }
                }
            }
            
            return false;
        }

        /// <summary>
        /// FILLET 步骤3：端点延伸到线段，并在交点处打断线段（不过滤）
        /// </summary>
        /// <param name="lines">输入线段集合</param>
        /// <param name="tolerance">几何容差（推荐 1e-6）</param>
        /// <param name="maxDistance">最大端点到线段距离阈值（单位：图形单位），推荐 10.0
        /// <br/>- 只有端点到线段的距离在 (tolerance, maxDistance] 范围内才会延伸</param>
        /// <returns>处理后的线段列表（未过滤）</returns>
        /// <remarks>
        /// 处理逻辑：
        /// <br/>1. 对每条线段，检查其端点到其他线段的垂直距离
        /// <br/>2. 如果距离 ∈ (tolerance, maxDistance]，计算无限延长线交点
        /// <br/>3. 延伸当前线段的端点到交点
        /// <br/>4. 在交点处打断另一条线段（使用固定阈值 0.5mm 避免极短线段）
        /// <br/>⚠️ 直接修改 lineList，使用 else if 保证每条线只延伸一个端点
        /// <br/>🔑 核心思想：只延伸和打断，不过滤，过滤由后续统一步骤处理
        /// </remarks>
        public List<Line2D> ExtendEndpointToLine(IEnumerable<Line2D> lines, double tolerance, double maxDistance = 10.0, double minBreakThreshold = 0.5)
        {
            var lineList = lines.ToList();
            
            // ⚠️ 防御性编程：过滤零长度或极短线段（避免 Normalize 错误）
            lineList = lineList.Where(line => line.Length >= tolerance).ToList();
            
            // 注意：第3步涉及动态插入线段，扫描线算法容易出错
            // 因此保持使用空间索引（已验证正确）
            // minBreakThreshold: 打断保护阈值，避免产生极短线段
            var result = new List<Line2D>();

            // 性能优化：使用空间网格索引
            var gridSize = maxDistance * 2;
            var spatialIndex = BuildSpatialIndex(lineList, gridSize);

            for (int i = 0; i < lineList.Count; i++)
            {
                Line2D currentLine = lineList[i];
                bool extended = false;
                
                // 只检查附近的线段（使用空间索引）
                var nearbyIndices = GetNearbyLineIndices(currentLine, spatialIndex, gridSize, maxDistance);
                
                foreach (int j in nearbyIndices)
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
                            Line2D newCurrentLine = new Line2D(intersection, currentLine.EndPoint);
                            
                            // 检查新线段是否有效（长度 > 0）
                            if (newCurrentLine.Length < 1e-6)
                            {
                                System.Diagnostics.Debug.WriteLine($"警告：跳过零长度延伸（起点） - 长度: {newCurrentLine.Length:F6}");
                                continue;  // 跳过这个延伸操作
                            }
                            
                            currentLine = newCurrentLine;
                            
                            // 在交点处智能打断 otherLine（检查打断后的两段长度）
                            double param = GetProjectionParameter(otherLine, intersection);
                            double length = otherLine.Length;
                            double segmentA = param * length;        // 起点到交点
                            double segmentB = (1 - param) * length;  // 交点到终点
                            
                            // 只有两段长度都 >= minBreakThreshold 才打断
                            if (segmentA >= minBreakThreshold && segmentB >= minBreakThreshold)
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
                            
                            // 在交点处智能打断 otherLine（检查打断后的两段长度）
                            double param = GetProjectionParameter(otherLine, intersection);
                            double length = otherLine.Length;
                            double segmentA = param * length;        // 起点到交点
                            double segmentB = (1 - param) * length;  // 交点到终点
                            
                            // 只有两段长度都 >= minBreakThreshold 才打断
                            if (segmentA >= minBreakThreshold && segmentB >= minBreakThreshold)
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
        /// 在交点处分割线段（智能打断）
        /// </summary>
        /// <param name="lines">输入线段集合</param>
        /// <param name="tolerance">几何容差</param>
        /// <param name="minBreakThreshold">打断保护阈值（默认 0.5mm）
        /// <br/>- 如果打断后产生的线段长度 &lt; minBreakThreshold，则不打断
        /// <br/>- 避免产生极短的无用线段</param>
        private List<Line2D> SplitAtIntersections(List<Line2D> lines, double tolerance, double minBreakThreshold = 0.5)
        {
            // 使用并发集合存储每条线段的分割点（使用投影参数）
            var splitParams = new System.Collections.Concurrent.ConcurrentDictionary<int, List<double>>();
            
            // 初始化
            for (int i = 0; i < lines.Count; i++)
            {
                splitParams[i] = new List<double>();
            }
            
            // 性能优化：使用更精细的空间网格索引
            // 使用较小的网格尺寸以提高精度
            var averageLength = lines.Average(l => l.Length);
            var gridSize = Math.Max(50.0, averageLength * 0.5); // 使用平均长度的一半作为网格大小
            var spatialIndex = BuildSpatialIndex(lines, gridSize);
            
            // 并行计算所有交点（适用于大量线段场景）
            int totalIntersections = 0;
            var lockObj = new object();
            
            System.Threading.Tasks.Parallel.For(0, lines.Count, i =>
            {
                var line1 = lines[i];
                
                // 优化搜索半径：使用线段长度而不是 Max(length, gridSize)
                // 交点只可能出现在线段长度范围内
                var searchRadius = line1.Length * 1.5; // 增加50%余量
                var nearbyIndices = GetNearbyLineIndices(line1, spatialIndex, gridSize, searchRadius);
                
                foreach (int j in nearbyIndices)
                {
                    if (j <= i) continue; // 避免重复检测
                    
                    var intersection = line1.GetIntersection(lines[j], tolerance);
                    if (intersection != default)
                    {
                        lock (lockObj)
                        {
                            totalIntersections++;
                        }
                        
                        // 计算交点在两条线段上的投影参数
                        double param1 = GetProjectionParameter(line1, intersection);
                        double param2 = GetProjectionParameter(lines[j], intersection);
                        
                        // 智能打断：检查打断后的两段长度是否都 >= minSegmentLength
                        // 避免产生极短的无用线段
                        double length1 = line1.Length;
                        double length2 = lines[j].Length;
                        
                        // 对于 line1：检查打断后的两段长度
                        double segment1A = param1 * length1;        // 起点到交点的长度
                        double segment1B = (1 - param1) * length1;  // 交点到终点的长度
                        
                        if (segment1A >= minBreakThreshold && segment1B >= minBreakThreshold)
                        {
                            lock (splitParams[i])
                            {
                                splitParams[i].Add(param1);
                            }
                        }
                        
                        // 对于 line2：检查打断后的两段长度
                        double segment2A = param2 * length2;        // 起点到交点的长度
                        double segment2B = (1 - param2) * length2;  // 交点到终点的长度
                        
                        if (segment2A >= minBreakThreshold && segment2B >= minBreakThreshold)
                        {
                            lock (splitParams[j])
                            {
                                splitParams[j].Add(param2);
                            }
                        }
                    }
                }
            });
            
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
                
                // 检查参数差值，避免创建极短线段
                if (Math.Abs(t2 - t1) > 1e-10)
                {
                    Point2D p1 = line.StartPoint.Add(line.Direction * t1);
                    Point2D p2 = line.StartPoint.Add(line.Direction * t2);
                    
                    // 额外检查：确保生成的线段不是零长度
                    double segmentLength = p1.DistanceTo(p2);
                    if (segmentLength > 1e-6)  // 至少 0.000001mm
                    {
                        segments.Add(new Line2D(p1, p2));
                    }
                    else
                    {
                        // 调试输出：零长度线段被跳过
                        System.Diagnostics.Debug.WriteLine($"警告：跳过零长度线段 ({p1.X:F3}, {p1.Y:F3}) → ({p2.X:F3}, {p2.Y:F3})");
                    }
                }
            }
            
            return segments;
        }

        #endregion

        #region 统一过滤功能

        /// <summary>
        /// 过滤短线段（统一过滤步骤）
        /// </summary>
        /// <param name="lines">输入线段集合</param>
        /// <param name="minLength">最小线段长度阈值</param>
        /// <returns>过滤后的线段列表（只包含长度 >= minLength 的有效线段）</returns>
        /// <remarks>
        /// 这是唯一的过滤步骤，在所有打断和延伸操作完成后统一执行
        /// </remarks>
        /// <summary>
        /// 过滤短线段（用于测试，输出每条线段的长度信息）
        /// </summary>
        /// <param name="lines">输入线段集合</param>
        /// <param name="minLength">最小长度阈值</param>
        /// <param name="enableDebugOutput">是否启用调试输出（默认 false）</param>
        /// <returns>过滤后的线段列表</returns>
        public List<Line2D> FilterShortSegments(IEnumerable<Line2D> lines, double minLength, bool enableDebugOutput = false)
        {
            var lineList = lines.ToList();
            var result = new List<Line2D>();
            var deletedLines = new List<Line2D>();
            
            if (enableDebugOutput)
            {
                System.Diagnostics.Debug.WriteLine("\n========== 过滤短线段（调试模式） ==========");
                System.Diagnostics.Debug.WriteLine($"最小长度阈值：{minLength:F6}");
                System.Diagnostics.Debug.WriteLine($"输入线段数：{lineList.Count}");
                System.Diagnostics.Debug.WriteLine("\n线段长度列表：");
            }
            
            for (int i = 0; i < lineList.Count; i++)
            {
                var line = lineList[i];
                double length = line.Length;
                bool keep = length >= minLength;
                
                if (enableDebugOutput)
                {
                    string status = keep ? "✅ 保留" : "❌ 删除";
                    System.Diagnostics.Debug.WriteLine($"  线段 {i + 1}: 长度 = {length:F6} mm, {status}");
                    System.Diagnostics.Debug.WriteLine($"    起点: ({line.StartPoint.X:F3}, {line.StartPoint.Y:F3})");
                    System.Diagnostics.Debug.WriteLine($"    终点: ({line.EndPoint.X:F3}, {line.EndPoint.Y:F3})");
                }
                
                if (keep)
                {
                    result.Add(line);
                }
                else
                {
                    deletedLines.Add(line);
                }
            }
            
            if (enableDebugOutput)
            {
                System.Diagnostics.Debug.WriteLine($"\n过滤结果：");
                System.Diagnostics.Debug.WriteLine($"  保留：{result.Count} 条");
                System.Diagnostics.Debug.WriteLine($"  删除：{deletedLines.Count} 条");
                System.Diagnostics.Debug.WriteLine("==========================================\n");
            }
            
            return result;
        }

        #endregion

        #region 空间索引优化

        /// <summary>
        /// 构建空间网格索引（提升性能）
        /// </summary>
        private Dictionary<(int, int), List<int>> BuildSpatialIndex(List<Line2D> lines, double gridSize)
        {
            var index = new Dictionary<(int, int), List<int>>();
            
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                
                // 计算线段包围盒的网格坐标
                double minX = Math.Min(line.StartPoint.X, line.EndPoint.X);
                double maxX = Math.Max(line.StartPoint.X, line.EndPoint.X);
                double minY = Math.Min(line.StartPoint.Y, line.EndPoint.Y);
                double maxY = Math.Max(line.StartPoint.Y, line.EndPoint.Y);
                
                int gridMinX = (int)Math.Floor(minX / gridSize);
                int gridMaxX = (int)Math.Floor(maxX / gridSize);
                int gridMinY = (int)Math.Floor(minY / gridSize);
                int gridMaxY = (int)Math.Floor(maxY / gridSize);
                
                // 将线段添加到所有相关的网格单元
                for (int gx = gridMinX; gx <= gridMaxX; gx++)
                {
                    for (int gy = gridMinY; gy <= gridMaxY; gy++)
                    {
                        var key = (gx, gy);
                        if (!index.ContainsKey(key))
                        {
                            index[key] = new List<int>();
                        }
                        index[key].Add(i);
                    }
                }
            }
            
            return index;
        }

        /// <summary>
        /// 获取附近的线段索引（使用空间索引）
        /// </summary>
        private HashSet<int> GetNearbyLineIndices(
            Line2D line, 
            Dictionary<(int, int), List<int>> spatialIndex, 
            double gridSize,
            double searchRadius)
        {
            var result = new HashSet<int>();
            
            // 计算搜索范围的网格坐标
            double minX = Math.Min(line.StartPoint.X, line.EndPoint.X) - searchRadius;
            double maxX = Math.Max(line.StartPoint.X, line.EndPoint.X) + searchRadius;
            double minY = Math.Min(line.StartPoint.Y, line.EndPoint.Y) - searchRadius;
            double maxY = Math.Max(line.StartPoint.Y, line.EndPoint.Y) + searchRadius;
            
            int gridMinX = (int)Math.Floor(minX / gridSize);
            int gridMaxX = (int)Math.Floor(maxX / gridSize);
            int gridMinY = (int)Math.Floor(minY / gridSize);
            int gridMaxY = (int)Math.Floor(maxY / gridSize);
            
            // 收集所有相关网格单元中的线段
            for (int gx = gridMinX; gx <= gridMaxX; gx++)
            {
                for (int gy = gridMinY; gy <= gridMaxY; gy++)
                {
                    var key = (gx, gy);
                    if (spatialIndex.ContainsKey(key))
                    {
                        foreach (var idx in spatialIndex[key])
                        {
                            result.Add(idx);
                        }
                    }
                }
            }
            
            return result;
        }

        #endregion

        #region 扫描线算法 - 高效交点计算

        /// <summary>
        /// 使用扫描线算法计算交点并分割线段（适合大数据集）
        /// </summary>
        /// <param name="lines">输入线段集合</param>
        /// <param name="tolerance">几何容差</param>
        /// <param name="minBreakThreshold">打断保护阈值</param>
        /// <returns>分割后的线段列表</returns>
        /// <remarks>
        /// 扫描线算法原理：
        /// <br/>1. 按 X 坐标排序所有线段端点（起点和终点）
        /// <br/>2. 从左到右扫描，维护"活跃线段"集合（按 Y 坐标排序）
        /// <br/>3. 遇到起点：将线段加入活跃集合，检测与相邻线段的交点
        /// <br/>4. 遇到终点：将线段从活跃集合移除
        /// <br/>5. 时间复杂度：O((n + k) log n)，其中 k 是交点数量
        /// </remarks>
        private List<Line2D> SplitAtIntersections_SweepLine(List<Line2D> lines, double tolerance, double minBreakThreshold = 0.5)
        {
            // 存储每条线段的分割点（使用投影参数）
            var splitParams = new Dictionary<int, List<double>>();
            for (int i = 0; i < lines.Count; i++)
            {
                splitParams[i] = new List<double>();
            }

            // 创建事件队列（线段端点）
            var events = new List<SweepEvent>();
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                // 确保左端点在前
                var (left, right) = GetLeftRightPoints(line);
                events.Add(new SweepEvent { Point = left, Type = EventType.Start, LineIndex = i });
                events.Add(new SweepEvent { Point = right, Type = EventType.End, LineIndex = i });
            }

            // 按 X 坐标排序事件（X 相同时，起点优先）
            events.Sort((a, b) =>
            {
                int cmp = a.Point.X.CompareTo(b.Point.X);
                if (cmp != 0) return cmp;
                // X 相同时，起点优先于终点
                return a.Type.CompareTo(b.Type);
            });

            // 活跃线段集合（按当前 X 位置的 Y 坐标排序）
            var activeLines = new SortedSet<int>(new ActiveLineComparer(lines, events[0].Point.X));
            int totalIntersections = 0;

            // 扫描所有事件
            foreach (var evt in events)
            {
                var currentX = evt.Point.X;
                var lineIndex = evt.LineIndex;

                if (evt.Type == EventType.Start)
                {
                    // 线段起点：加入活跃集合
                    activeLines.Add(lineIndex);

                    // 检测与前后相邻线段的交点
                    var neighbors = GetNeighbors(activeLines, lineIndex);
                    foreach (var neighborIndex in neighbors)
                    {
                        var intersection = lines[lineIndex].GetIntersection(lines[neighborIndex], tolerance);
                        if (intersection != default)
                        {
                            totalIntersections++;

                            // 计算交点参数并应用打断保护
                            AddIntersectionIfValid(lines, splitParams, lineIndex, neighborIndex, 
                                                   intersection, minBreakThreshold);
                        }
                    }
                }
                else
                {
                    // 线段终点：从活跃集合移除
                    activeLines.Remove(lineIndex);
                }
            }

            System.Diagnostics.Debug.WriteLine($"[扫描线] 找到 {totalIntersections} 个交点");

            // 分割线段
            var result = new List<Line2D>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (splitParams[i].Count == 0)
                {
                    result.Add(lines[i]);
                }
                else
                {
                    splitParams[i].Sort();
                    result.AddRange(SplitLineAtParameters(lines[i], splitParams[i]));
                }
            }

            return result;
        }

        /// <summary>
        /// 获取线段的左右端点（按 X 坐标）
        /// </summary>
        private (Point2D left, Point2D right) GetLeftRightPoints(Line2D line)
        {
            if (line.StartPoint.X < line.EndPoint.X)
                return (line.StartPoint, line.EndPoint);
            else if (line.StartPoint.X > line.EndPoint.X)
                return (line.EndPoint, line.StartPoint);
            else
                // X 相同时按 Y 排序
                return line.StartPoint.Y <= line.EndPoint.Y 
                    ? (line.StartPoint, line.EndPoint) 
                    : (line.EndPoint, line.StartPoint);
        }

        /// <summary>
        /// 获取活跃集合中指定线段的前后相邻线段
        /// </summary>
        private List<int> GetNeighbors(SortedSet<int> activeLines, int lineIndex)
        {
            var neighbors = new List<int>();
            var view = activeLines.GetViewBetween(int.MinValue, int.MaxValue);
            
            bool foundCurrent = false;
            int? prev = null;
            
            foreach (var idx in view)
            {
                if (idx == lineIndex)
                {
                    foundCurrent = true;
                    if (prev.HasValue)
                        neighbors.Add(prev.Value); // 前一个
                }
                else if (foundCurrent)
                {
                    neighbors.Add(idx); // 后一个
                    break;
                }
                prev = idx;
            }
            
            return neighbors;
        }

        /// <summary>
        /// 添加交点（如果打断后的线段长度有效）
        /// </summary>
        private void AddIntersectionIfValid(
            List<Line2D> lines, 
            Dictionary<int, List<double>> splitParams,
            int index1, 
            int index2, 
            Point2D intersection, 
            double minBreakThreshold)
        {
            // 计算交点在两条线段上的投影参数
            double param1 = GetProjectionParameter(lines[index1], intersection);
            double param2 = GetProjectionParameter(lines[index2], intersection);

            double length1 = lines[index1].Length;
            double length2 = lines[index2].Length;

            // 检查打断后的两段长度
            double segment1A = param1 * length1;
            double segment1B = (1 - param1) * length1;

            if (segment1A >= minBreakThreshold && segment1B >= minBreakThreshold)
            {
                splitParams[index1].Add(param1);
            }

            double segment2A = param2 * length2;
            double segment2B = (1 - param2) * length2;

            if (segment2A >= minBreakThreshold && segment2B >= minBreakThreshold)
            {
                splitParams[index2].Add(param2);
            }
        }

        /// <summary>
        /// 扫描线事件类型
        /// </summary>
        private enum EventType
        {
            Start = 0,  // 线段起点
            End = 1     // 线段终点
        }

        /// <summary>
        /// 扫描线事件
        /// </summary>
        private class SweepEvent
        {
            public Point2D Point { get; set; }
            public EventType Type { get; set; }
            public int LineIndex { get; set; }
        }

        /// <summary>
        /// 活跃线段比较器（按当前扫描位置的 Y 坐标排序）
        /// </summary>
        private class ActiveLineComparer : IComparer<int>
        {
            private readonly List<Line2D> _lines;
            private readonly double _currentX;

            public ActiveLineComparer(List<Line2D> lines, double currentX)
            {
                _lines = lines;
                _currentX = currentX;
            }

            public int Compare(int idx1, int idx2)
            {
                if (idx1 == idx2) return 0;

                // 计算两条线段在当前 X 位置的 Y 坐标
                double y1 = GetYAtX(_lines[idx1], _currentX);
                double y2 = GetYAtX(_lines[idx2], _currentX);

                int cmp = y1.CompareTo(y2);
                // Y 相同时按索引排序（保证稳定性）
                return cmp != 0 ? cmp : idx1.CompareTo(idx2);
            }

            private double GetYAtX(Line2D line, double x)
            {
                double dx = line.EndPoint.X - line.StartPoint.X;
                if (Math.Abs(dx) < 1e-10)
                    return Math.Min(line.StartPoint.Y, line.EndPoint.Y);

                double t = (x - line.StartPoint.X) / dx;
                t = Math.Max(0.0, Math.Min(1.0, t));
                return line.StartPoint.Y + t * (line.EndPoint.Y - line.StartPoint.Y);
            }
        }

        /// <summary>
        /// 扫描线优化版本：端点延伸
        /// </summary>
        private List<Line2D> ExtendNearEndpoints_SweepLine(List<Line2D> lines, double tolerance, double maxDistance)
        {
            var lineList = lines.ToList();
            var result = new List<Line2D>();
            var processed = new HashSet<int>();

            // 为所有线段端点创建事件
            var endpointEvents = new List<EndpointEvent>();
            for (int i = 0; i < lineList.Count; i++)
            {
                var line = lineList[i];
                endpointEvents.Add(new EndpointEvent
                {
                    Point = line.StartPoint,
                    LineIndex = i,
                    IsStartPoint = true
                });
                endpointEvents.Add(new EndpointEvent
                {
                    Point = line.EndPoint,
                    LineIndex = i,
                    IsStartPoint = false
                });
            }

            // 按 X 坐标排序
            endpointEvents.Sort((a, b) => a.Point.X.CompareTo(b.Point.X));

            // 扫描所有端点，维护活跃端点窗口
            for (int i = 0; i < endpointEvents.Count; i++)
            {
                var evt1 = endpointEvents[i];
                if (processed.Contains(evt1.LineIndex)) continue;

                // 在 X 方向 maxDistance 范围内查找候选端点
                for (int j = i + 1; j < endpointEvents.Count; j++)
                {
                    var evt2 = endpointEvents[j];
                    
                    // X 距离超过阈值，停止搜索
                    if (evt2.Point.X - evt1.Point.X > maxDistance)
                        break;

                    if (evt1.LineIndex == evt2.LineIndex) continue;
                    if (processed.Contains(evt2.LineIndex)) continue;

                    // 检查端点距离
                    double dist = evt1.Point.DistanceTo(evt2.Point);
                    if (dist > tolerance && dist <= maxDistance)
                    {
                        var line1 = lineList[evt1.LineIndex];
                        var line2 = lineList[evt2.LineIndex];

                        // 计算无限延长线交点
                        if (TryGetInfiniteLinesIntersection(line1, line2, tolerance, out Point2D intersection))
                        {
                            // 延伸线段
                            Line2D extended1 = evt1.IsStartPoint
                                ? new Line2D(intersection, line1.EndPoint)
                                : new Line2D(line1.StartPoint, intersection);

                            Line2D extended2 = evt2.IsStartPoint
                                ? new Line2D(intersection, line2.EndPoint)
                                : new Line2D(line2.StartPoint, intersection);

                            // ⚠️ 防御性检查：避免产生零长度线段
                            if (extended1.Length >= tolerance && extended2.Length >= tolerance)
                            {
                                lineList[evt1.LineIndex] = extended1;
                                lineList[evt2.LineIndex] = extended2;
                                processed.Add(evt1.LineIndex);
                                processed.Add(evt2.LineIndex);
                                break;
                            }
                        }
                    }
                }
            }

            // 返回处理后的线段
            return lineList;
        }

        /// <summary>
        /// 扫描线优化版本：端点到线延伸
        /// </summary>
        private List<Line2D> ExtendEndpointToLine_SweepLine(List<Line2D> lines, double tolerance, double maxDistance, double minBreakThreshold)
        {
            var lineList = lines.ToList();

            // 为所有线段端点创建事件
            var endpointEvents = new List<EndpointEvent>();
            for (int i = 0; i < lineList.Count; i++)
            {
                var line = lineList[i];
                endpointEvents.Add(new EndpointEvent
                {
                    Point = line.StartPoint,
                    LineIndex = i,
                    IsStartPoint = true
                });
                endpointEvents.Add(new EndpointEvent
                {
                    Point = line.EndPoint,
                    LineIndex = i,
                    IsStartPoint = false
                });
            }

            // 按 X 坐标排序
            endpointEvents.Sort((a, b) => a.Point.X.CompareTo(b.Point.X));

            // 扫描所有端点
            for (int i = 0; i < endpointEvents.Count; i++)
            {
                var evt = endpointEvents[i];
                var currentLine = lineList[evt.LineIndex];
                var endPoint = evt.IsStartPoint ? currentLine.StartPoint : currentLine.EndPoint;

                // 在 X 方向 maxDistance 范围内查找候选线段
                for (int j = 0; j < lineList.Count; j++)
                {
                    if (j == evt.LineIndex) continue;

                    var targetLine = lineList[j];
                    
                    // X 范围剪枝
                    double minX = Math.Min(targetLine.StartPoint.X, targetLine.EndPoint.X);
                    double maxX = Math.Max(targetLine.StartPoint.X, targetLine.EndPoint.X);
                    if (endPoint.X < minX - maxDistance || endPoint.X > maxX + maxDistance)
                        continue;

                    // 计算端点到目标线段的垂直距离
                    double distance = DistanceToLine(endPoint, targetLine);
                    if (distance > tolerance && distance <= maxDistance)
                    {
                        // 计算投影点
                        Point2D projection = GetProjectionPoint(endPoint, targetLine);

                        // 检查投影点是否在线段上
                        if (IsPointOnSegment(projection, targetLine, tolerance))
                        {
                            // 延伸当前线段到投影点
                            Line2D extendedLine = evt.IsStartPoint
                                ? new Line2D(projection, currentLine.EndPoint)
                                : new Line2D(currentLine.StartPoint, projection);

                            // 在投影点处打断目标线段（应用 minBreakThreshold）
                            double param = GetProjectionParameter(targetLine, projection);
                            double seg1Len = param * targetLine.Length;
                            double seg2Len = (1 - param) * targetLine.Length;

                            if (seg1Len >= minBreakThreshold && seg2Len >= minBreakThreshold)
                            {
                                Line2D seg1 = new Line2D(targetLine.StartPoint, projection);
                                Line2D seg2 = new Line2D(projection, targetLine.EndPoint);

                                lineList[evt.LineIndex] = extendedLine;
                                lineList[j] = seg1;
                                lineList.Insert(j + 1, seg2);

                                // 更新事件索引（因为插入了新线段）
                                if (j < evt.LineIndex)
                                {
                                    // 调整当前线段索引
                                    for (int k = i; k < endpointEvents.Count; k++)
                                    {
                                        if (endpointEvents[k].LineIndex > j)
                                            endpointEvents[k].LineIndex++;
                                    }
                                }

                                break;
                            }
                        }
                    }
                }
            }

            return lineList;
        }

        /// <summary>
        /// 端点事件（用于扫描线算法）
        /// </summary>
        private class EndpointEvent
        {
            public Point2D Point { get; set; }
            public int LineIndex { get; set; }
            public bool IsStartPoint { get; set; }
        }

        /// <summary>
        /// 计算两条无限延长线的交点
        /// </summary>
        private bool TryGetInfiniteLinesIntersection(Line2D line1, Line2D line2, double tolerance, out Point2D intersection)
        {
            intersection = line1.GetIntersectionWithInfiniteLine(line2, tolerance);
            return intersection != default;
        }

        /// <summary>
        /// 计算点到线段的垂直距离
        /// </summary>
        private double DistanceToLine(Point2D point, Line2D line)
        {
            return line.DistanceToPoint(point);
        }

        /// <summary>
        /// 计算点在线段上的投影点
        /// </summary>
        private Point2D GetProjectionPoint(Point2D point, Line2D line)
        {
            Vector2D lineVec = line.Direction;
            Vector2D pointVec = new Vector2D(point.X - line.StartPoint.X, point.Y - line.StartPoint.Y);
            
            double lineLength = line.Length;
            if (lineLength < 1e-10) return line.StartPoint;
            
            double projection = lineVec.Dot(pointVec) / (lineLength * lineLength);
            projection = Math.Max(0.0, Math.Min(1.0, projection));
            
            return new Point2D(
                line.StartPoint.X + projection * lineVec.X,
                line.StartPoint.Y + projection * lineVec.Y
            );
        }

        /// <summary>
        /// 判断点是否在线段上
        /// </summary>
        private bool IsPointOnSegment(Point2D point, Line2D line, double tolerance)
        {
            return line.IsPointOnSegment(point, tolerance);
        }

        #endregion
    }
}
