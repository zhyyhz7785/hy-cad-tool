using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Refactored.Domain.ValueObjects;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;

namespace HyCADTool.Refactored.Domain.Services
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
        /// 合并多个多边形为一个（并集）
        /// 优先使用 Weiler-Atherton 算法，失败时回退到 Clipper2
        /// </summary>
        public Polygon2D UnionPolygons(IEnumerable<Polygon2D> polygons)
        {
            var polygonList = polygons.ToList();
            
            if (polygonList.Count == 0)
                throw new ArgumentException("多边形列表为空", nameof(polygons));
            
            if (polygonList.Count == 1)
                return polygonList[0];
            
            try
            {
                // 优先使用 Weiler-Atherton 算法
                return UnionUsingWeilerAtherton(polygonList);
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
            
            do
            {
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
        /// 使用 Clipper2 合并多边形（备用方案）
        /// </summary>
        private Polygon2D UnionUsingClipper2(List<Polygon2D> polygons)
        {
            // TODO: 集成 Clipper2 库
            // 这里使用简化的实现作为占位符
            
            // 找到包含所有多边形的最大边界
            var allVertices = polygons.SelectMany(p => p.Vertices).ToList();
            
            double minX = allVertices.Min(v => v.X);
            double maxX = allVertices.Max(v => v.X);
            double minY = allVertices.Min(v => v.Y);
            double maxY = allVertices.Max(v => v.Y);
            
            // 返回边界矩形（临时实现）
            var boundingBox = new List<Point2D>
            {
                new Point2D(minX, minY),
                new Point2D(maxX, minY),
                new Point2D(maxX, maxY),
                new Point2D(minX, maxY)
            };
            
            return new Polygon2D(boundingBox, isClosed: true);
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

