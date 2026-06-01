using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.DCEL.Domain.DataStructures;
using HyCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Interfaces;
using HyCADTool.Shared.AutoCAD.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.DCEL.Services
{
    /// <summary>
    /// DCEL 渲染器实现（DCEL Renderer Implementation）
    /// 将 DCEL 图渲染到 AutoCAD
    /// </summary>
    public class DCELRenderer : IDCELRenderer
    {
        private readonly ILayerManager _layerManager;
        private readonly MarkerLayerService _markerService;

        /// <summary>
        /// 构造函数（支持依赖注入）
        /// </summary>
        public DCELRenderer(ILayerManager layerManager = null, MarkerLayerService markerService = null)
        {
            _layerManager = layerManager ?? new LayerManager();
            _markerService = markerService ?? new MarkerLayerService();
        }

        /// <summary>
        /// 渲染 DCEL 图
        /// 根据 Face.IsOuter 属性分别绘制到不同图层
        /// </summary>
        public void Render(DCELGraph graph, string outerLayer = "dcelOuter", string innerLayer = "dcelInner")
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("无法获取当前活动文档");

            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                // 使用统一的图层管理服务
                _layerManager.EnsureLayer(tr, outerLayer, 1); // 红色
                _layerManager.EnsureLayer(tr, innerLayer, 2);  // 黄色

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // 统一遍历所有面，根据 IsOuter 属性分别绘制
                foreach (var face in graph.Faces)
                {
                    try
                    {
                        // 获取面的所有顶点
                        var vertices = face.Components
                            .Select(he => he.StartVertex.Position)
                            .Select(p => new Point2d(p.X, p.Y))
                            .Where(IsValidPoint)
                            .Distinct(new Point2dEqualityComparer())
                            .ToList();

                        if (vertices.Count < 3)
                            continue; // 跳过无效的面

                        // 创建多段线
                        var polyline = new Polyline(vertices.Count);
                        polyline.Layer = face.IsOuter ? outerLayer : innerLayer;

                        for (int i = 0; i < vertices.Count; i++)
                        {
                            polyline.AddVertexAt(i, vertices[i], 0, 0, 0);
                        }

                        polyline.Closed = true;

                        // 添加到图形数据库
                        btr.AppendEntity(polyline);
                        tr.AddNewlyCreatedDBObject(polyline, true);
                    }
                    catch (Exception)
                    {
                        // 忽略单个面的绘制错误，继续处理其他面
                        continue;
                    }
                }

                tr.Commit();
            }
        }

        /// <summary>
        /// 渲染 DCEL 图（包含曲线信息恢复）
        /// 尝试恢复原始曲线类型（Arc, Spline, Ellipse）并连接成 Polyline
        /// </summary>
        public void RenderWithCurveInfo(
            DCELGraph graph,
            List<CurveSegment2D> curveSegments,
            string outerLayer = "dcelOuter",
            string innerLayer = "dcelInner")
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            if (curveSegments == null)
                throw new ArgumentNullException(nameof(curveSegments));

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("无法获取当前活动文档");

            var db = doc.Database;
            var disconnectedSegments = new List<(Point3d start, Point3d end)>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                // 创建图层
                _layerManager.EnsureLayer(tr, outerLayer, 1); // 红色
                _layerManager.EnsureLayer(tr, innerLayer, 2);  // 黄色

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // 遍历所有面
                foreach (var face in graph.Faces)
                {
                    try
                    {
                        var layerName = face.IsOuter ? outerLayer : innerLayer;

                        // 尝试构建包含曲线的 Polyline
                        var polyline = TryBuildPolylineWithCurves(face, curveSegments, out var failedSegments);

                        if (polyline != null)
                        {
                            // 成功连接
                            polyline.Layer = layerName;
                            btr.AppendEntity(polyline);
                            tr.AddNewlyCreatedDBObject(polyline, true);
                        }
                        else
                        {
                            // 无法连接，绘制独立曲线并记录
                            foreach (var segment in failedSegments)
                            {
                                DrawIndividualCurve(segment, layerName, tr, btr);
                                var start3d = new Point3d(segment.StartPoint.X, segment.StartPoint.Y, 0);
                                var end3d = new Point3d(segment.EndPoint.X, segment.EndPoint.Y, 0);
                                disconnectedSegments.Add((start3d, end3d));
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // 忽略单个面的绘制错误，继续处理其他面
                        continue;
                    }
                }

                tr.Commit();
            }

            // 标记无法连接的曲线段
            if (disconnectedSegments.Count > 0)
            {
                MarkDisconnectedSegments(db, disconnectedSegments);
            }
        }

        /// <summary>
        /// 尝试构建包含曲线的 Polyline
        /// </summary>
        private Polyline TryBuildPolylineWithCurves(
            HyCADTool.Features.DCEL.Domain.DataStructures.Face face,
            List<CurveSegment2D> curveSegments,
            out List<CurveSegment2D> failedSegments)
        {
            failedSegments = new List<CurveSegment2D>();
            var polyline = new Polyline();
            int index = 0;
            const double matchTolerance = 1e-3; // 增加容差以处理浮点数精度问题

            // 将 Arc 的弦与辅助边缓存：
            var arcChords = curveSegments
                .Where(s => s.Type == CurveSegmentType.Arc && s.OriginalArc.HasValue)
                .Select(s => new { Seg = s, Start = s.SimplifiedLine.StartPoint, End = s.SimplifiedLine.EndPoint })
                .ToList();
            var arcAuxEdges = curveSegments
                .Where(s => s.Type == CurveSegmentType.Arc && s.OriginalArc.HasValue)
                .SelectMany(s =>
                {
                    var arc = s.OriginalArc.Value;
                    var start = arc.StartPoint;
                    var end = arc.EndPoint;
                    var mid = arc.MidPoint;
                    return new[]
                    {
                        new { A = start, B = mid },
                        new { A = mid, B = end }
                    };
                })
                .ToList();

            for (int heIndex = 0; heIndex < face.Components.Count; heIndex++)
            {
                var halfEdge = face.Components[heIndex];
                var edgeStart = halfEdge.StartVertex.Position;
                var edgeEnd = halfEdge.GetEndVertex()?.Position;

                if (edgeEnd == null)
                    continue; // 跳过无效的半边

                // 辅助边（start→mid、mid→end）跳过，不参与输出
                bool isAux = arcAuxEdges.Any(e =>
                    (edgeStart.DistanceTo(e.A) < matchTolerance && edgeEnd.Value.DistanceTo(e.B) < matchTolerance) ||
                    (edgeStart.DistanceTo(e.B) < matchTolerance && edgeEnd.Value.DistanceTo(e.A) < matchTolerance));
                if (isAux)
                {
                    continue;
                }

                // 查找匹配的曲线段（优先弦完全相等的Arc，其次直线）
                var matchedSegment = FindMatchingCurveSegment(
                    edgeStart, edgeEnd.Value, curveSegments, matchTolerance, out bool isReversed);

                if (matchedSegment == null)
                {
                    // 尝试识别：当前半边是否位于某个Arc的弦上，且位于弦的起点方向
                    var chord = arcChords.FirstOrDefault(c =>
                        edgeStart.DistanceTo(c.Start) < matchTolerance &&
                        IsPointOnSegment(edgeEnd.Value, c.Start, c.End, matchTolerance));

                    if (chord != null)
                    {
                        // 沿着后续半边合并，直到到达弦的终点
                        var arcSeg = chord.Seg;
                        var chordEnd = chord.End;
                        int k = heIndex;
                        var curEnd = edgeEnd.Value;
                        while (k + 1 < face.Components.Count && curEnd.DistanceTo(chordEnd) > matchTolerance)
                        {
                            var next = face.Components[k + 1];
                            var nStart = next.StartVertex.Position;
                            var nEnd = next.GetEndVertex()?.Position;
                            if (nEnd == null) break;
                            if (!IsPointOnSegment(nStart, chord.Start, chord.End, matchTolerance) ||
                                !IsPointOnSegment(nEnd.Value, chord.Start, chord.End, matchTolerance))
                                break;
                            k++;
                            curEnd = nEnd.Value;
                        }

                        // 写入一次弧：从弦起点到弦终点
                        double bulge = arcSeg.OriginalBulge ?? 0.0;
                        // 方向：如果遍历是从弦终点开始，则反转（这里保证从Start触发，所以无需反转）
                        polyline.AddVertexAt(index, new Point2d(edgeStart.X, edgeStart.Y), bulge, 0, 0);

                        // 跳过已合并的半边
                        heIndex = k;
                    }
                    else
                    {
                        // 普通直线
                        polyline.AddVertexAt(index, new Point2d(edgeStart.X, edgeStart.Y), 0, 0, 0);
                        index++;
                        continue;
                    }
                }
                else
                {
                    // 根据曲线类型添加顶点
                    if (matchedSegment.Type == CurveSegmentType.Arc && matchedSegment.OriginalArc.HasValue)
                    {
                        // 使用原始bulge（已经规范化为 (-π, π) 的短弧表示）
                        double bulge = matchedSegment.OriginalBulge.Value;

#if DEBUG
                        var debugDoc = Application.DocumentManager.MdiActiveDocument;
                        if (debugDoc != null)
                        {
                            var debugEd = debugDoc.Editor;
                            debugEd.WriteMessage($"\n[Bulge处理] originalBulge={bulge:F4}, isReversed={isReversed}");
                        }
#endif

                        // 仅根据半边方向是否反向来调整bulge符号
                        if (isReversed)
                        {
                            bulge = -bulge;
#if DEBUG
                            if (debugDoc != null)
                            {
                                var debugEd = debugDoc.Editor;
                                debugEd.WriteMessage($" → 由于isReversed，反转bulge: {bulge:F4}");
                            }
#endif
                        }

#if DEBUG
                        if (debugDoc != null)
                        {
                            var debugEd = debugDoc.Editor;
                            debugEd.WriteMessage($" → 最终bulge={bulge:F4}");
                        }
#endif

                        polyline.AddVertexAt(index, new Point2d(edgeStart.X, edgeStart.Y), bulge, 0, 0);
                    }
                    else
                    {
                        // 直线段或其他类型（Spline/Ellipse 暂时用直线表示）
                        polyline.AddVertexAt(index, new Point2d(edgeStart.X, edgeStart.Y), 0, 0, 0);
                    }
                }
            }

            polyline.Closed = true;
            return polyline;
        }

        /// <summary>
        /// 查找匹配的曲线段
        /// </summary>
        private CurveSegment2D FindMatchingCurveSegment(
            HyCAD.Geometry.Point2D start,
            HyCAD.Geometry.Point2D end,
            List<CurveSegment2D> curveSegments,
            double tolerance,
            out bool isReversed)
        {
            isReversed = false;
            
            foreach (var segment in curveSegments)
            {
                // 仅当DCEL半边与该Arc的弦（SimplifiedLine）完全相等时，才用原始Arc替换。
                if (segment.Type == CurveSegmentType.Arc && segment.OriginalArc.HasValue)
                {
                    if (start.DistanceTo(segment.SimplifiedLine.StartPoint) < tolerance &&
                        end.DistanceTo(segment.SimplifiedLine.EndPoint) < tolerance)
                    {
                        isReversed = false;
                        return segment;
                    }
                    if (start.DistanceTo(segment.SimplifiedLine.EndPoint) < tolerance &&
                        end.DistanceTo(segment.SimplifiedLine.StartPoint) < tolerance)
                    {
                        isReversed = true;
                        return segment;
                    }
                    // 否则忽略该Arc，避免三角化辅助边被误判为Arc
                }
                
                // Line匹配：SimplifiedLine完全匹配
                if (segment.Type == CurveSegmentType.Line)
                {
                    // 正向匹配
                    if (start.DistanceTo(segment.SimplifiedLine.StartPoint) < tolerance &&
                        end.DistanceTo(segment.SimplifiedLine.EndPoint) < tolerance)
                    {
                        isReversed = false;
                        return segment;
                    }

                    // 反向匹配
                    if (start.DistanceTo(segment.SimplifiedLine.EndPoint) < tolerance &&
                        end.DistanceTo(segment.SimplifiedLine.StartPoint) < tolerance)
                    {
                        isReversed = true;
                        return segment;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 创建反向曲线段
        /// </summary>
        private CurveSegment2D CreateReversedSegment(CurveSegment2D segment)
        {
            if (segment.Type == CurveSegmentType.Arc && segment.OriginalArc.HasValue)
            {
                var arc = segment.OriginalArc.Value;
                // 反向圆弧：交换起始和结束角度
                var reversedArc = new Arc2D(arc.Center, arc.Radius, arc.EndAngle, arc.StartAngle);
                return new CurveSegment2D(reversedArc);
            }
            else if (segment.Type == CurveSegmentType.Line && segment.OriginalLine.HasValue)
            {
                var line = segment.OriginalLine.Value;
                var reversedLine = new HyCAD.Geometry.Line2D(line.EndPoint, line.StartPoint);
                return new CurveSegment2D(reversedLine);
            }

            // 其他类型暂时返回原始段
            return segment;
        }

        private static bool IsPointOnSegment(
            HyCAD.Geometry.Point2D p,
            HyCAD.Geometry.Point2D a,
            HyCAD.Geometry.Point2D b,
            double tol)
        {
            // 向量叉积判断共线
            double cross = (p.Y - a.Y) * (b.X - a.X) - (p.X - a.X) * (b.Y - a.Y);
            if (Math.Abs(cross) > tol * 10) return false;
            // 点到端点的投影范围判断
            double dot = (p.X - a.X) * (b.X - a.X) + (p.Y - a.Y) * (b.Y - a.Y);
            if (dot < -tol) return false;
            double len2 = (b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y);
            if (dot - len2 > tol) return false;
            return true;
        }

        /// <summary>
        /// 从圆弧计算 bulge 值
        /// bulge = tan(angle/4)
        /// </summary>
        private double CalculateBulge(Arc2D arc)
        {
            // 直接使用 EndAngle - StartAngle 以保留方向信息
            // 正值：逆时针，负值：顺时针
            double sweepAngle = arc.EndAngle - arc.StartAngle;
            
            // bulge = tan(angle/4)
            double bulge = Math.Tan(sweepAngle / 4.0);
            
            return bulge;
        }

        /// <summary>
        /// 绘制独立曲线
        /// </summary>
        private void DrawIndividualCurve(
            CurveSegment2D segment,
            string layerName,
            Transaction tr,
            BlockTableRecord btr)
        {
            Entity entity = null;

            try
            {
                if (segment.Type == CurveSegmentType.Arc && segment.OriginalArc.HasValue)
                {
                    // 绘制圆弧
                    var arc = segment.OriginalArc.Value;
                    var center3d = new Point3d(arc.Center.X, arc.Center.Y, 0);
                    entity = new Arc(center3d, arc.Radius, arc.StartAngle, arc.EndAngle);
                }
                else if (segment.Type == CurveSegmentType.Ellipse && segment.OriginalEllipse.HasValue)
                {
                    // 绘制椭圆（暂时用直线代替，完整实现较复杂）
                    var start3d = new Point3d(segment.StartPoint.X, segment.StartPoint.Y, 0);
                    var end3d = new Point3d(segment.EndPoint.X, segment.EndPoint.Y, 0);
                    entity = new Line(start3d, end3d);
                }
                else if (segment.Type == CurveSegmentType.Spline && segment.OriginalSpline.HasValue)
                {
                    // 绘制样条（暂时用直线代替，完整实现较复杂）
                    var start3d = new Point3d(segment.StartPoint.X, segment.StartPoint.Y, 0);
                    var end3d = new Point3d(segment.EndPoint.X, segment.EndPoint.Y, 0);
                    entity = new Line(start3d, end3d);
                }
                else
                {
                    // 直线
                    var start3d = new Point3d(segment.StartPoint.X, segment.StartPoint.Y, 0);
                    var end3d = new Point3d(segment.EndPoint.X, segment.EndPoint.Y, 0);
                    entity = new Line(start3d, end3d);
                }

                if (entity != null)
                {
                    entity.Layer = layerName;
                    btr.AppendEntity(entity);
                    tr.AddNewlyCreatedDBObject(entity, true);
                }
            }
            catch (Exception)
            {
                // 忽略绘制错误
            }
        }

        /// <summary>
        /// 标记无法连接的曲线段
        /// </summary>
        private void MarkDisconnectedSegments(Database db, List<(Point3d start, Point3d end)> segments)
        {
            const string markerLayer = "00_HY_标记_DCEL未连接曲线";

            using (var tr = db.TransactionManager.StartTransaction())
            {
                _markerService.EnsureMarkerLayer(db, tr, markerLayer);

                foreach (var (start, end) in segments)
                {
                    // 在起点和终点绘制圆形标记
                    _markerService.DrawCircleMarker(db, tr, start, 5.0, markerLayer);
                    _markerService.DrawCircleMarker(db, tr, end, 5.0, markerLayer);
                }

                tr.Commit();
            }

            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;
            ed?.WriteMessage($"\n⚠️ 有 {segments.Count} 个曲线段无法连接，已标记");
        }
        
        /// <summary>
        /// 渲染 DCEL 图（使用简化曲线映射）
        /// 根据映射字典恢复原始曲线或使用简化线段
        /// </summary>
        public void RenderWithMappings(
            DCELGraph graph,
            List<SimplifiedCurveMapping> mappings,
            string outerLayer = "dcelOuter",
            string innerLayer = "dcelInner",
            bool restoreOriginal = true)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("无法获取当前活动文档");

            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                // 使用统一的图层管理服务
                _layerManager.EnsureLayer(tr, outerLayer, 1); // 红色
                _layerManager.EnsureLayer(tr, innerLayer, 2);  // 黄色

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // 遍历所有面
                foreach (var face in graph.Faces)
                {
                    try
                    {
                        // 获取面的边界
                        var boundaryHalfEdges = face.Components.ToList();
                        if (boundaryHalfEdges.Count < 3)
                            continue;

                        // 构建Polyline，尝试恢复原始曲线
                        var polyline = new Polyline();
                        polyline.Layer = face.IsOuter ? outerLayer : innerLayer;

                        if (restoreOriginal)
                        {
                            // 尝试恢复原始曲线
                            TryBuildPolylineWithOriginalCurves(polyline, boundaryHalfEdges, mappings);
                        }
                        else
                        {
                            // 使用简化线段
                            TryBuildPolylineWithSimplifiedSegments(polyline, boundaryHalfEdges);
                        }

                        if (polyline.NumberOfVertices >= 3)
                        {
                            polyline.Closed = true;
                            btr.AppendEntity(polyline);
                            tr.AddNewlyCreatedDBObject(polyline, true);
                        }
                        else
                        {
                            polyline.Dispose();
                        }
                    }
                    catch
                    {
                        // 静默处理错误
                    }
                }

                tr.Commit();
            }
        }
        
        /// <summary>
        /// 尝试构建带原始曲线的Polyline
        /// </summary>
        private void TryBuildPolylineWithOriginalCurves(
            Polyline polyline,
            List<HalfEdge> halfEdges,
            List<SimplifiedCurveMapping> mappings)
        {
            const double matchTolerance = 1e-3;
            int vertexIndex = 0;
            var processedEdges = new HashSet<HalfEdge>();

            for (int i = 0; i < halfEdges.Count; i++)
            {
                if (processedEdges.Contains(halfEdges[i]))
                    continue;

                var currentEdge = halfEdges[i];
                var startPt = currentEdge.StartVertex.Position;
                var endPt = currentEdge.GetEndVertex()?.Position;

                if (endPt == null)
                    continue;

                var edgeLine = new Line2D(startPt, endPt.Value);

                // 尝试查找匹配的映射
                var matchedMapping = FindMatchingMapping(edgeLine, halfEdges, i, mappings, matchTolerance, out int consumedCount);

                if (matchedMapping != null)
                {
                    // 找到匹配的曲线映射，恢复原始曲线
                    if (matchedMapping.OriginalType == CurveSegmentType.Arc && matchedMapping.OriginalArc.HasValue)
                    {
                        var arc = matchedMapping.OriginalArc.Value;
                        double bulge = CalculateBulgeFromArc(arc);
                        
                        polyline.AddVertexAt(vertexIndex, 
                            new Point2d(startPt.X, startPt.Y), 
                            bulge, 0, 0);
                        vertexIndex++;
                        
                        // 标记已处理的边
                        for (int j = 0; j < consumedCount; j++)
                        {
                            processedEdges.Add(halfEdges[(i + j) % halfEdges.Count]);
                        }
                        
                        // 跳过已处理的边
                        i += consumedCount - 1;
                    }
                    else
                    {
                        // 其他曲线类型（Ellipse、Spline）：当前使用简化线段
                        // TODO: 实现Ellipse和Spline的原生恢复
                        AddSimplifiedSegments(polyline, matchedMapping.SimplifiedSegments, ref vertexIndex);
                        
                        for (int j = 0; j < consumedCount; j++)
                        {
                            processedEdges.Add(halfEdges[(i + j) % halfEdges.Count]);
                        }
                        
                        i += consumedCount - 1;
                    }
                }
                else
                {
                    // 没有找到匹配，使用直线
                    polyline.AddVertexAt(vertexIndex, 
                        new Point2d(startPt.X, startPt.Y), 
                        0, 0, 0);
                    vertexIndex++;
                    processedEdges.Add(currentEdge);
                }
            }
        }
        
        /// <summary>
        /// 尝试构建带简化线段的Polyline
        /// </summary>
        private void TryBuildPolylineWithSimplifiedSegments(
            Polyline polyline,
            List<HalfEdge> halfEdges)
        {
            int vertexIndex = 0;
            Point2d? lastPoint = null;

            foreach (var halfEdge in halfEdges)
            {
                var startPt = halfEdge.StartVertex.Position;
                var point = new Point2d(startPt.X, startPt.Y);
                if (!IsValidPoint(point))
                    continue;

                if (lastPoint.HasValue && AreSamePoint(lastPoint.Value, point))
                    continue;

                polyline.AddVertexAt(vertexIndex, 
                    point, 
                    0, 0, 0);
                vertexIndex++;
                lastPoint = point;
            }
        }
        
        /// <summary>
        /// 查找匹配的简化曲线映射
        /// </summary>
        private SimplifiedCurveMapping FindMatchingMapping(
            Line2D edgeLine,
            List<HalfEdge> allHalfEdges,
            int startIndex,
            List<SimplifiedCurveMapping> mappings,
            double tolerance,
            out int consumedEdgeCount)
        {
            consumedEdgeCount = 1;

            foreach (var mapping in mappings)
            {
                // 尝试匹配简化线段序列（支持正向和逆向）
                var matchResult = TryMatchSimplifiedSequence(
                    edgeLine, 
                    allHalfEdges, 
                    startIndex, 
                    mapping.SimplifiedSegments, 
                    tolerance);

                if (matchResult.matched)
                {
                    consumedEdgeCount = matchResult.count;
                    
                    // 如果是逆向匹配且是Arc类型，需要反转bulge
                    if (matchResult.reversed && mapping.OriginalType == CurveSegmentType.Arc)
                    {
                        // 创建反转的Arc映射
                        var reversedArc = new Arc2D(
                            mapping.OriginalArc.Value.Center,
                            mapping.OriginalArc.Value.Radius,
                            mapping.OriginalArc.Value.EndAngle,    // 交换起止角度
                            mapping.OriginalArc.Value.StartAngle);
                        
                        var reversedSegments = new List<Line2D>(mapping.SimplifiedSegments);
                        reversedSegments.Reverse();
                        
                        return new SimplifiedCurveMapping(reversedArc, reversedSegments);
                    }
                    
                    return mapping;
                }
            }

            return null;
        }
        
        /// <summary>
        /// 尝试匹配简化线段序列（支持正向和逆向）
        /// </summary>
        private (bool matched, int count, bool reversed) TryMatchSimplifiedSequence(
            Line2D firstEdge,
            List<HalfEdge> allHalfEdges,
            int startIndex,
            List<Line2D> simplifiedSegments,
            double tolerance)
        {
            if (simplifiedSegments.Count == 0)
                return (false, 0, false);

            // 尝试正向匹配
            if (simplifiedSegments.Count <= allHalfEdges.Count - startIndex)
            {
                bool forwardMatch = true;
                for (int i = 0; i < simplifiedSegments.Count; i++)
                {
                    var halfEdge = allHalfEdges[(startIndex + i) % allHalfEdges.Count];
                    var halfEdgeStart = halfEdge.StartVertex.Position;
                    var halfEdgeEnd = halfEdge.GetEndVertex()?.Position;

                    if (halfEdgeEnd == null)
                    {
                        forwardMatch = false;
                        break;
                    }

                    var simplified = simplifiedSegments[i];

                    if (halfEdgeStart.DistanceTo(simplified.StartPoint) >= tolerance ||
                        halfEdgeEnd.Value.DistanceTo(simplified.EndPoint) >= tolerance)
                    {
                        forwardMatch = false;
                        break;
                    }
                }

                if (forwardMatch)
                    return (true, simplifiedSegments.Count, false);
                    
                // 尝试逆向匹配
                bool reverseMatch = true;
                for (int i = 0; i < simplifiedSegments.Count; i++)
                {
                    var halfEdge = allHalfEdges[(startIndex + i) % allHalfEdges.Count];
                    var halfEdgeStart = halfEdge.StartVertex.Position;
                    var halfEdgeEnd = halfEdge.GetEndVertex()?.Position;

                    if (halfEdgeEnd == null)
                    {
                        reverseMatch = false;
                        break;
                    }

                    var simplified = simplifiedSegments[simplifiedSegments.Count - 1 - i];

                    if (halfEdgeStart.DistanceTo(simplified.EndPoint) >= tolerance ||
                        halfEdgeEnd.Value.DistanceTo(simplified.StartPoint) >= tolerance)
                    {
                        reverseMatch = false;
                        break;
                    }
                }

                if (reverseMatch)
                    return (true, simplifiedSegments.Count, true);
            }

            return (false, 0, false);
        }
        
        /// <summary>
        /// 添加简化线段到Polyline
        /// </summary>
        private void AddSimplifiedSegments(Polyline polyline, List<Line2D> segments, ref int vertexIndex)
        {
            foreach (var segment in segments)
            {
                polyline.AddVertexAt(vertexIndex, 
                    new Point2d(segment.StartPoint.X, segment.StartPoint.Y), 
                    0, 0, 0);
                vertexIndex++;
            }
        }
        
        /// <summary>
        /// 从Arc2D计算Bulge值
        /// </summary>
        private double CalculateBulgeFromArc(Arc2D arc)
        {
            // Bulge = tan(θ/4), θ为圆心角
            return Math.Tan(arc.SweepAngle / 4.0);
        }

        private static bool IsValidPoint(Point2d point)
        {
            return !(double.IsNaN(point.X) || double.IsNaN(point.Y) ||
                     double.IsInfinity(point.X) || double.IsInfinity(point.Y));
        }

        private static bool AreSamePoint(Point2d a, Point2d b, double tolerance = 1e-9)
        {
            return a.GetDistanceTo(b) <= tolerance;
        }

        private sealed class Point2dEqualityComparer : IEqualityComparer<Point2d>
        {
            public bool Equals(Point2d x, Point2d y)
            {
                return AreSamePoint(x, y);
            }

            public int GetHashCode(Point2d obj)
            {
                unchecked
                {
                    int x = Math.Round(obj.X, 9).GetHashCode();
                    int y = Math.Round(obj.Y, 9).GetHashCode();
                    return (x * 397) ^ y;
                }
            }
        }
    }
}







