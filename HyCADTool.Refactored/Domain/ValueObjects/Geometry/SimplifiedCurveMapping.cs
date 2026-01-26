using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Domain.ValueObjects.Geometry
{
    /// <summary>
    /// 简化曲线映射 - 记录原始曲线和简化后线段的对应关系
    /// </summary>
    public class SimplifiedCurveMapping
    {
        /// <summary>
        /// 原始曲线类型
        /// </summary>
        public CurveSegmentType OriginalType { get; }
        
        /// <summary>
        /// 原始Arc（如果是Arc类型）
        /// </summary>
        public Arc2D? OriginalArc { get; }
        
        /// <summary>
        /// 原始Ellipse（如果是Ellipse类型）
        /// </summary>
        public Ellipse2D? OriginalEllipse { get; }
        
        /// <summary>
        /// 原始Spline（如果是Spline类型）
        /// </summary>
        public Spline2D? OriginalSpline { get; }
        
        /// <summary>
        /// 简化后的线段列表（按顺序）
        /// </summary>
        public List<Line2D> SimplifiedSegments { get; }
        
        /// <summary>
        /// 构造函数 - Arc映射
        /// </summary>
        public SimplifiedCurveMapping(Arc2D arc, List<Line2D> simplifiedSegments)
        {
            if (simplifiedSegments == null || simplifiedSegments.Count == 0)
                throw new ArgumentException("简化线段不能为空", nameof(simplifiedSegments));
                
            OriginalType = CurveSegmentType.Arc;
            OriginalArc = arc;
            SimplifiedSegments = new List<Line2D>(simplifiedSegments);
        }
        
        /// <summary>
        /// 构造函数 - Ellipse映射
        /// </summary>
        public SimplifiedCurveMapping(Ellipse2D ellipse, List<Line2D> simplifiedSegments)
        {
            if (simplifiedSegments == null || simplifiedSegments.Count == 0)
                throw new ArgumentException("简化线段不能为空", nameof(simplifiedSegments));
                
            OriginalType = CurveSegmentType.Ellipse;
            OriginalEllipse = ellipse;
            SimplifiedSegments = new List<Line2D>(simplifiedSegments);
        }
        
        /// <summary>
        /// 构造函数 - Spline映射
        /// </summary>
        public SimplifiedCurveMapping(Spline2D spline, List<Line2D> simplifiedSegments)
        {
            if (simplifiedSegments == null || simplifiedSegments.Count == 0)
                throw new ArgumentException("简化线段不能为空", nameof(simplifiedSegments));
                
            OriginalType = CurveSegmentType.Spline;
            OriginalSpline = spline;
            SimplifiedSegments = new List<Line2D>(simplifiedSegments);
        }
        
        /// <summary>
        /// 判断给定线段是否在简化线段列表中
        /// </summary>
        /// <param name="line">待判断的线段</param>
        /// <param name="tolerance">容差</param>
        /// <returns>匹配的索引（-1表示不匹配）</returns>
        public int FindSegmentIndex(Line2D line, double tolerance = 1e-3)
        {
            for (int i = 0; i < SimplifiedSegments.Count; i++)
            {
                var seg = SimplifiedSegments[i];
                
                // 正向匹配
                if (seg.StartPoint.DistanceTo(line.StartPoint) < tolerance &&
                    seg.EndPoint.DistanceTo(line.EndPoint) < tolerance)
                {
                    return i;
                }
                
                // 反向匹配
                if (seg.StartPoint.DistanceTo(line.EndPoint) < tolerance &&
                    seg.EndPoint.DistanceTo(line.StartPoint) < tolerance)
                {
                    return i;
                }
            }
            
            return -1;
        }
        
        /// <summary>
        /// 判断给定线段序列是否匹配简化线段的子序列
        /// </summary>
        /// <param name="lines">待判断的线段序列</param>
        /// <param name="tolerance">容差</param>
        /// <returns>匹配的起始索引和长度（null表示不匹配）</returns>
        public (int startIndex, int length)? FindSubSequence(List<Line2D> lines, double tolerance = 1e-3)
        {
            if (lines == null || lines.Count == 0)
                return null;
            
            // 在简化线段中查找连续子序列
            for (int i = 0; i <= SimplifiedSegments.Count - lines.Count; i++)
            {
                bool matched = true;
                
                // 正向匹配
                for (int j = 0; j < lines.Count; j++)
                {
                    var seg = SimplifiedSegments[i + j];
                    var line = lines[j];
                    
                    if (seg.StartPoint.DistanceTo(line.StartPoint) >= tolerance ||
                        seg.EndPoint.DistanceTo(line.EndPoint) >= tolerance)
                    {
                        matched = false;
                        break;
                    }
                }
                
                if (matched)
                    return (i, lines.Count);
                
                // 反向匹配
                matched = true;
                for (int j = 0; j < lines.Count; j++)
                {
                    var seg = SimplifiedSegments[i + j];
                    var line = lines[lines.Count - 1 - j];
                    
                    if (seg.StartPoint.DistanceTo(line.EndPoint) >= tolerance ||
                        seg.EndPoint.DistanceTo(line.StartPoint) >= tolerance)
                    {
                        matched = false;
                        break;
                    }
                }
                
                if (matched)
                    return (i, lines.Count);
            }
            
            return null;
        }
        
        /// <summary>
        /// 获取原始曲线的起点
        /// </summary>
        public Point2D GetStartPoint()
        {
            return SimplifiedSegments.First().StartPoint;
        }
        
        /// <summary>
        /// 获取原始曲线的终点
        /// </summary>
        public Point2D GetEndPoint()
        {
            return SimplifiedSegments.Last().EndPoint;
        }
    }
}
























