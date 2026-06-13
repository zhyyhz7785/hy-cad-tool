using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.DCEL.Domain.DataStructures;
using HyCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Interfaces;
using HyCADTool.Shared.AutoCAD.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
// AutoCAD.DatabaseServices 也有 Face 类型，显式指向 DCEL 领域的 Face
using Face = HyCADTool.Features.DCEL.Domain.DataStructures.Face;

namespace HyCADTool.Features.DCEL.Services
{
    /// <summary>
    /// DCEL 渲染器实现（DCEL Renderer Implementation）
    /// 将 DCEL 图渲染到 AutoCAD
    /// </summary>
    public class DCELRenderer : IDCELRenderer
    {
        private readonly ILayerManager _layerManager;

        /// <summary>
        /// 构造函数（支持依赖注入）
        /// </summary>
        public DCELRenderer(ILayerManager layerManager = null)
        {
            _layerManager = layerManager ?? new LayerManager();
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
                _layerManager.EnsureLayer(tr, innerLayer, 2); // 黄色

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // 统一遍历所有面，根据 IsOuter 属性分别绘制
                foreach (var face in graph.Faces)
                {
                    Polyline polyline = null;
                    bool appended = false;
                    try
                    {
                        // 仅去除相邻重复点（全局 Distinct 会误删"8字形"等
                        // 多次经过同一顶点的合法面顶点）
                        var vertices = CollectFaceVertices(face);
                        if (vertices.Count < 3)
                            continue; // 跳过无效的面

                        polyline = new Polyline(vertices.Count);
                        polyline.Layer = face.IsOuter ? outerLayer : innerLayer;

                        for (int i = 0; i < vertices.Count; i++)
                        {
                            polyline.AddVertexAt(i, vertices[i], 0, 0, 0);
                        }

                        polyline.Closed = true;

                        btr.AppendEntity(polyline);
                        tr.AddNewlyCreatedDBObject(polyline, true);
                        appended = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DCELRenderer] 单面绘制失败: {ex.Message}");
                    }
                    finally
                    {
                        if (!appended)
                            polyline?.Dispose();
                    }
                }

