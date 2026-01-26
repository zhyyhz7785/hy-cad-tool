using System;
using System.Collections.Generic;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;

namespace HyCADTool.Refactored.Domain.Services
{
    /// <summary>
    /// 曲线简化服务 - 将复杂曲线简化为连续线段
    /// </summary>
    public class CurveSimplificationService
    {
        /// <summary>
        /// 简化Arc为连续线段
        /// </summary>
        /// <param name="arc">原始圆弧</param>
        /// <param name="segmentCount">分段数量（默认：根据圆弧角度自动计算）</param>
        /// <returns>简化后的线段列表</returns>
        public List<Line2D> SimplifyArc(Arc2D arc, int? segmentCount = null)
        {
            var segments = new List<Line2D>();
            
            // 自动计算分段数：每15度一段，最少4段
            int count = segmentCount ?? Math.Max(4, (int)Math.Ceiling(Math.Abs(arc.SweepAngle) / 15.0));
            
            var startAngle = arc.StartAngle;
            var endAngle = arc.EndAngle;
            var angleStep = (endAngle - startAngle) / count;
            
            for (int i = 0; i < count; i++)
            {
                Point2D p1, p2;
                
                // 关键修复：第一个点必须精确等于arc.StartPoint，最后一个点必须精确等于arc.EndPoint
                if (i == 0)
                {
                    p1 = arc.StartPoint; // 使用原始起点
                }
                else
                {
                    var angle1 = startAngle + angleStep * i;
                    p1 = new Point2D(
                        arc.Center.X + arc.Radius * Math.Cos(angle1 * Math.PI / 180.0),
                        arc.Center.Y + arc.Radius * Math.Sin(angle1 * Math.PI / 180.0));
                }
                
                if (i == count - 1)
                {
                    p2 = arc.EndPoint; // 使用原始终点
                }
                else
                {
                    var angle2 = startAngle + angleStep * (i + 1);
                    p2 = new Point2D(
                        arc.Center.X + arc.Radius * Math.Cos(angle2 * Math.PI / 180.0),
                        arc.Center.Y + arc.Radius * Math.Sin(angle2 * Math.PI / 180.0));
                }
                
                segments.Add(new Line2D(p1, p2));
            }
            
            return segments;
        }
        
        /// <summary>
        /// 简化Ellipse为连续线段
        /// </summary>
        /// <param name="ellipse">原始椭圆</param>
        /// <param name="segmentCount">分段数量（默认：根据椭圆周长估算）</param>
        /// <returns>简化后的线段列表</returns>
        public List<Line2D> SimplifyEllipse(Ellipse2D ellipse, int? segmentCount = null)
        {
            var segments = new List<Line2D>();
            
            // 自动计算分段数：根据椭圆周长，最少16段
            int count = segmentCount ?? Math.Max(16, (int)Math.Ceiling(
                Math.PI * (ellipse.MajorRadius + ellipse.MinorRadius) / 50.0));
            
            var startParam = ellipse.StartParam;
            var endParam = ellipse.EndParam;
            var paramStep = (endParam - startParam) / count;
            
            for (int i = 0; i < count; i++)
            {
                var param1 = startParam + paramStep * i;
                var param2 = startParam + paramStep * (i + 1);
                
                var p1 = GetEllipsePoint(ellipse, param1);
                var p2 = GetEllipsePoint(ellipse, param2);
                
                segments.Add(new Line2D(p1, p2));
            }
            
            return segments;
        }
        
        /// <summary>
        /// 简化Spline为连续线段
        /// </summary>
        /// <param name="spline">原始样条曲线</param>
        /// <param name="segmentCount">分段数量（默认：根据控制点数量估算）</param>
        /// <returns>简化后的线段列表</returns>
        public List<Line2D> SimplifySpline(Spline2D spline, int? segmentCount = null)
        {
            var segments = new List<Line2D>();
            
            // 自动计算分段数：每个控制点区间8段，最少16段
            int count = segmentCount ?? Math.Max(16, (spline.ControlPoints?.Length ?? 2) * 8);
            
            var points = SampleSpline(spline, count);
            
            for (int i = 0; i < points.Count - 1; i++)
            {
                segments.Add(new Line2D(points[i], points[i + 1]));
            }
            
            return segments;
        }
        
        /// <summary>
        /// 获取椭圆上的点
        /// </summary>
        private Point2D GetEllipsePoint(Ellipse2D ellipse, double parameter)
        {
            // 椭圆参数方程
            double x = ellipse.MajorRadius * Math.Cos(parameter);
            double y = ellipse.MinorRadius * Math.Sin(parameter);
            
            // 旋转变换
            double cosR = Math.Cos(ellipse.Rotation);
            double sinR = Math.Sin(ellipse.Rotation);
            
            double rotatedX = x * cosR - y * sinR;
            double rotatedY = x * sinR + y * cosR;
            
            return new Point2D(
                ellipse.Center.X + rotatedX,
                ellipse.Center.Y + rotatedY);
        }
        
        /// <summary>
        /// 采样Spline曲线
        /// </summary>
        private List<Point2D> SampleSpline(Spline2D spline, int sampleCount)
        {
            var points = new List<Point2D>();
            
            // 简化版本：使用控制点作为近似（实际应使用B样条曲线算法）
            // 这里假设控制点已经足够密集，可以直接连接
            if (spline.ControlPoints == null || spline.ControlPoints.Length < 3)
            {
                // 控制点太少，直接返回控制点
                if (spline.ControlPoints != null)
                {
                    return new List<Point2D>(spline.ControlPoints);
                }
                return points;
            }
            
            // 均匀采样
            var step = (double)(spline.ControlPoints.Length - 1) / (sampleCount - 1);
            for (int i = 0; i < sampleCount; i++)
            {
                var t = i * step;
                var index = (int)Math.Floor(t);
                var fraction = t - index;
                
                if (index >= spline.ControlPoints.Length - 1)
                {
                    points.Add(spline.ControlPoints[spline.ControlPoints.Length - 1]);
                }
                else
                {
                    // 线性插值
                    var p1 = spline.ControlPoints[index];
                    var p2 = spline.ControlPoints[index + 1];
                    points.Add(new Point2D(
                        p1.X + (p2.X - p1.X) * fraction,
                        p1.Y + (p2.Y - p1.Y) * fraction));
                }
            }
            
            return points;
        }
    }
}

