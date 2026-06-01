using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using HyCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Shared.AutoCAD.Services
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

        /// <summary>
        /// 从 ObjectId 集合提取所有曲线段（包含完整曲线信息）
        /// 新方法：支持 Polyline 多顶点、Arc、Spline、Ellipse
        /// </summary>
        public List<CurveSegment2D> ExtractSegmentsWithCurveInfo(IEnumerable<ObjectId> curveIds, double tolerance)
        {
            var segments = new List<CurveSegment2D>();
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

                        if (entity is Line line)
                        {
                            // 直线：简单处理
                            var start = new Point2D(line.StartPoint.X, line.StartPoint.Y);
                            var end = new Point2D(line.EndPoint.X, line.EndPoint.Y);
                            segments.Add(new CurveSegment2D(new Line2D(start, end)));
                        }
                        else if (entity is Polyline polyline)
                        {
                            // 多段线：提取所有顶点间的线段
                            segments.AddRange(ExtractPolylineSegments(polyline));
                        }
                        else if (entity is Arc arc)
                        {
                            // 圆弧：记录完整圆弧信息
                            segments.Add(ExtractArcSegment(arc));
                        }
                        else if (entity is Spline spline)
                        {
                            // 样条：记录控制点信息
                            segments.Add(ExtractSplineSegment(spline));
                        }
                        else if (entity is Circle circle)
                        {
                            // 完整圆：转换为360度的圆弧，标记为来自完整圆
                            var center = new Point2D(circle.Center.X, circle.Center.Y);
                            // 完整圆：从0度到360度（2π），逆时针方向
                            var arc2d = new Arc2D(center, circle.Radius, 0, 2 * Math.PI);
                            // 完整圆默认逆时针，bulge > 0（这里用一个大的正值表示）
                            double bulge = Math.Tan(Math.PI / 2.0); // 180度圆弧的 bulge，表示逆时针
                            
                            // Circle的SimplifiedLine起点=终点，centerSide设为0（无意义）
                            double centerSide = 0; // Circle需要在FindMatchingCurveSegment中动态计算
                            
                            segments.Add(new CurveSegment2D(arc2d, isFromFullCircle: true, originalBulge: bulge, centerSide: centerSide));
                        }
                        else if (entity is Ellipse ellipse)
                        {
                            // 椭圆：记录椭圆参数
                            segments.Add(ExtractEllipseSegment(ellipse));
                        }
                        else if (entity is Curve curve)
                        {
                            // 其他曲线类型：简化为直线
                            var start = new Point2D(curve.StartPoint.X, curve.StartPoint.Y);
                            var end = new Point2D(curve.EndPoint.X, curve.EndPoint.Y);
                            segments.Add(new CurveSegment2D(new Line2D(start, end)));
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
        /// 从 Polyline 提取所有线段（包含圆弧段）
        /// </summary>
        private List<CurveSegment2D> ExtractPolylineSegments(Polyline polyline)
        {
            var segments = new List<CurveSegment2D>();
            int numSegments = polyline.Closed ? polyline.NumberOfVertices : polyline.NumberOfVertices - 1;

            for (int i = 0; i < numSegments; i++)
            {
                int nextIndex = (i + 1) % polyline.NumberOfVertices;

                var p1 = polyline.GetPoint2dAt(i);
                var p2 = polyline.GetPoint2dAt(nextIndex);
                double bulge = polyline.GetBulgeAt(i);

                var start = new Point2D(p1.X, p1.Y);
                var end = new Point2D(p2.X, p2.Y);

                if (Math.Abs(bulge) < 1e-10)
                {
                    // 直线段
                    segments.Add(new CurveSegment2D(new Line2D(start, end)));
                }
                else
                {
                    // 圆弧段：从 bulge 计算圆弧参数，并保存原始 bulge
                    var arc = BulgeToArc2D(start, end, bulge);
                    
                    // 计算圆心在SimplifiedLine的哪一侧
                    double chordDx = end.X - start.X;
                    double chordDy = end.Y - start.Y;
                    double normalX = -chordDy;
                    double normalY = chordDx;
                    double centerDx = arc.Center.X - start.X;
                    double centerDy = arc.Center.Y - start.Y;
                    double centerSide = normalX * centerDx + normalY * centerDy;
                    
                    segments.Add(new CurveSegment2D(arc, isFromFullCircle: false, originalBulge: bulge, centerSide: centerSide));
                }
            }

            return segments;
        }

        /// <summary>
        /// 从 bulge 值计算圆弧参数
        /// bulge = tan(angle/4), 其中 angle 是圆弧的扫掠角度
        /// </summary>
        private Arc2D BulgeToArc2D(Point2D start, Point2D end, double bulge)
        {
            // 弦长
            double chordLength = start.DistanceTo(end);
            
            // 圆弧角度
            double sweepAngle = 4.0 * Math.Atan(Math.Abs(bulge));
            
            // 半径
            double radius = chordLength / (2.0 * Math.Sin(sweepAngle / 2.0));
            
            // 弦中点
            double midX = (start.X + end.X) / 2.0;
            double midY = (start.Y + end.Y) / 2.0;
            
            // 弦方向向量
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            
            // 垂直方向（指向圆心）
            double perpX = -dy / chordLength;
            double perpY = dx / chordLength;
            
            // 圆心到弦中点的距离
            double sagitta = Math.Abs(bulge) * chordLength / 2.0;
            double distToCenter = radius - sagitta;
            
            // bulge 的符号决定圆弧的方向
            int sign = bulge > 0 ? 1 : -1;
            
            // 圆心位置
            double centerX = midX + sign * perpX * distToCenter;
            double centerY = midY + sign * perpY * distToCenter;
            var center = new Point2D(centerX, centerY);
            
            // 计算起始和结束角度
            double startAngle = Math.Atan2(start.Y - centerY, start.X - centerX);
            double endAngle = Math.Atan2(end.Y - centerY, end.X - centerX);
            
            // 调整角度以确保正确的扫掠方向
            if (bulge > 0)
            {
                // 逆时针
                if (endAngle <= startAngle)
                    endAngle += 2.0 * Math.PI;
            }
            else
            {
                // 顺时针
                if (endAngle >= startAngle)
                    endAngle -= 2.0 * Math.PI;
            }
            
            return new Arc2D(center, radius, startAngle, endAngle);
        }

        /// <summary>
        /// 从 Arc 对象提取圆弧段
        /// </summary>
        private CurveSegment2D ExtractArcSegment(Arc arc)
        {
            var center = new Point2D(arc.Center.X, arc.Center.Y);
            var arc2d = new Arc2D(center, arc.Radius, arc.StartAngle, arc.EndAngle);
            
            // 计算并保存 bulge 值
            // 注意：AutoCAD Arc 的 EndAngle-StartAngle 可能为 ±270° 等非最小扫掠角
            // Polyline 的 bulge 约定使用 (-π, π) 内的最小扫掠角表示同一弧段
            double sweepAngleRaw = arc.EndAngle - arc.StartAngle;
            // 归一化到 (-π, π)
            while (sweepAngleRaw > Math.PI) sweepAngleRaw -= 2.0 * Math.PI;
            while (sweepAngleRaw <= -Math.PI) sweepAngleRaw += 2.0 * Math.PI;
            double bulge = Math.Tan(sweepAngleRaw / 4.0);
            
            // 计算圆心在SimplifiedLine的哪一侧
            // 使用叉积判断：chordNormal · (center - startPoint)
            var startPoint = arc2d.StartPoint;
            var endPoint = arc2d.EndPoint;
            
            // 弦方向向量
            double chordDx = endPoint.X - startPoint.X;
            double chordDy = endPoint.Y - startPoint.Y;
            
            // 法向向量（逆时针旋转90度）：(-dy, dx)
            double normalX = -chordDy;
            double normalY = chordDx;
            
            // 圆心相对于起点的向量
            double centerDx = center.X - startPoint.X;
            double centerDy = center.Y - startPoint.Y;
            
            // 点积：判断圆心在法向的哪一侧
            double centerSide = normalX * centerDx + normalY * centerDy;
            
            return new CurveSegment2D(arc2d, isFromFullCircle: false, originalBulge: bulge, centerSide: centerSide);
        }

        /// <summary>
        /// 从 Spline 对象提取样条段
        /// </summary>
        private CurveSegment2D ExtractSplineSegment(Spline spline)
        {
            // 提取控制点
            var controlPoints = new List<Point2D>();
            for (int i = 0; i < spline.NumControlPoints; i++)
            {
                var pt = spline.GetControlPointAt(i);
                controlPoints.Add(new Point2D(pt.X, pt.Y));
            }

            var start = new Point2D(spline.StartPoint.X, spline.StartPoint.Y);
            var end = new Point2D(spline.EndPoint.X, spline.EndPoint.Y);

            var spline2d = new Spline2D(controlPoints.ToArray(), spline.Degree, start, end);
            return new CurveSegment2D(spline2d);
        }

        /// <summary>
        /// 从 Ellipse 对象提取椭圆段
        /// </summary>
        private CurveSegment2D ExtractEllipseSegment(Ellipse ellipse)
        {
            var center = new Point2D(ellipse.Center.X, ellipse.Center.Y);
            
            // AutoCAD Ellipse 使用主轴向量和半径比
            double majorRadius = ellipse.MajorRadius;
            double minorRadius = ellipse.MinorRadius;
            
            // 计算旋转角度（主轴方向）
            var majorAxis = ellipse.MajorAxis;
            double rotation = Math.Atan2(majorAxis.Y, majorAxis.X);
            
            var ellipse2d = new Ellipse2D(
                center,
                majorRadius,
                minorRadius,
                rotation,
                ellipse.StartParam,
                ellipse.EndParam);
            
            return new CurveSegment2D(ellipse2d);
        }
        
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
                        // Polyline逐段处理
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
                        var curveSegment = ExtractArcSegment(arc);
                        if (curveSegment.OriginalArc.HasValue)
                        {
                            var simplified = simplificationService.SimplifyArc(
                                curveSegment.OriginalArc.Value, 
                                arcSegmentCount);
                            
                            allSegments.AddRange(simplified);
                            mappings.Add(new SimplifiedCurveMapping(
                                curveSegment.OriginalArc.Value, 
                                simplified));
                        }
                    }
                    else if (entity is Circle circle)
                    {
                        // Circle作为完整Arc处理
                        var center = new Point2D(circle.Center.X, circle.Center.Y);
                        var radius = circle.Radius;
                        var arc2d = new Arc2D(center, radius, 0, 2.0 * Math.PI);
                        
                        var simplified = simplificationService.SimplifyArc(arc2d, arcSegmentCount);
                        
                        allSegments.AddRange(simplified);
                        mappings.Add(new SimplifiedCurveMapping(arc2d, simplified));
                    }
                    else if (entity is Ellipse ellipse)
                    {
                        // Ellipse简化
                        var curveSegment = ExtractEllipseSegment(ellipse);
                        if (curveSegment.OriginalEllipse.HasValue)
                        {
                            var simplified = simplificationService.SimplifyEllipse(
                                curveSegment.OriginalEllipse.Value, 
                                ellipseSegmentCount);
                            
                            allSegments.AddRange(simplified);
                            mappings.Add(new SimplifiedCurveMapping(
                                curveSegment.OriginalEllipse.Value, 
                                simplified));
                        }
                    }
                    else if (entity is Spline spline)
                    {
                        // Spline简化
                        var curveSegment = ExtractSplineSegment(spline);
                        if (curveSegment.OriginalSpline.HasValue)
                        {
                            var simplified = simplificationService.SimplifySpline(
                                curveSegment.OriginalSpline.Value, 
                                splineSegmentCount);
                            
                            allSegments.AddRange(simplified);
                            mappings.Add(new SimplifiedCurveMapping(
                                curveSegment.OriginalSpline.Value, 
                                simplified));
                        }
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
            int vertexCount = polyline.NumberOfVertices;
            if (polyline.Closed)
                vertexCount++; // 闭合多段线需要处理最后一段

            for (int i = 0; i < vertexCount - 1; i++)
            {
                int currentIndex = i;
                int nextIndex = (i + 1) % polyline.NumberOfVertices;

                var bulge = polyline.GetBulgeAt(currentIndex);
                var start = polyline.GetPoint2dAt(currentIndex);
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
                    var arc = BulgeToArc(startPoint, endPoint, bulge);
                    var simplified = simplificationService.SimplifyArc(arc, arcSegmentCount);
                    
                    allSegments.AddRange(simplified);
                    mappings.Add(new SimplifiedCurveMapping(arc, simplified));
                }
            }
        }
        
        /// <summary>
        /// Bulge转Arc（辅助方法）
        /// </summary>
        private Arc2D BulgeToArc(Point2D start, Point2D end, double bulge)
        {
            // Bulge = tan(θ/4), θ为圆心角
            double angle = 4 * Math.Atan(bulge);
            double chord = start.DistanceTo(end);
            double radius = chord / (2 * Math.Sin(angle / 2));
            
            // 计算圆心
            var midPoint = new Point2D((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            var chordVec = new Vector2D(end.X - start.X, end.Y - start.Y);
            var chordLength = Math.Sqrt(chordVec.X * chordVec.X + chordVec.Y * chordVec.Y);
            var chordDir = new Vector2D(chordVec.X / chordLength, chordVec.Y / chordLength);
            var perpDir = new Vector2D(-chordDir.Y, chordDir.X); // 垂直方向
            
            double sagitta = radius - Math.Sqrt(radius * radius - (chord / 2) * (chord / 2));
            var center = new Point2D(
                midPoint.X + perpDir.X * (bulge > 0 ? sagitta : -sagitta),
                midPoint.Y + perpDir.Y * (bulge > 0 ? sagitta : -sagitta));
            
            // 计算起止角度
            var startDir = new Vector2D(start.X - center.X, start.Y - center.Y);
            var endDir = new Vector2D(end.X - center.X, end.Y - center.Y);
            
            double startAngle = Math.Atan2(startDir.Y, startDir.X);
            double endAngle = Math.Atan2(endDir.Y, endDir.X);
            
            // Arc2D 统一使用弧度制，规范化到 [0, 2π)
            if (startAngle < 0) startAngle += 2.0 * Math.PI;
            if (endAngle < 0) endAngle += 2.0 * Math.PI;
            
            return new Arc2D(center, Math.Abs(radius), startAngle, endAngle);
        }
    }
}

