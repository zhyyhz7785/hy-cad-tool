using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using HyCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Interfaces;
using System;
using System.Collections.Generic;

namespace HyCADTool.Shared.AutoCAD.Services
{
    /// <summary>
    /// 曲线线段提取器实现（Curve Segment Extractor Implementation）
    /// 将 AutoCAD 曲线对象转换为平台无关的 Line2D 线段，
    /// 曲线（Arc/Circle/Ellipse/Spline/多段线弧段）简化为连续线段并记录原始曲线映射。
    /// </summary>
    public class CurveSegmentExtractor : ICurveSegmentExtractor
    {
        /// <summary>
        /// 提取并简化所有曲线为线段，同时建立映射字典
        /// </summary>
        /// <param name="curveIds">曲线对象ID集合</param>
        /// <param name="simplificationService">曲线简化服务</param>
        /// <param name="tolerance">容差</param>
        /// <param name="arcSegmentCount">Arc简化分段数（null=自动）</param>
        /// <param name="ellipseSegmentCount">Ellipse简化分段数（null=自动）</param>
        /// <param name="splineSegmentCount">Spline简化分段数（null=自动）</param>
        /// <returns>简化后的线段列表 + 映射字典</returns>
        public (List<Line2D> segments, List<SimplifiedCurveMapping> mappings) ExtractAndSimplify(
            IEnumerable<ObjectId> curveIds,
            HyCAD.Geometry.Algorithms.CurveSimplificationService simplificationService,
            double tolerance,
            int? arcSegmentCount = null,
            int? ellipseSegmentCount = null,
            int? splineSegmentCount = null)
        {
            var allSegments = new List<Line2D>();
            var mappings = new List<SimplifiedCurveMapping>();

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return (allSegments, mappings);

            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var id in curveIds)
                {
                    if (!id.IsValid)
                        continue;

                    try
                    {
                        var entity = tr.GetObject(id, OpenMode.ForRead);
                        if (entity == null)
                            continue;

                        if (entity is Line line)
                        {
                            // 直线直接添加，不需要简化
                            var start = new Point2D(line.StartPoint.X, line.StartPoint.Y);
                            var end = new Point2D(line.EndPoint.X, line.EndPoint.Y);
                            allSegments.Add(new Line2D(start, end));
                        }
                        else if (entity is Polyline polyline)
                        {
                            // Polyline逐段处理（直线段直接加，弧段简化）
                            ProcessPolylineWithSimplification(
                                polyline,
                                simplificationService,
                                arcSegmentCount,
                                allSegments,
                                mappings);
                        }
                        else if (entity is Arc arc)
                        {
                            // Arc简化
                            var arc2d = ToArc2D(arc);
                            var simplified = simplificationService.SimplifyArc(arc2d, arcSegmentCount);

                            allSegments.AddRange(simplified);
                            mappings.Add(new SimplifiedCurveMapping(arc2d, simplified));
                        }
                        else if (entity is Circle circle)
                        {
                            // Circle拆为两个半圆Arc：
                            // 整圆若按单条 Arc2D(0, 2π) 恢复，bulge = tan(π/2) 无穷大且只有1个顶点，
                            // 拆半圆后每段 bulge = tan(π/4) = 1，可正常以多段线 bulge 段恢复。
                            var center = new Point2D(circle.Center.X, circle.Center.Y);
                            var radius = circle.Radius;

                            var upperHalf = new Arc2D(center, radius, 0, Math.PI);
                            var lowerHalf = new Arc2D(center, radius, Math.PI, 2.0 * Math.PI);

                            foreach (var half in new[] { upperHalf, lowerHalf })
                            {
                                var simplified = simplificationService.SimplifyArc(half, arcSegmentCount);
                                allSegments.AddRange(simplified);
                                mappings.Add(new SimplifiedCurveMapping(half, simplified));
                            }
                        }
                        else if (entity is Ellipse ellipse)
                        {
                            // Ellipse简化
                            var ellipse2d = ToEllipse2D(ellipse);
                            var simplified = simplificationService.SimplifyEllipse(ellipse2d, ellipseSegmentCount);

                            allSegments.AddRange(simplified);
                            mappings.Add(new SimplifiedCurveMapping(ellipse2d, simplified));
                        }
                        else if (entity is Spline spline)
                        {
                            // Spline简化
                            var spline2d = ToSpline2D(spline);
                            var simplified = simplificationService.SimplifySpline(spline2d, splineSegmentCount);

                            allSegments.AddRange(simplified);
                            mappings.Add(new SimplifiedCurveMapping(spline2d, simplified));
                        }
                        else if (entity is Polyline2d polyline2d)
                        {
                            // 2D重多段线：按顶点折线提取
                            ExtractPolyline2dSegments(polyline2d, tr, allSegments);
                        }
                        else if (entity is Polyline3d polyline3d)
                        {
                            // 3D多段线：按顶点折线提取（投影到XY平面）
                            ExtractPolyline3dSegments(polyline3d, tr, allSegments);
                        }
                        else if (entity is Curve curve)
                        {
                            // 其他曲线类型：退化为首尾直线
                            var start = new Point2D(curve.StartPoint.X, curve.StartPoint.Y);
                            var end = new Point2D(curve.EndPoint.X, curve.EndPoint.Y);
                            allSegments.Add(new Line2D(start, end));
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

            return (allSegments, mappings);
        }

        /// <summary>
        /// 处理Polyline：提取所有顶点段，对弧段简化
        /// </summary>
        private void ProcessPolylineWithSimplification(
            Polyline polyline,
            HyCAD.Geometry.Algorithms.CurveSimplificationService simplificationService,
            int? arcSegmentCount,
            List<Line2D> allSegments,
            List<SimplifiedCurveMapping> mappings)
        {
            int numSegments = polyline.Closed ? polyline.NumberOfVertices : polyline.NumberOfVertices - 1;

            for (int i = 0; i < numSegments; i++)
            {
                int nextIndex = (i + 1) % polyline.NumberOfVertices;

                var bulge = polyline.GetBulgeAt(i);
                var start = polyline.GetPoint2dAt(i);
                var end = polyline.GetPoint2dAt(nextIndex);

                var startPoint = new Point2D(start.X, start.Y);
                var endPoint = new Point2D(end.X, end.Y);

                if (Math.Abs(bulge) < 1e-10)
                {
                    // 直线段
                    allSegments.Add(new Line2D(startPoint, endPoint));
                }
                else
                {
                    // 弧段：简化为连续线段
                    var arc = BulgeToArc2D(startPoint, endPoint, bulge);
                    var simplified = simplificationService.SimplifyArc(arc, arcSegmentCount);

                    allSegments.AddRange(simplified);
                    mappings.Add(new SimplifiedCurveMapping(arc, simplified));
                }
            }
        }

        /// <summary>
        /// 从 bulge 值计算圆弧参数（bulge = tan(angle/4)，angle 为扫掠角）。
        /// 返回值统一规范化为逆时针（CCW）表示：bulge &lt; 0（顺时针弧）时交换起止角，
        /// 保证 Arc2D.SweepAngle 始终为该弧的真实扫掠角而非补弧。
        /// </summary>
        private Arc2D BulgeToArc2D(Point2D start, Point2D end, double bulge)
        {
            // 弦长
            double chordLength = start.DistanceTo(end);

            // 圆弧扫掠角
            double sweepAngle = 4.0 * Math.Atan(Math.Abs(bulge));

            // 半径
            double radius = chordLength / (2.0 * Math.Sin(sweepAngle / 2.0));

            // 弦中点
            double midX = (start.X + end.X) / 2.0;
            double midY = (start.Y + end.Y) / 2.0;

            // 弦方向向量
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;

            // 左侧法向（弦方向逆时针旋转90度）
            double perpX = -dy / chordLength;
            double perpY = dx / chordLength;

            // 圆心到弦的有向距离（apothem）：弓高 sagitta = |bulge| * 弦长 / 2
            // 扫掠角 > 180° 时 sagitta > radius，apothem 为负（圆心在弦另一侧），公式自然成立
            double sagitta = Math.Abs(bulge) * chordLength / 2.0;
            double distToCenter = radius - sagitta;

            // bulge > 0（逆时针弧）圆心在弦左侧，bulge < 0 在右侧
            int sign = bulge > 0 ? 1 : -1;

            double centerX = midX + sign * perpX * distToCenter;
            double centerY = midY + sign * perpY * distToCenter;
            var center = new Point2D(centerX, centerY);

            double angleAtStart = Math.Atan2(start.Y - centerY, start.X - centerX);
            double angleAtEnd = Math.Atan2(end.Y - centerY, end.X - centerX);

            // 规范化为 CCW：顺时针弧（bulge<0）等价于从 end 到 start 的逆时针弧
            double startAngle = bulge > 0 ? angleAtStart : angleAtEnd;
            double endAngle = bulge > 0 ? angleAtEnd : angleAtStart;
            if (endAngle <= startAngle)
                endAngle += 2.0 * Math.PI;

            return new Arc2D(center, radius, startAngle, endAngle);
        }

        /// <summary>
        /// 从 Arc 实体转换为 Arc2D。
        /// AutoCAD Arc 永远从 StartAngle 逆时针扫到 EndAngle；跨 0° 时 EndAngle &lt; StartAngle，
        /// 必须归一化（EndAngle += 2π），否则下游按负步长离散会画出补弧。
        /// </summary>
        private Arc2D ToArc2D(Arc arc)
        {
            var center = new Point2D(arc.Center.X, arc.Center.Y);
            double startAngle = arc.StartAngle;
            double endAngle = arc.EndAngle;
            if (endAngle <= startAngle)
                endAngle += 2.0 * Math.PI;

            return new Arc2D(center, arc.Radius, startAngle, endAngle);
        }

        /// <summary>
        /// 从 Spline 实体转换为 Spline2D
        /// </summary>
        private Spline2D ToSpline2D(Spline spline)
        {
            var controlPoints = new List<Point2D>();
            for (int i = 0; i < spline.NumControlPoints; i++)
            {
                var pt = spline.GetControlPointAt(i);
                controlPoints.Add(new Point2D(pt.X, pt.Y));
            }

            var start = new Point2D(spline.StartPoint.X, spline.StartPoint.Y);
            var end = new Point2D(spline.EndPoint.X, spline.EndPoint.Y);

            return new Spline2D(controlPoints.ToArray(), spline.Degree, start, end);
        }

        /// <summary>
        /// 从 Ellipse 实体转换为 Ellipse2D
        /// </summary>
        private Ellipse2D ToEllipse2D(Ellipse ellipse)
        {
            var center = new Point2D(ellipse.Center.X, ellipse.Center.Y);

            // 计算旋转角度（主轴方向）
            var majorAxis = ellipse.MajorAxis;
            double rotation = Math.Atan2(majorAxis.Y, majorAxis.X);

            return new Ellipse2D(
                center,
                ellipse.MajorRadius,
                ellipse.MinorRadius,
                rotation,
                ellipse.StartParam,
                ellipse.EndParam);
        }

        /// <summary>
        /// 从 Polyline2d 提取顶点折线（忽略弧拟合）
        /// </summary>
        private void ExtractPolyline2dSegments(Polyline2d polyline2d, Transaction tr, List<Line2D> allSegments)
        {
            Point2D? prevPoint = null;
            Point2D? firstPoint = null;

            foreach (ObjectId vId in polyline2d)
            {
                var vertex = tr.GetObject(vId, OpenMode.ForRead) as Vertex2d;
                if (vertex == null)
                    continue;

                var currentPoint = new Point2D(vertex.Position.X, vertex.Position.Y);

                if (prevPoint == null)
                    firstPoint = currentPoint;
                else
                    allSegments.Add(new Line2D(prevPoint.Value, currentPoint));

                prevPoint = currentPoint;
            }

            if (polyline2d.Closed && prevPoint.HasValue && firstPoint.HasValue)
                allSegments.Add(new Line2D(prevPoint.Value, firstPoint.Value));
        }

        /// <summary>
        /// 从 Polyline3d 提取顶点折线（投影到XY平面）
        /// </summary>
        private void ExtractPolyline3dSegments(Polyline3d polyline3d, Transaction tr, List<Line2D> allSegments)
        {
            Point2D? prevPoint = null;
            Point2D? firstPoint = null;

            foreach (ObjectId vId in polyline3d)
            {
                var vertex = tr.GetObject(vId, OpenMode.ForRead) as PolylineVertex3d;
                if (vertex == null)
                    continue;

                var currentPoint = new Point2D(vertex.Position.X, vertex.Position.Y);

                if (prevPoint == null)
                    firstPoint = currentPoint;
                else
                    allSegments.Add(new Line2D(prevPoint.Value, currentPoint));

                prevPoint = currentPoint;
            }

            if (polyline3d.Closed && prevPoint.HasValue && firstPoint.HasValue)
                allSegments.Add(new Line2D(prevPoint.Value, firstPoint.Value));
        }
    }
}