                tr.Commit();
            }
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
            bool restoreOriginal = true,
            double tolerance = 0.01)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("无法获取当前活动文档");

            var db = doc.Database;

            // 匹配容差与构建容差同源：顶点网格量化合并最多偏移约 tol·√2，
            // 取 2×tolerance 保证简化段端点仍能命中合并后的顶点
            double matchTolerance = Math.Max(tolerance * 2.0, 1e-9);

            // 量化端点索引：把 O(半边数 × 映射数 × 段数) 的全量扫描降为查桶
            var mappingIndex = restoreOriginal && mappings != null && mappings.Count > 0
                ? new MappingEndpointIndex(mappings, matchTolerance)
                : null;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                // 使用统一的图层管理服务
                _layerManager.EnsureLayer(tr, outerLayer, 1); // 红色
                _layerManager.EnsureLayer(tr, innerLayer, 2); // 黄色

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                foreach (var face in graph.Faces)
                {
                    Polyline polyline = null;
                    bool appended = false;
                    try
                    {
                        var boundaryHalfEdges = face.Components;
                        if (boundaryHalfEdges.Count < 3)
                            continue;

                        polyline = new Polyline();
                        polyline.Layer = face.IsOuter ? outerLayer : innerLayer;

                        if (mappingIndex != null)
                        {
                            // 尝试恢复原始曲线
                            BuildPolylineWithOriginalCurves(polyline, boundaryHalfEdges, mappingIndex, matchTolerance);
                        }
                        else
                        {
                            // 使用简化线段
                            BuildPolylineWithSimplifiedSegments(polyline, boundaryHalfEdges);
                        }

                        if (polyline.NumberOfVertices >= 3)
                        {
                            polyline.Closed = true;
                            btr.AppendEntity(polyline);
                            tr.AddNewlyCreatedDBObject(polyline, true);
                            appended = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DCELRenderer] 多段线绘制失败: {ex.Message}");
                    }
                    finally
                    {
                        if (!appended)
                            polyline?.Dispose();
                    }
                }

                tr.Commit();
            }
        }

        /// <summary>
        /// 收集面的顶点（仅去除相邻重复点，保留多次经过的同一顶点）
        /// </summary>
        private static List<Point2d> CollectFaceVertices(Face face)
        {
            var vertices = new List<Point2d>(face.Components.Count);

            foreach (var halfEdge in face.Components)
            {
                var position = halfEdge.StartVertex.Position;
                var point = new Point2d(position.X, position.Y);

                if (!IsValidPoint(point))
                    continue;

                if (vertices.Count > 0 && AreSamePoint(vertices[vertices.Count - 1], point))
                    continue;

                vertices.Add(point);
            }

            // 闭合多段线首尾点重复时去掉尾点
            if (vertices.Count > 1 && AreSamePoint(vertices[0], vertices[vertices.Count - 1]))
                vertices.RemoveAt(vertices.Count - 1);

            return vertices;
        }

        /// <summary>
        /// 构建带原始曲线的Polyline：
        /// 半边序列命中某条曲线的简化线段序列时，整段折线替换为一个 bulge 弧段（Arc）
        /// 或按面遍历方向输出简化顶点（Ellipse/Spline）。
        /// </summary>
        private void BuildPolylineWithOriginalCurves(
            Polyline polyline,
            List<HalfEdge> halfEdges,
            MappingEndpointIndex mappingIndex,
            double matchTolerance)
        {
            int vertexIndex = 0;
            var processedEdges = new HashSet<HalfEdge>();

            for (int i = 0; i < halfEdges.Count; i++)
            {
                var currentEdge = halfEdges[i];
                if (processedEdges.Contains(currentEdge))
                    continue;

                var startPt = currentEdge.StartVertex.Position;
                var endPt = currentEdge.GetEndVertex()?.Position;

                if (endPt == null)
                    continue;

                // 仅检索起点落在同一量化桶附近的候选映射
                var matched = FindMatchingMapping(
                    halfEdges, i, mappingIndex, startPt, matchTolerance,
                    out int consumedCount, out bool reversed);

                if (matched != null)
                {
                    if (matched.OriginalType == CurveSegmentType.Arc && matched.OriginalArc.HasValue)
                    {
                        // Arc：整段替换为一个 bulge 段。
                        // Arc2D 统一为 CCW 表示（SweepAngle 恒正），
                        // 面遍历方向与之相反时仅需翻转 bulge 符号，不再构造"反转 Arc2D"
                        //（旧实现交换起止角后 SweepAngle 仍为正，恢复出来永远是逆时针补弧）。
                        var arc = matched.OriginalArc.Value;
                        double bulge = Math.Tan(arc.SweepAngle / 4.0);
                        if (reversed)
                            bulge = -bulge;

                        polyline.AddVertexAt(vertexIndex,
                            new Point2d(startPt.X, startPt.Y),
                            bulge, 0, 0);
                        vertexIndex++;
                    }
                    else
                    {
                        // Ellipse/Spline：按面遍历方向逐顶点输出消费掉的半边起点，
                        // 自动保证方向正确（旧实现反向匹配时按正向加点，折线方向错乱）。
                        for (int j = 0; j < consumedCount; j++)
                        {
                            var he = halfEdges[(i + j) % halfEdges.Count];
                            var p = he.StartVertex.Position;
                            polyline.AddVertexAt(vertexIndex,
                                new Point2d(p.X, p.Y),
                                0, 0, 0);
                            vertexIndex++;
                        }
                    }

                    // 标记已处理的边并跳过
                    for (int j = 0; j < consumedCount; j++)
                    {
                        processedEdges.Add(halfEdges[(i + j) % halfEdges.Count]);
                    }
                    i += consumedCount - 1;
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
        /// 构建带简化线段的Polyline
        /// </summary>
        private void BuildPolylineWithSimplifiedSegments(
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

                polyline.AddVertexAt(vertexIndex, point, 0, 0, 0);
                vertexIndex++;
                lastPoint = point;
            }
        }

        /// <summary>
        /// 查找匹配的简化曲线映射（仅扫描量化桶内候选）
        /// </summary>
        private SimplifiedCurveMapping FindMatchingMapping(
            List<HalfEdge> allHalfEdges,
            int startIndex,
            MappingEndpointIndex mappingIndex,
            Point2D edgeStart,
            double tolerance,
            out int consumedEdgeCount,
            out bool reversed)
        {
            consumedEdgeCount = 1;
            reversed = false;

            foreach (var mapping in mappingIndex.GetCandidates(edgeStart))
            {
                var matchResult = TryMatchSimplifiedSequence(
                    allHalfEdges,
                    startIndex,
                    mapping.SimplifiedSegments,
                    tolerance);

                if (matchResult.matched)
                {
                    consumedEdgeCount = matchResult.count;
                    reversed = matchResult.reversed;
                    return mapping;
                }
            }

            return null;
        }

        /// <summary>
        /// 尝试匹配简化线段序列（支持正向和逆向）
        /// </summary>
        private (bool matched, int count, bool reversed) TryMatchSimplifiedSequence(
            List<HalfEdge> allHalfEdges,
            int startIndex,
            List<Line2D> simplifiedSegments,
            double tolerance)
        {
            if (simplifiedSegments.Count == 0 ||
                simplifiedSegments.Count > allHalfEdges.Count - startIndex)
                return (false, 0, false);

            // 尝试正向匹配
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

            return (false, 0, false);
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

        /// <summary>
        /// 简化曲线映射的量化端点索引。
        /// 每条映射按曲线起点和终点各登记一次；查询时检索点所在格及周边 8 格，
        /// 保证容差内的端点不因网格边界漏检。
        /// </summary>
        private sealed class MappingEndpointIndex
        {
            private readonly Dictionary<(long, long), List<SimplifiedCurveMapping>> _buckets;
            private readonly double _cellSize;

            public MappingEndpointIndex(List<SimplifiedCurveMapping> mappings, double cellSize)
            {
                _cellSize = cellSize > 0 ? cellSize : 1e-9;
                _buckets = new Dictionary<(long, long), List<SimplifiedCurveMapping>>(mappings.Count * 2);

                foreach (var mapping in mappings)
                {
                    Add(mapping.GetStartPoint(), mapping);
                    Add(mapping.GetEndPoint(), mapping);
                }
            }

            private (long, long) Quantize(Point2D point)
            {
                return ((long)Math.Round(point.X / _cellSize), (long)Math.Round(point.Y / _cellSize));
            }

            private void Add(Point2D point, SimplifiedCurveMapping mapping)
            {
                var key = Quantize(point);
                if (!_buckets.TryGetValue(key, out var list))
                {
                    list = new List<SimplifiedCurveMapping>();
                    _buckets[key] = list;
                }
                if (!list.Contains(mapping))
                    list.Add(mapping);
            }

            public IEnumerable<SimplifiedCurveMapping> GetCandidates(Point2D point)
            {
                var (qx, qy) = Quantize(point);
                HashSet<SimplifiedCurveMapping> seen = null;

                for (long dx = -1; dx <= 1; dx++)
                {
                    for (long dy = -1; dy <= 1; dy++)
                    {
                        if (!_buckets.TryGetValue((qx + dx, qy + dy), out var list))
                            continue;

                        foreach (var mapping in list)
                        {
                            if (seen == null)
                                seen = new HashSet<SimplifiedCurveMapping>();
                            if (seen.Add(mapping))
                                yield return mapping;
                        }
                    }
                }
            }
        }
    }
}
