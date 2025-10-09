using HyCADTool.Refactored.Domain.Services.GeometryAlgorithms;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Domain.Services
{
    /// <summary>
    /// OverKill 功能的领域服务
    /// 提供线段重叠合并、独立端点查找、端点延伸等核心算法
    /// </summary>
    public class LineOverKillService
    {
        /// <summary>
        /// 合并重叠的线段
        /// </summary>
        /// <param name="lines">输入线段集合</param>
        /// <param name="tolerance">几何容差</param>
        /// <returns>合并后的线段列表</returns>
        public List<Line2D> MergeOverlappingLines(
            IEnumerable<Line2D> lines, 
            double tolerance)
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
                    
                    if (CheckAndMergeOverlap(current, lineList[j], tolerance, out Line2D merged))
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
        /// 检查两条线段是否重叠，如果重叠则返回合并后的线段
        /// </summary>
        private bool CheckAndMergeOverlap(
            Line2D line1, 
            Line2D line2, 
            double tolerance,
            out Line2D merged)
        {
            merged = default;
            
            // 检查是否共线
            if (!line1.IsCollinear(line2, tolerance))
                return false;
            
            // 检查是否有重叠部分
            // 使用 LineAlgorithms.CheckOverlap 已有的算法
            var overlapLine = LineAlgorithms.CheckOverlap(line1, line2, tolerance);
            if (overlapLine == default)
                return false;
            
            // 找到所有端点中的最远两个点
            var points = new[] { 
                line1.StartPoint, line1.EndPoint, 
                line2.StartPoint, line2.EndPoint 
            };
            
            // 计算沿着线段方向的投影距离
            Vector2D direction = line1.Direction.Normalize();
            var projections = points
                .Select(p => new { Point = p, Proj = direction.Dot(new Vector2D(p.X - line1.StartPoint.X, p.Y - line1.StartPoint.Y)) })
                .OrderBy(x => x.Proj)
                .ToList();
            
            merged = new Line2D(projections.First().Point, projections.Last().Point);
            return true;
        }
        
        /// <summary>
        /// 查找独立端点（没有其他线段连接的端点）
        /// </summary>
        /// <param name="lines">线段集合</param>
        /// <param name="tolerance">几何容差</param>
        /// <returns>独立端点列表（线段, 是否为起点）</returns>
        public List<(Line2D Line, bool IsStartPoint)> FindIndependentEndpoints(
            IEnumerable<Line2D> lines,
            double tolerance)
        {
            var lineList = lines.ToList();
            var result = new List<(Line2D, bool)>();
            
            for (int i = 0; i < lineList.Count; i++)
            {
                Line2D current = lineList[i];
                Point2D start = current.StartPoint;
                Point2D end = current.EndPoint;
                
                // 检查起点是否独立
                bool startIsIndependent = true;
                for (int j = 0; j < lineList.Count; j++)
                {
                    if (j == i) continue;
                    
                    if (start.DistanceTo(lineList[j].StartPoint) < tolerance ||
                        start.DistanceTo(lineList[j].EndPoint) < tolerance)
                    {
                        startIsIndependent = false;
                        break;
                    }
                }
                
                // 检查终点是否独立
                bool endIsIndependent = true;
                for (int j = 0; j < lineList.Count; j++)
                {
                    if (j == i) continue;
                    
                    if (end.DistanceTo(lineList[j].StartPoint) < tolerance ||
                        end.DistanceTo(lineList[j].EndPoint) < tolerance)
                    {
                        endIsIndependent = false;
                        break;
                    }
                }
                
                // 添加独立端点
                if (startIsIndependent)
                    result.Add((current, true));
                if (endIsIndependent)
                    result.Add((current, false));
            }
            
            return result;
        }
        
        /// <summary>
        /// 延长线段到与其他线段的交点
        /// </summary>
        /// <param name="line">要延长的线段</param>
        /// <param name="extendFromStart">是否从起点延长</param>
        /// <param name="otherLines">其他线段集合</param>
        /// <param name="extensionDistance">延伸距离</param>
        /// <param name="tolerance">几何容差</param>
        /// <returns>(延长后的线段, 是否找到交点, 交点坐标)</returns>
        public (Line2D ExtendedLine, bool Found, Point2D Intersection) 
            ExtendToIntersection(
                Line2D line,
                bool extendFromStart,
                IEnumerable<Line2D> otherLines,
                double extensionDistance,
                double tolerance)
        {
            Point2D freeEnd = extendFromStart ? line.StartPoint : line.EndPoint;
            Point2D fixedEnd = extendFromStart ? line.EndPoint : line.StartPoint;
            
            // 计算延伸方向
            Vector2D direction = line.Direction.Normalize();
            if (extendFromStart)
                direction = direction * -1; // 反向
            
            // 创建延伸线段
            Point2D extendedPoint = freeEnd.Add(direction * extensionDistance);
            Line2D extensionLine = new Line2D(freeEnd, extendedPoint);
            
            // 查找与其他线段的交点
            foreach (var other in otherLines)
            {
                Point2D intersection = extensionLine.GetIntersection(other, tolerance);
                if (intersection != default)
                {
                    // 找到交点，创建延伸后的线段
                    Line2D extended = extendFromStart 
                        ? new Line2D(intersection, fixedEnd)
                        : new Line2D(fixedEnd, intersection);
                    
                    return (extended, true, intersection);
                }
            }
            
            // 没有找到交点
            return (line, false, default);
        }
    }
}

