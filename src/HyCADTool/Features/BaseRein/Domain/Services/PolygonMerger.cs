using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using HyCAD.Geometry;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Features.BaseRein.Domain.Services
{
    /// <summary>
    /// 多边形合并服务
    /// 使用 Weiler-Atherton 算法进行多边形布尔运算
    /// 提供 Clipper2 作为备用方案
    /// </summary>
    public class PolygonMerger
    {
        private readonly double _tolerance;
        
        public PolygonMerger(double? tolerance = null)
        {
            _tolerance = tolerance ?? ToleranceSettings.Instance.DistanceTolerance;
        }
        
        /// <summary>
        /// 合并多个多边形（并集）。
        /// 优先使用 Weiler-Atherton 算法；该路径抛错时改用 Clipper2，保留孔洞和不相交的块。
        /// </summary>
        public PolygonUnionResult UnionPolygons(IEnumerable<Polygon2D> polygons)
        {
            var polygonList = polygons.ToList();
            
            if (polygonList.Count == 0)
                throw new ArgumentException("多边形列表为空", nameof(polygons));
            
            if (polygonList.Count == 1)
                return PolygonUnionResult.FromOuter(polygonList[0]);
            
            try
            {
                // 优先使用 Weiler-Atherton 算法
                return PolygonUnionResult.FromOuter(UnionUsingWeilerAtherton(polygonList));
            }
            catch (Exception)
            {
                // 回退到 Clipper2
                return UnionUsingClipper2(polygonList);
            }
        }
        
        /// <summary>
        /// 使用 Weiler-Atherton 算法合并多边形
        /// </summary>
        private Polygon2D UnionUsingWeilerAtherton(List<Polygon2D> polygons)
        {
            // 从第一个多边形开始
            Polygon2D result = polygons[0];
            
            // 依次与后续多边形合并
            for (int i = 1; i < polygons.Count; i++)
            {
                result = UnionTwoPolygons(result, polygons[i]);
            }
            
            return result;
        }
        
        /// <summary>
        /// 合并两个多边形（Weiler-Atherton 核心算法）
        /// </summary>
        private Polygon2D UnionTwoPolygons(Polygon2D subject, Polygon2D clip)
        {
            // 1. 计算所有交点
            var intersections = FindIntersections(subject, clip);
            
            // 如果没有交点，检查是否包含关系
            if (intersections.Count == 0)
            {
                // 检查 subject 是否完全包含 clip
                if (subject.ContainsPoint(clip.Vertices[0]))
                    return subject;
                
                // 检查 clip 是否完全包含 subject
                if (clip.ContainsPoint(subject.Vertices[0]))
                    return clip;
                
                // 两个多边形不相交，返回原多边形
                // 实际应该抛出异常或返回多个多边形
                throw new InvalidOperationException("多边形不相交，无法合并");
            }
            
            // 2. 构建交点链表
            var subjectChain = BuildChain(subject, intersections, true);
            var clipChain = BuildChain(clip, intersections, false);
            
            // 3. 遍历链表生成并集轮廓
            var unionVertices = TraceUnionBoundary(subjectChain, clipChain);
            
            // 4. 创建合并后的多边形
            return new Polygon2D(unionVertices, isClosed: true);
        }
        
        /// <summary>
        /// 查找两个多边形的所有交点
        /// </summary>
        private List<IntersectionPoint> FindIntersections(Polygon2D subject, Polygon2D clip)
        {
            var intersections = new List<IntersectionPoint>();
            
            var subjectEdges = subject.GetEdges().ToList();
            var clipEdges = clip.GetEdges().ToList();
            
            for (int i = 0; i < subjectEdges.Count; i++)
            {
                for (int j = 0; j < clipEdges.Count; j++)
                {
                    var intersection = ComputeLineIntersection(
                        subjectEdges[i].StartPoint, subjectEdges[i].EndPoint,
                        clipEdges[j].StartPoint, clipEdges[j].EndPoint);
                    
                    if (intersection.HasValue)
                    {
                        intersections.Add(new IntersectionPoint
                        {
                            Point = intersection.Value,
                            SubjectEdgeIndex = i,
                            ClipEdgeIndex = j,
                            ParameterOnSubject = ComputeParameter(
                                subjectEdges[i].StartPoint, subjectEdges[i].EndPoint, intersection.Value),
                            ParameterOnClip = ComputeParameter(
                                clipEdges[j].StartPoint, clipEdges[j].EndPoint, intersection.Value)
                        });
                    }
                }
            }
            
            return intersections;
        }
        
        /// <summary>
        /// 计算两条线段的交点
        /// </summary>
        private Point2D? ComputeLineIntersection(
            Point2D p1Start, Point2D p1End,
            Point2D p2Start, Point2D p2End)
        {
            double x1 = p1Start.X, y1 = p1Start.Y;
            double x2 = p1End.X, y2 = p1End.Y;
            double x3 = p2Start.X, y3 = p2Start.Y;
            double x4 = p2End.X, y4 = p2End.Y;
            
            double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            
            if (Math.Abs(denom) < _tolerance)
                return null; // 平行或共线
            
            double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
            double u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denom;
            
            // 检查交点是否在线段上
            if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
            {
                double x = x1 + t * (x2 - x1);
                double y = y1 + t * (y2 - y1);
                return new Point2D(x, y);
            }
            
            return null;
        }
        
        /// <summary>
        /// 计算点在线段上的参数位置（0-1）
        /// </summary>
        private double ComputeParameter(Point2D start, Point2D end, Point2D point)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            
            if (Math.Abs(dx) > Math.Abs(dy))
                return (point.X - start.X) / dx;
            else
                return (point.Y - start.Y) / dy;
        }
        
        /// <summary>
        /// 构建顶点链表（插入交点）
        /// </summary>
        private List<ChainVertex> BuildChain(
            Polygon2D polygon,
            List<IntersectionPoint> intersections,
            bool isSubject)
        {
            var chain = new List<ChainVertex>();
            
            // 添加原始顶点
            for (int i = 0; i < polygon.VertexCount; i++)
            {
                chain.Add(new ChainVertex
                {
                    Point = polygon.Vertices[i],
                    IsIntersection = false,
                    EdgeIndex = i
                });
            }
            
            // 插入交点
            var relevantIntersections = isSubject
                ? intersections.OrderBy(x => x.SubjectEdgeIndex).ThenBy(x => x.ParameterOnSubject)
                : intersections.OrderBy(x => x.ClipEdgeIndex).ThenBy(x => x.ParameterOnClip);
            
            foreach (var intersection in relevantIntersections)
            {
                int edgeIndex = isSubject ? intersection.SubjectEdgeIndex : intersection.ClipEdgeIndex;
                
                // 在该边之后插入交点
                int insertIndex = chain.FindIndex(v => v.EdgeIndex == edgeIndex && !v.IsIntersection);
                if (insertIndex >= 0)
                {
                    chain.Insert(insertIndex + 1, new ChainVertex
                    {
                        Point = intersection.Point,
                        IsIntersection = true,
                        EdgeIndex = edgeIndex,
                        Intersection = intersection
                    });
                }
            }
            
            return chain;
        }
        
        /// <summary>
        /// 跟踪并集边界
        /// </summary>
        private List<Point2D> TraceUnionBoundary(
            List<ChainVertex> subjectChain,
            List<ChainVertex> clipChain)
        {
            var result = new List<Point2D>();
            
            // 找到起点（最左下角）
            var startVertex = subjectChain
                .OrderBy(v => v.Point.X)
                .ThenBy(v => v.Point.Y)
                .First();
            
            var current = startVertex;
            var currentChain = subjectChain;
            bool onSubject = true;
            int steps = 0;
            // 跟踪不回到起点时会一直追加顶点。步数超过两条链就放弃，交给 Clipper2。
            int stepLimit = subjectChain.Count + clipChain.Count;
            
            do
            {
                if (++steps > stepLimit)
                    throw new InvalidOperationException("Weiler-Atherton 并集跟踪未闭合");

                result.Add(current.Point);
                
                // 如果是交点，切换到另一条链
                if (current.IsIntersection)
                {
                    currentChain = onSubject ? clipChain : subjectChain;
                    onSubject = !onSubject;
                    
                    // 找到对应的交点
                    current = currentChain.First(v =>
                        v.IsIntersection &&
                        v.Point.DistanceTo(current.Point) < _tolerance);
                }
                
                // 移动到下一个顶点
                int currentIndex = currentChain.IndexOf(current);
                int nextIndex = (currentIndex + 1) % currentChain.Count;
                current = currentChain[nextIndex];
                
            } while (current != startVertex);
            
            return result;
        }
        
        /// <summary>
        /// Clipper2 的小数位数。与距离容差同一量级，上限受库限制。
        /// </summary>
        private const int ClipperPrecision = 3;

        /// <summary>
        /// 使用 Clipper2 做并集（备用方案）。
        /// 外环逆时针，孔洞顺时针；不相交的块分成多个区域。
        /// </summary>
        private PolygonUnionResult UnionUsingClipper2(List<Polygon2D> polygons)
        {
            var paths = new PathsD();
            for (int i = 0; i < polygons.Count; i++)
            {
                var poly = polygons[i];
                if (poly == null || poly.VertexCount < 3)
                    continue;
                paths.Add(ToPath(poly));
            }

            if (paths.Count == 0)
                throw new InvalidOperationException("Clipper2 并集没有有效多边形");

            var tree = new PolyTreeD();
            Clipper.BooleanOp(ClipType.Union, paths, null, tree, FillRule.NonZero, ClipperPrecision);

            var regions = new List<PolygonUnionRegion>();
            CollectRegions(tree, regions);
            if (regions.Count == 0)
                throw new InvalidOperationException("Clipper2 并集为空");

            regions.Sort(CompareRegions);
            return new PolygonUnionResult(regions);
        }

        private void CollectRegions(PolyPathD node, List<PolygonUnionRegion> regions)
        {
            for (int i = 0; i < node.Count; i++)
            {
                var child = node[i];
                if (child.IsHole)
                {
                    if (child.Count > 0)
                        CollectRegions(child, regions);
                    continue;
                }

                var holes = new List<Polygon2D>();
                for (int h = 0; h < child.Count; h++)
                {
                    var holeNode = child[h];
                    if (!holeNode.IsHole)
                        continue;

                    var hole = ToPolygon(holeNode.Polygon, counterClockwise: false);
                    if (hole != null)
                        holes.Add(hole);

                    if (holeNode.Count > 0)
                        CollectRegions(holeNode, regions);
                }

                holes.Sort(ComparePolygons);
                var outer = ToPolygon(child.Polygon, counterClockwise: true);
                if (outer != null)
                    regions.Add(new PolygonUnionRegion(outer, holes));
            }
        }

        private static int CompareRegions(PolygonUnionRegion a, PolygonUnionRegion b)
        {
            return ComparePolygons(a.Outer, b.Outer);
        }

        private static int ComparePolygons(Polygon2D a, Polygon2D b)
        {
            var pa = a.Vertices[0];
            var pb = b.Vertices[0];
            int cmp = pa.X.CompareTo(pb.X);
            if (cmp != 0)
                return cmp;
            return pa.Y.CompareTo(pb.Y);
        }

        private Polygon2D ToPolygon(PathD path, bool counterClockwise)
        {
            if (path == null || path.Count < 3)
                return null;

            var verts = new List<Point2D>(path.Count);
            foreach (var p in path)
                verts.Add(new Point2D(p.x, p.y));

            verts = DropClosingDuplicate(verts);
            verts = DropCollinear(verts);
            if (verts.Count < 3)
                return null;

            var polygon = new Polygon2D(verts, isClosed: true);
            if (polygon.IsCounterClockwise() != counterClockwise)
                polygon = polygon.Reverse();

            return RotateToBottomLeft(polygon);
        }

        private List<Point2D> DropClosingDuplicate(List<Point2D> verts)
        {
            if (verts.Count >= 2 && verts[0].DistanceTo(verts[verts.Count - 1]) <= _tolerance)
                verts.RemoveAt(verts.Count - 1);
            return verts;
        }

        private List<Point2D> DropCollinear(List<Point2D> verts)
        {
            if (verts.Count < 4)
                return verts;

            var kept = new List<Point2D>(verts.Count);
            int n = verts.Count;
            for (int i = 0; i < n; i++)
            {
                var prev = verts[(i - 1 + n) % n];
                var cur = verts[i];
                var next = verts[(i + 1) % n];
                if (cur.DistanceTo(prev) <= _tolerance)
                    continue;

                double cross = (cur.X - prev.X) * (next.Y - cur.Y) - (cur.Y - prev.Y) * (next.X - cur.X);
                double scale = prev.DistanceTo(next);
                if (scale < 1)
                    scale = 1;
                if (Math.Abs(cross) <= _tolerance * scale)
                    continue;

                kept.Add(cur);
            }

            return kept.Count >= 3 ? kept : verts;
        }

        private static Polygon2D RotateToBottomLeft(Polygon2D polygon)
        {
            var verts = polygon.Vertices;
            int start = 0;
            for (int i = 1; i < verts.Count; i++)
            {
                if (verts[i].Y < verts[start].Y
                    || (verts[i].Y == verts[start].Y && verts[i].X < verts[start].X))
                    start = i;
            }

            if (start == 0)
                return polygon;

            var rotated = new List<Point2D>(verts.Count);
            for (int i = 0; i < verts.Count; i++)
                rotated.Add(verts[(start + i) % verts.Count]);
            return new Polygon2D(rotated, polygon.IsClosed);
        }

        private static PathD ToPath(Polygon2D polygon)
        {
            var path = new PathD();
            foreach (var v in polygon.Vertices)
                path.Add(new PointD(v.X, v.Y));
            return path;
        }
    }
    
    /// <summary>
    /// 交点信息
    /// </summary>
    internal class IntersectionPoint
    {
        public Point2D Point { get; set; }
        public int SubjectEdgeIndex { get; set; }
        public int ClipEdgeIndex { get; set; }
        public double ParameterOnSubject { get; set; }
        public double ParameterOnClip { get; set; }
    }
    
    /// <summary>
    /// 链表顶点
    /// </summary>
    internal class ChainVertex
    {
        public Point2D Point { get; set; }
        public bool IsIntersection { get; set; }
        public int EdgeIndex { get; set; }
        public IntersectionPoint Intersection { get; set; }
    }
}

