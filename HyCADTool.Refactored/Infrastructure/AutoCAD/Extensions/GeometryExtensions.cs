using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions
{
    /// <summary>
    /// AutoCAD几何对象扩展方法
    /// AutoCAD Geometry Object Extension Methods
    /// </summary>
    public static class GeometryExtensions
    {
        /// <summary>
        /// 将AutoCAD Line转换为Domain Line2D
        /// Convert AutoCAD Line to Domain Line2D
        /// </summary>
        public static Line2D ToDomainLine2D(this Line line)
        {
            var startPoint = new Point2D(line.StartPoint.X, line.StartPoint.Y);
            var endPoint = new Point2D(line.EndPoint.X, line.EndPoint.Y);
            return new Line2D(startPoint, endPoint);
        }

        /// <summary>
        /// 将Domain Line2D转换为AutoCAD Line
        /// Convert Domain Line2D to AutoCAD Line
        /// </summary>
        public static Line ToAcadLine(this Line2D line)
        {
            var startPoint = new Point3d(line.StartPoint.X, line.StartPoint.Y, 0);
            var endPoint = new Point3d(line.EndPoint.X, line.EndPoint.Y, 0);
            return new Line(startPoint, endPoint);
        }

        /// <summary>
        /// 将AutoCAD Point3d转换为Domain Point2D
        /// Convert AutoCAD Point3d to Domain Point2D
        /// </summary>
        public static Point2D ToDomainPoint2D(this Point3d point)
        {
            return new Point2D(point.X, point.Y);
        }

        /// <summary>
        /// 将Domain Point2D转换为AutoCAD Point3d
        /// Convert Domain Point2D to AutoCAD Point3d
        /// </summary>
        public static Point3d ToAcadPoint3d(this Point2D point, double z = 0)
        {
            return new Point3d(point.X, point.Y, z);
        }

        /// <summary>
        /// 将AutoCAD Polyline转换为Domain Polygon2D
        /// Convert AutoCAD Polyline to Domain Polygon2D
        /// </summary>
        public static Polygon2D ToDomainPolygon2D(this Polyline polyline)
        {
            var vertices = new List<Point2D>();
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                var pt = polyline.GetPoint2dAt(i);
                vertices.Add(new Point2D(pt.X, pt.Y));
            }
            return new Polygon2D(vertices);
        }

        /// <summary>
        /// 将Domain Polygon2D转换为AutoCAD Polyline
        /// Convert Domain Polygon2D to AutoCAD Polyline
        /// </summary>
        public static Polyline ToAcadPolyline(this Polygon2D polygon)
        {
            var polyline = new Polyline();
            for (int i = 0; i < polygon.Vertices.Count; i++)
            {
                var vertex = polygon.Vertices[i];
                polyline.AddVertexAt(i, new Point2d(vertex.X, vertex.Y), 0, 0, 0);
            }
            polyline.Closed = true;
            return polyline;
        }

        /// <summary>
        /// 将AutoCAD Circle转换为Domain Circle2D
        /// Convert AutoCAD Circle to Domain Circle2D
        /// </summary>
        public static Circle2D ToDomainCircle2D(this Circle circle)
        {
            var center = new Point2D(circle.Center.X, circle.Center.Y);
            return new Circle2D(center, circle.Radius);
        }

        /// <summary>
        /// 将Domain Circle2D转换为AutoCAD Circle
        /// Convert Domain Circle2D to AutoCAD Circle
        /// </summary>
        public static Circle ToAcadCircle(this Circle2D circle)
        {
            var center = new Point3d(circle.Center.X, circle.Center.Y, 0);
            var normal = Vector3d.ZAxis;
            return new Circle(center, normal, circle.Radius);
        }

        /// <summary>
        /// 将AutoCAD Vector3d转换为Domain Vector2D
        /// Convert AutoCAD Vector3d to Domain Vector2D
        /// </summary>
        public static Vector2D ToDomainVector2D(this Vector3d vector)
        {
            return new Vector2D(vector.X, vector.Y);
        }

        /// <summary>
        /// 将Domain Vector2D转换为AutoCAD Vector3d
        /// Convert Domain Vector2D to AutoCAD Vector3d
        /// </summary>
        public static Vector3d ToAcadVector3d(this Vector2D vector)
        {
            return new Vector3d(vector.X, vector.Y, 0);
        }

        /// <summary>
        /// 获取Polyline的所有顶点作为Point2D集合
        /// Get all vertices of Polyline as Point2D collection
        /// </summary>
        public static IEnumerable<Point2D> GetVertices2D(this Polyline polyline)
        {
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                var pt = polyline.GetPoint2dAt(i);
                yield return new Point2D(pt.X, pt.Y);
            }
        }

        /// <summary>
        /// 获取Polyline的所有线段作为Line2D集合
        /// Get all segments of Polyline as Line2D collection
        /// </summary>
        public static IEnumerable<Line2D> GetSegments2D(this Polyline polyline)
        {
            for (int i = 0; i < polyline.NumberOfVertices - 1; i++)
            {
                var start = polyline.GetPoint2dAt(i);
                var end = polyline.GetPoint2dAt(i + 1);
                yield return new Line2D(
                    new Point2D(start.X, start.Y),
                    new Point2D(end.X, end.Y)
                );
            }

            // 如果多段线是闭合的，添加最后一段
            if (polyline.Closed && polyline.NumberOfVertices > 0)
            {
                var start = polyline.GetPoint2dAt(polyline.NumberOfVertices - 1);
                var end = polyline.GetPoint2dAt(0);
                yield return new Line2D(
                    new Point2D(start.X, start.Y),
                    new Point2D(end.X, end.Y)
                );
            }
        }

        /// <summary>
        /// 计算Curve的长度（2D投影）
        /// Calculate curve length (2D projection)
        /// </summary>
        public static double GetLength2D(this Curve curve)
        {
            return curve.GetDistanceAtParameter(curve.EndParam) -
                   curve.GetDistanceAtParameter(curve.StartParam);
        }

        /// <summary>
        /// 判断点是否在Polyline内部（2D）
        /// Check if point is inside Polyline (2D)
        /// </summary>
        public static bool Contains2D(this Polyline polyline, Point2D point)
        {
            var polygon = polyline.ToDomainPolygon2D();
            // TODO: 使用 IPolygonAlgorithmService.ContainsPoint
            return false; // Placeholder
        }

        /// <summary>
        /// 获取Polyline的边界框
        /// Get bounding box of Polyline
        /// </summary>
        public static BoundingBox GetBoundingBox2D(this Polyline polyline)
        {
            var vertices = polyline.GetVertices2D().ToList();
            if (vertices.Count == 0)
                return new BoundingBox(Point2D.Origin, Point2D.Origin);

            var minX = vertices.Min(p => p.X);
            var minY = vertices.Min(p => p.Y);
            var maxX = vertices.Max(p => p.X);
            var maxY = vertices.Max(p => p.Y);

            return new BoundingBox(
                new Point2D(minX, minY),
                new Point2D(maxX, maxY)
            );
        }

        /// <summary>
        /// 获取Line的中点
        /// Get midpoint of Line
        /// </summary>
        public static Point2D GetMidpoint2D(this Line line)
        {
            var domainLine = line.ToDomainLine2D();
            return domainLine.MidPoint;
        }

        /// <summary>
        /// 获取Line的方向向量（2D）
        /// Get direction vector of Line (2D)
        /// </summary>
        public static Vector2D GetDirection2D(this Line line)
        {
            var domainLine = line.ToDomainLine2D();
            return domainLine.Direction;
        }
    }
}

