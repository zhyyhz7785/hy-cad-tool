using HyCADTool.Shared.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Shared.Geometry.Algorithms
{
    /// <summary>
    /// 几何转换服务实现（平台无关）
    /// 提供几何对象之间的转换算法实现
    /// </summary>
    public class GeometryConverterService : IGeometryConverterService
    {
        private readonly ILineAlgorithmService _lineAlgorithms;
        private readonly IPolygonAlgorithmService _polygonAlgorithms;
        private readonly IPointAlgorithmService _pointAlgorithms;

        public GeometryConverterService(
            ILineAlgorithmService lineAlgorithms,
            IPolygonAlgorithmService polygonAlgorithms,
            IPointAlgorithmService pointAlgorithms)
        {
            _lineAlgorithms = lineAlgorithms ?? throw new ArgumentNullException(nameof(lineAlgorithms));
            _polygonAlgorithms = polygonAlgorithms ?? throw new ArgumentNullException(nameof(polygonAlgorithms));
            _pointAlgorithms = pointAlgorithms ?? throw new ArgumentNullException(nameof(pointAlgorithms));
        }

        // === 线段与多边形转换 ===

        public Polygon2D LineToPolygon(Line2D line)
        {
            if (line == default)
                return null;

            var points = new List<Point2D> { line.StartPoint, line.EndPoint };
            return new Polygon2D(points, false);
        }

        public Polygon2D LinesToPolygon(List<Line2D> lines, Tolerance tolerance)
        {
            if (lines == default || lines.Count == 0)
                return null;

            return _polygonAlgorithms.CreateFromLines(lines, tolerance);
        }

        public List<Line2D> PolygonToLines(Polygon2D polygon)
        {
            if (polygon?.Vertices == default || polygon.Vertices.Count < 2)
                return new List<Line2D>();

            return _polygonAlgorithms.GetEdges(polygon);
        }

        // === 多边形简化与复杂化 ===

        public Polygon2D PolygonToBoundingBox(Polygon2D polygon)
        {
            if (polygon?.Vertices == default || polygon.Vertices.Count == 0)
                return null;

            var (minPoint, maxPoint) = _polygonAlgorithms.CalculateBoundingBox(polygon);
            return _polygonAlgorithms.CreateRectangle(minPoint, maxPoint);
        }

        public Polygon2D PolygonToConvexHull(Polygon2D polygon)
        {
            if (polygon?.Vertices == default || polygon.Vertices.Count < 3)
                return polygon;

            var hullPoints = _pointAlgorithms.CalculateConvexHull(polygon.Vertices.ToList());
            return new Polygon2D(hullPoints, true);
        }

        public Polygon2D RectangleToPolygon(Point2D minPoint, Point2D maxPoint)
        {
            return _polygonAlgorithms.CreateRectangle(minPoint, maxPoint);
        }

        public Polygon2D CircleToPolygon(Point2D center, double radius, int segments = 32)
        {
            return _polygonAlgorithms.CreateCircle(center, radius, segments);
        }

        // === 点集合转换 ===

        public Polygon2D PointsToPolygon(List<Point2D> points, bool isClosed = true)
        {
            return _polygonAlgorithms.CreateFromPoints(points, isClosed);
        }

        public List<Point2D> PolygonToPoints(Polygon2D polygon)
        {
            if (polygon?.Vertices == default)
                return new List<Point2D>();

            return new List<Point2D>(polygon.Vertices);
        }

        public List<Line2D> PointsToLines(List<Point2D> points, bool isClosed = false)
        {
            var lines = new List<Line2D>();
            if (points == default || points.Count < 2)
                return lines;

            for (int i = 0; i < points.Count - 1; i++)
            {
                lines.Add(new Line2D(points[i], points[i + 1]));
            }

            if (isClosed && points.Count > 2)
            {
                lines.Add(new Line2D(points.Last(), points.First()));
            }

            return lines;
        }

        // === 几何格式化 ===

        public Polygon2D NormalizePolygon(Polygon2D polygon, bool clockwise, Tolerance tolerance)
        {
            if (polygon?.Vertices == default)
                return polygon;

            // 去除重复顶点
            var normalized = _polygonAlgorithms.RemoveDuplicateVertices(polygon, tolerance);

            // 设置方向
            if (clockwise)
                normalized = _polygonAlgorithms.SetClockwise(normalized);
            else
                normalized = _polygonAlgorithms.SetCounterclockwise(normalized);

            // 重排顶点顺序
            normalized = _polygonAlgorithms.ResetVertexOrder(normalized);

            return normalized;
        }

        public Line2D NormalizeLine(Line2D line)
        {
            if (line == default)
                return default;

            // 确保起点的坐标在终点之前
            var start = line.StartPoint;
            var end = line.EndPoint;

            if (start.X > end.X || (System.Math.Abs(start.X - end.X) < 1e-10 && start.Y > end.Y))
            {
                return new Line2D(end, start);
            }

            return line;
        }

        public Point2D NormalizePoint(Point2D point, int precision = 6)
        {
            if (point == default)
                return default;

            double factor = System.Math.Pow(10, precision);
            double x = System.Math.Round(point.X * factor) / factor;
            double y = System.Math.Round(point.Y * factor) / factor;

            return new Point2D(x, y);
        }

        // === 几何分割 ===

        public List<Line2D> DivideLine(Line2D line, int segments)
        {
            var lines = new List<Line2D>();
            if (line == default || segments <= 0)
                return lines;

            if (segments == 1)
            {
                lines.Add(line);
                return lines;
            }

            var points = DivideLineBySegments(line, segments);
            for (int i = 0; i < points.Count - 1; i++)
            {
                lines.Add(new Line2D(points[i], points[i + 1]));
            }

            return lines;
        }

        public List<Point2D> DivideLineByDistance(Line2D line, double interval)
        {
            var points = new List<Point2D>();
            if (line == default || interval <= 0)
                return points;

            double length = _lineAlgorithms.CalculateLength(line);
            int segments = (int)System.Math.Ceiling(length / interval);

            return DivideLineBySegments(line, segments);
        }

        public List<Point2D> DividePolygonByGrid(Polygon2D polygon, double gridSizeX, double gridSizeY)
        {
            var gridPoints = new List<Point2D>();
            if (polygon?.Vertices == default || gridSizeX <= 0 || gridSizeY <= 0)
                return gridPoints;

            var (minPoint, maxPoint) = _polygonAlgorithms.CalculateBoundingBox(polygon);
            if (minPoint == default || maxPoint == default)
                return gridPoints;

            for (double x = minPoint.X; x <= maxPoint.X; x += gridSizeX)
            {
                for (double y = minPoint.Y; y <= maxPoint.Y; y += gridSizeY)
                {
                    var point = new Point2D(x, y);
                    if (_polygonAlgorithms.IsPointInside(point, polygon, new Tolerance(1e-10)))
                    {
                        gridPoints.Add(point);
                    }
                }
            }

            return gridPoints;
        }

        // === 几何合并 ===

        public List<Line2D> MergeCollinearLines(List<Line2D> lines, Tolerance tolerance)
        {
            if (lines == default || lines.Count == 0)
                return new List<Line2D>();

            return _lineAlgorithms.MergeOverlappingLines(lines, tolerance);
        }

        public List<Polygon2D> MergeAdjacentPolygons(List<Polygon2D> polygons, Tolerance tolerance)
        {
            // 这是一个复杂的算法，这里提供简化实现
            // 实际项目中可能需要更复杂的拓扑分析
            
            if (polygons == default || polygons.Count <= 1)
                return new List<Polygon2D>(polygons ?? new List<Polygon2D>());

            var merged = new List<Polygon2D>();
            var processed = new HashSet<int>();

            for (int i = 0; i < polygons.Count; i++)
            {
                if (processed.Contains(i))
                    continue;

                var current = polygons[i];
                processed.Add(i);

                // 查找相邻的多边形
                for (int j = i + 1; j < polygons.Count; j++)
                {
                    if (processed.Contains(j))
                        continue;

                    if (_polygonAlgorithms.DoPolygonsIntersect(current, polygons[j], tolerance))
                    {
                        // 简化合并：使用边界框
                        var bbox1 = _polygonAlgorithms.CalculateBoundingBox(current);
                        var bbox2 = _polygonAlgorithms.CalculateBoundingBox(polygons[j]);

                        var minX = System.Math.Min(bbox1.MinPoint.X, bbox2.MinPoint.X);
                        var minY = System.Math.Min(bbox1.MinPoint.Y, bbox2.MinPoint.Y);
                        var maxX = System.Math.Max(bbox1.MaxPoint.X, bbox2.MaxPoint.X);
                        var maxY = System.Math.Max(bbox1.MaxPoint.Y, bbox2.MaxPoint.Y);

                        current = _polygonAlgorithms.CreateRectangle(
                            new Point2D(minX, minY), 
                            new Point2D(maxX, maxY));
                        processed.Add(j);
                    }
                }

                merged.Add(current);
            }

            return merged;
        }

        // === 坐标系转换 ===

        public Point2D TransformPoint(Point2D point, Point2D translation, double rotation, double scale, Point2D origin)
        {
            if (origin.Equals(default(Point2D)))
                origin = new Point2D(0, 0);
            
            if (translation.Equals(default(Point2D)))
                translation = new Point2D(0, 0);

            // 1. 平移到原点
            var translated = _pointAlgorithms.Translate(point, -origin.X, -origin.Y);
            if (!translated.HasValue) return point;

            // 2. 缩放
            var scaled = _pointAlgorithms.Scale(translated.Value, new Point2D(0, 0), scale, scale);
            if (!scaled.HasValue) return point;

            // 3. 旋转
            var rotated = _pointAlgorithms.Rotate(scaled.Value, new Point2D(0, 0), rotation);
            if (!rotated.HasValue) return point;

            // 4. 平移回去并应用最终平移
            var final = _pointAlgorithms.Translate(rotated.Value, origin.X + translation.X, origin.Y + translation.Y);

            return final.HasValue ? final.Value : point;
        }

        public List<Point2D> TransformPoints(List<Point2D> points, Point2D translation, double rotation, double scale, Point2D origin)
        {
            if (points == default)
                return new List<Point2D>();

            return points.Select(p => TransformPoint(p, translation, rotation, scale, origin)).ToList();
        }

        public Line2D TransformLine(Line2D line, Point2D translation, double rotation, double scale, Point2D origin)
        {
            if (line == default)
                return default;

            var startTransformed = TransformPoint(line.StartPoint, translation, rotation, scale, origin);
            var endTransformed = TransformPoint(line.EndPoint, translation, rotation, scale, origin);

            return new Line2D(startTransformed, endTransformed);
        }

        public Polygon2D TransformPolygon(Polygon2D polygon, Point2D translation, double rotation, double scale, Point2D origin)
        {
            if (polygon?.Vertices == default)
                return polygon;

            var transformedPoints = TransformPoints(polygon.Vertices.ToList(), translation, rotation, scale, origin);
            return new Polygon2D(transformedPoints, polygon.IsClosed);
        }

        // === 几何验证与修复 ===

        public GeometryValidationResult ValidatePolygon(Polygon2D polygon, Tolerance tolerance)
        {
            var result = new GeometryValidationResult { IsValid = true };

            if (polygon?.Vertices == default)
            {
                result.IsValid = false;
                result.Errors.Add("Polygon is null or has no points");
                return result;
            }

            if (polygon.Vertices.Count < 3)
            {
                result.IsValid = false;
                result.Errors.Add("Polygon must have at least 3 points");
            }

            // 检查重复点
            var duplicateGroups = polygon.Vertices
                .Select((point, index) => new { Point = point, Index = index })
                .GroupBy(x => x.Point, new Point2DComparer(tolerance))
                .Where(g => g.Count() > 1);

            foreach (var group in duplicateGroups)
            {
                result.Warnings.Add($"Duplicate points found at indices: {string.Join(", ", group.Select(x => x.Index))}");
            }

            // 检查自相交
            if (!_polygonAlgorithms.IsSimple(polygon, tolerance))
            {
                result.IsValid = false;
                result.Errors.Add("Polygon has self-intersections");
            }

            return result;
        }

        public Polygon2D RepairPolygon(Polygon2D polygon, Tolerance tolerance)
        {
            if (polygon?.Vertices == default)
                return polygon;

            // 去除重复顶点
            var repaired = _polygonAlgorithms.RemoveDuplicateVertices(polygon, tolerance);

            // 确保至少有3个点
            if (repaired.Vertices.Count < 3)
                return null;

            // 设置为逆时针方向（标准约定）
            repaired = _polygonAlgorithms.SetCounterclockwise(repaired);

            return repaired;
        }

        public GeometryValidationResult ValidateLine(Line2D line, Tolerance tolerance)
        {
            var result = new GeometryValidationResult { IsValid = true };

            if (line == default)
            {
                result.IsValid = false;
                result.Errors.Add("Line is null");
                return result;
            }

            if (line.StartPoint == default || line.EndPoint == default)
            {
                result.IsValid = false;
                result.Errors.Add("Line has null start or end point");
                return result;
            }

            // 检查线段长度
            double length = _lineAlgorithms.CalculateLength(line);
            if (length < tolerance.Value)
            {
                result.Warnings.Add("Line length is very small, might be degenerate");
            }

            return result;
        }

        // === 几何属性提取 ===

        public List<Point2D> ExtractFeaturePoints(Polygon2D polygon, Tolerance tolerance)
        {
            var featurePoints = new List<Point2D>();
            if (polygon?.Vertices == default || polygon.Vertices.Count < 3)
                return featurePoints;

            var points = polygon.Vertices;
            for (int i = 0; i < points.Count; i++)
            {
                int prev = (i - 1 + points.Count) % points.Count;
                int next = (i + 1) % points.Count;

                // 计算转角
                double angle = _pointAlgorithms.CalculateAngleAt(points[prev], points[i], points[next]);
                
                // 如果转角显著（非直线），则为特征点
                if (System.Math.Abs(angle - System.Math.PI) > tolerance.Value)
                {
                    featurePoints.Add(points[i]);
                }
            }

            return featurePoints;
        }

        public GeometryStatistics CalculateStatistics(Polygon2D polygon)
        {
            var stats = new GeometryStatistics();
            
            if (polygon?.Vertices == default)
                return stats;

            stats.VertexCount = polygon.Vertices.Count;
            stats.EdgeCount = _polygonAlgorithms.GetEdges(polygon).Count;
            stats.Area = _polygonAlgorithms.CalculateArea(polygon);
            stats.Perimeter = _polygonAlgorithms.CalculatePerimeter(polygon);
            stats.Centroid = _polygonAlgorithms.CalculateCentroid(polygon);
            stats.BoundingBox = _polygonAlgorithms.CalculateBoundingBox(polygon);
            stats.IsConvex = _polygonAlgorithms.IsConvex(polygon, new Tolerance(1e-10));
            stats.IsSimple = _polygonAlgorithms.IsSimple(polygon, new Tolerance(1e-10));
            stats.IsClockwise = _polygonAlgorithms.IsClockwise(polygon);

            return stats;
        }

        // === 私有辅助方法 ===

        private List<Point2D> DivideLineBySegments(Line2D line, int segments)
        {
            var points = new List<Point2D>();
            if (line == default || segments <= 0)
                return points;

            points.Add(line.StartPoint);

            if (segments > 1)
            {
                double dx = line.EndPoint.X - line.StartPoint.X;
                double dy = line.EndPoint.Y - line.StartPoint.Y;

                for (int i = 1; i < segments; i++)
                {
                    double ratio = (double)i / segments;
                    double x = line.StartPoint.X + ratio * dx;
                    double y = line.StartPoint.Y + ratio * dy;
                    points.Add(new Point2D(x, y));
                }
            }

            points.Add(line.EndPoint);
            return points;
        }
    }

}
