using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 曲线线段提取器实现（Curve Segment Extractor Implementation）
    /// 将 AutoCAD 曲线对象转换为平台无关的 Line2D 线段
    /// </summary>
    public class CurveSegmentExtractor : ICurveSegmentExtractor
    {
        /// <summary>
        /// 从 Curve 对象提取线段
        /// 重要：与原代码保持一致，只提取 StartPoint 和 EndPoint
        /// 原代码逻辑：var start = curve.StartPoint; var end = curve.EndPoint;
        /// 不提取多段线的中间顶点！
        /// </summary>
        public Line2D[] ExtractFromCurve(Curve curve, double tolerance)
        {
            if (curve == null)
                return Array.Empty<Line2D>();

            // 重要：与原代码保持一致，所有曲线都只提取起点和终点
            // 不管是 Line, Polyline, Arc 还是其他类型，都只取首尾两个点
            var start = new Point2D(curve.StartPoint.X, curve.StartPoint.Y);
            var end = new Point2D(curve.EndPoint.X, curve.EndPoint.Y);
            
            return new[] { new Line2D(start, end) };
        }

        /// <summary>
        /// 从 ObjectId 集合提取所有线段
        /// </summary>
        public List<Line2D> ExtractSegments(IEnumerable<ObjectId> curveIds, double tolerance)
        {
            var segments = new List<Line2D>();
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return segments;

            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var id in curveIds)
                {
                    try
                    {
                        var entity = tr.GetObject(id, OpenMode.ForRead);
                        if (entity is Curve curve)
                        {
                            var curveSegments = ExtractFromCurve(curve, tolerance);
                            segments.AddRange(curveSegments);
                        }
                    }
                    catch (Exception)
                    {
                        // 忽略无法处理的对象
                        continue;
                    }
                }
                tr.Commit();
            }

            return segments;
        }

        /// <summary>
        /// 从 Line 对象提取线段（直线）
        /// </summary>
        private Line2D[] ExtractFromLine(Line line)
        {
            var start = new Point2D(line.StartPoint.X, line.StartPoint.Y);
            var end = new Point2D(line.EndPoint.X, line.EndPoint.Y);
            
            return new[] { new Line2D(start, end) };
        }

        /// <summary>
        /// 从 Polyline 对象提取线段（多段线）
        /// 当前仅提取直线段（不处理bulge圆弧段）
        /// </summary>
        private Line2D[] ExtractFromPolyline(Polyline pline)
        {
            var segments = new List<Line2D>();
            int numSegments = pline.Closed ? pline.NumberOfVertices : pline.NumberOfVertices - 1;

            for (int i = 0; i < numSegments; i++)
            {
                int nextIndex = (i + 1) % pline.NumberOfVertices;
                
                var p1 = pline.GetPoint2dAt(i);
                var p2 = pline.GetPoint2dAt(nextIndex);
                
                var start = new Point2D(p1.X, p1.Y);
                var end = new Point2D(p2.X, p2.Y);
                
                // 当前仅处理直线段，忽略 bulge（圆弧段）
                // 未来扩展：if (Math.Abs(pline.GetBulgeAt(i)) > 1e-6) { 离散化圆弧段 }
                
                segments.Add(new Line2D(start, end));
            }

            return segments.ToArray();
        }

        /// <summary>
        /// 从 Polyline2d 对象提取线段（2D多段线）
        /// </summary>
        private Line2D[] ExtractFromPolyline2d(Polyline2d pline2d)
        {
            var segments = new List<Line2D>();
            
            using (var tr = pline2d.Database.TransactionManager.StartTransaction())
            {
                Point2D? prevPoint = null;
                Point2D? firstPoint = null;

                foreach (ObjectId vId in pline2d)
                {
                    var vertex = tr.GetObject(vId, OpenMode.ForRead) as Vertex2d;
                    if (vertex == null)
                        continue;

                    var currentPoint = new Point2D(vertex.Position.X, vertex.Position.Y);

                    if (prevPoint == null)
                    {
                        firstPoint = currentPoint;
                    }
                    else
                    {
                        segments.Add(new Line2D(prevPoint.Value, currentPoint));
                    }

                    prevPoint = currentPoint;
                }

                // 如果多段线是闭合的，连接最后一点和第一点
                if (pline2d.Closed && prevPoint.HasValue && firstPoint.HasValue)
                {
                    segments.Add(new Line2D(prevPoint.Value, firstPoint.Value));
                }

                tr.Commit();
            }

            return segments.ToArray();
        }

        // 未来扩展：圆弧离散化
        // private Line2D[] ExtractFromArc(Arc arc, double tolerance)
        // {
        //     // 实现圆弧离散化算法
        //     // 根据 tolerance 计算需要的段数
        //     // 沿圆弧采样点并生成 Line2D 线段
        // }

        // 未来扩展：圆离散化
        // private Line2D[] ExtractFromCircle(Circle circle, double tolerance)
        // {
        //     // 实现圆离散化算法
        // }

        // 未来扩展：样条曲线离散化
        // private Line2D[] ExtractFromSpline(Spline spline, double tolerance)
        // {
        //     // 实现样条曲线离散化算法
        // }
    }
}

