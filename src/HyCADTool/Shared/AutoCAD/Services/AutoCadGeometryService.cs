using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Shared.Geometry.Interfaces;
using HyCADTool.Shared.Geometry;
using HyCADTool.Shared.Geometry.Algorithms;
using HyCADTool.Shared.AutoCAD.Converters;
using Clipper2Lib;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Shared.AutoCAD.Services
{
    /// <summary>
    /// AutoCAD 几何服务实现
    /// 使用 Clipper2 库进行布尔运算
    /// </summary>
    public class AutoCadGeometryService : IGeometryService
    {
        private readonly IGeometryConverter _converter;
        private const double ClipperScale = 1000.0; // Clipper2 缩放因子

        public AutoCadGeometryService(IGeometryConverter converter)
        {
            _converter = converter;
        }

        public Polygon2D CreatePolygonFromPoints(IEnumerable<Point2D> points)
        {
            return new Polygon2D(points);
        }

        public bool IsPointInsidePolygon(Point2D point, Polygon2D polygon)
        {
            // 使用领域对象的方法
            return polygon.ContainsPoint(point);
        }

        public IEnumerable<Point2D> GetIntersectionPoints(Line2D line1, Line2D line2)
        {
            // 使用参数方程求解线段交点
            var p1 = line1.StartPoint;
            var p2 = line1.EndPoint;
            var p3 = line2.StartPoint;
            var p4 = line2.EndPoint;

            double x1 = p1.X, y1 = p1.Y;
            double x2 = p2.X, y2 = p2.Y;
            double x3 = p3.X, y3 = p3.Y;
            double x4 = p4.X, y4 = p4.Y;

            double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);

            if (System.Math.Abs(denom) < 1e-10)
            {
                // 平行或重合，没有交点
                return Enumerable.Empty<Point2D>();
            }

            double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
            double u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denom;

            if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
            {
                // 交点在两条线段内
                double ix = x1 + t * (x2 - x1);
                double iy = y1 + t * (y2 - y1);
                return new[] { new Point2D(ix, iy) };
            }

            return Enumerable.Empty<Point2D>();
        }

        public IEnumerable<Polygon2D> Union(IEnumerable<Polygon2D> polygons)
        {
            var polygonList = polygons.ToList();
            if (!polygonList.Any())
                return Enumerable.Empty<Polygon2D>();

            // 转换为 Clipper2 路径（统一使用 PathD）
            var paths = polygonList.Select(ConvertToPathD).ToList();

            // 执行并集操作
            var solution = Clipper.Union(new PathsD(paths), FillRule.NonZero);

            // 转换回领域对象
            return solution.Select(path => CreatePolygonFromPathD(path));
        }

        public IEnumerable<Polygon2D> Difference(Polygon2D subject, Polygon2D clip)
        {
            var subjectPath = ConvertToPathD(subject);
            var clipPath = ConvertToPathD(clip);

            var solution = Clipper.Difference(
                new PathsD { subjectPath },
                new PathsD { clipPath },
                FillRule.NonZero
            );

            return solution.Select(path => CreatePolygonFromPathD(path));
        }

        public IEnumerable<Polygon2D> Intersection(Polygon2D subject, Polygon2D clip)
        {
            var subjectPath = ConvertToPathD(subject);
            var clipPath = ConvertToPathD(clip);

            var solution = Clipper.Intersect(
                new PathsD { subjectPath },
                new PathsD { clipPath },
                FillRule.NonZero
            );

            return solution.Select(path => CreatePolygonFromPathD(path));
        }

        public IEnumerable<Polygon2D> Offset(Polygon2D polygon, double distance)
        {
            var path = ConvertToPathD(polygon);

            // 使用 Clipper2 的 InflatePaths 进行偏移
            var solution = Clipper.InflatePaths(
                new PathsD { path },
                distance,
                JoinType.Miter,
                EndType.Polygon,
                2.0  // miterLimit
            );

            return solution.Select(p => CreatePolygonFromPathD(p));
        }

        public IEnumerable<Polygon2D> Xor(Polygon2D polygon1, Polygon2D polygon2)
        {
            var path1 = ConvertToPathD(polygon1);
            var path2 = ConvertToPathD(polygon2);

            var solution = Clipper.Xor(
                new PathsD { path1 },
                new PathsD { path2 },
                FillRule.NonZero
            );

            return solution.Select(path => CreatePolygonFromPathD(path));
        }

        public Polygon2D ComputeConvexHull(IEnumerable<Point2D> points)
        {
            // 调用领域层的 Graham 扫描算法
            return ConvexHullAlgorithm.GrahamScan(points);
        }

        public List<Line2D> MergeOverlappingLines(IEnumerable<Line2D> lines, double tolerance)
        {
            // 调用领域层的线段合并算法
            return LineAlgorithms.MergeCollinearLines(lines, tolerance).ToList();
        }

        #region Clipper2 转换辅助方法

        private Path64 ConvertToPath64(Polygon2D polygon)
        {
            var path = new Path64();
            foreach (var vertex in polygon.Vertices)
            {
                path.Add(new Point64(
                    (long)(vertex.X * ClipperScale),
                    (long)(vertex.Y * ClipperScale)
                ));
            }
            return path;
        }

        private PathD ConvertToPathD(Polygon2D polygon)
        {
            var path = new PathD();
            foreach (var vertex in polygon.Vertices)
            {
                path.Add(new PointD(vertex.X, vertex.Y));
            }
            return path;
        }

        private PathD ConvertPath64ToPathD(Path64 path)
        {
            var pathD = new PathD();
            foreach (var point in path)
            {
                pathD.Add(new PointD(
                    point.X / ClipperScale,
                    point.Y / ClipperScale
                ));
            }
            return pathD;
        }

        private Polygon2D CreatePolygonFromPath64(Path64 path)
        {
            var vertices = new List<Point2D>();
            foreach (var point in path)
            {
                vertices.Add(new Point2D(
                    point.X / ClipperScale,
                    point.Y / ClipperScale
                ));
            }
            return new Polygon2D(vertices);
        }

        private Polygon2D CreatePolygonFromPathD(PathD path)
        {
            var vertices = path.Select(p => new Point2D(p.x, p.y)).ToList();
            return new Polygon2D(vertices);
        }

        #endregion
    }
}

