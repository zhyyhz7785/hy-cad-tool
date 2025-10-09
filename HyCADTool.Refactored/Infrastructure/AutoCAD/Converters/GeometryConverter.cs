using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Converters
{
    /// <summary>
    /// 几何类型转换器实现
    /// 负责领域几何对象与 AutoCAD 几何对象之间的转换
    /// </summary>
    public class GeometryConverter : IGeometryConverter
    {
        // ========== Domain → AutoCAD ==========

        public Point2d ToAutoCADPoint2d(Domain.ValueObjects.Geometry.Point2D domainPoint)
        {
            return new Point2d(domainPoint.X, domainPoint.Y);
        }

        public Point3d ToAutoCADPoint3d(Domain.ValueObjects.Geometry.Point3D domainPoint)
        {
            return new Point3d(domainPoint.X, domainPoint.Y, domainPoint.Z);
        }

        public Polyline ToAutoCADPolyline(Domain.ValueObjects.Geometry.Polygon2D domainPolygon)
        {
            var polyline = new Polyline();

            for (int i = 0; i < domainPolygon.Vertices.Count; i++)
            {
                var pt = domainPolygon.Vertices[i];
                polyline.AddVertexAt(i, new Point2d(pt.X, pt.Y), 0, 0, 0);
            }

            polyline.Closed = domainPolygon.IsClosed;
            return polyline;
        }

        public Line ToAutoCADLine(Domain.ValueObjects.Geometry.Line2D domainLine)
        {
            return new Line(
                ToAutoCADPoint3d(new Domain.ValueObjects.Geometry.Point3D(domainLine.StartPoint)),
                ToAutoCADPoint3d(new Domain.ValueObjects.Geometry.Point3D(domainLine.EndPoint))
            );
        }

        // ========== AutoCAD → Domain ==========

        public Domain.ValueObjects.Geometry.Point2D FromAutoCADPoint2d(Point2d acPoint)
        {
            return new Domain.ValueObjects.Geometry.Point2D(acPoint.X, acPoint.Y);
        }

        public Domain.ValueObjects.Geometry.Point3D FromAutoCADPoint3d(Point3d acPoint)
        {
            return new Domain.ValueObjects.Geometry.Point3D(acPoint.X, acPoint.Y, acPoint.Z);
        }

        public Domain.ValueObjects.Geometry.Polygon2D FromAutoCADPolyline(Polyline acPolyline)
        {
            var vertices = new List<Domain.ValueObjects.Geometry.Point2D>();

            for (int i = 0; i < acPolyline.NumberOfVertices; i++)
            {
                var pt = acPolyline.GetPoint2dAt(i);
                vertices.Add(new Domain.ValueObjects.Geometry.Point2D(pt.X, pt.Y));
            }

            return new Domain.ValueObjects.Geometry.Polygon2D(vertices, acPolyline.Closed);
        }

        public Domain.ValueObjects.Geometry.Line2D FromAutoCADLine(Line acLine)
        {
            var start = FromAutoCADPoint3d(acLine.StartPoint).ToPoint2D();
            var end = FromAutoCADPoint3d(acLine.EndPoint).ToPoint2D();
            return new Domain.ValueObjects.Geometry.Line2D(start, end);
        }

        // ========== Circle 转换 ==========

        public Circle ToAutoCADCircle(Domain.ValueObjects.Geometry.Circle2D domainCircle)
        {
            var center = ToAutoCADPoint3d(new Domain.ValueObjects.Geometry.Point3D(domainCircle.Center));
            return new Circle(center, Vector3d.ZAxis, domainCircle.Radius);
        }

        public Domain.ValueObjects.Geometry.Circle2D FromAutoCADCircle(Circle acCircle)
        {
            var center = FromAutoCADPoint3d(acCircle.Center).ToPoint2D();
            return new Domain.ValueObjects.Geometry.Circle2D(center, acCircle.Radius);
        }

        // ========== 批量转换 ==========

        public List<Line> ToAutoCADLines(IEnumerable<Domain.ValueObjects.Geometry.Line2D> domainLines)
        {
            var result = new List<Line>();
            foreach (var line in domainLines)
            {
                result.Add(ToAutoCADLine(line));
            }
            return result;
        }

        public List<Domain.ValueObjects.Geometry.Line2D> FromAutoCADLines(IEnumerable<Line> acLines)
        {
            var result = new List<Domain.ValueObjects.Geometry.Line2D>();
            foreach (var line in acLines)
            {
                result.Add(FromAutoCADLine(line));
            }
            return result;
        }
    }
}

