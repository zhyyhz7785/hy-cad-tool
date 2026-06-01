using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCAD.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Shared.AutoCAD.Converters
{
    /// <summary>
    /// Polyline2D 与 AutoCAD Polyline 之间的转换器
    /// </summary>
    public static class ReinforcementConverter
    {
        /// <summary>
        /// AutoCAD Polyline → Domain Polyline2D
        /// </summary>
        public static Polyline2D ToDomainPolyline(this Polyline acadPoly)
        {
            var vertices = new List<Point2D>();
            for (int i = 0; i < acadPoly.NumberOfVertices; i++)
            {
                var pt = acadPoly.GetPoint2dAt(i);
                vertices.Add(new Point2D(pt.X, pt.Y));
            }
            return new Polyline2D(vertices, acadPoly.Closed);
        }

        /// <summary>
        /// Domain Polyline2D → AutoCAD Polyline
        /// </summary>
        public static Polyline ToAcadPolyline(this Polyline2D domainPoly)
        {
            var acadPoly = new Polyline();
            for (int i = 0; i < domainPoly.VertexCount; i++)
            {
                var pt = domainPoly.GetPointAt(i);
                acadPoly.AddVertexAt(i, new Point2d(pt.X, pt.Y), 0, 0, 0);
            }
            acadPoly.Closed = domainPoly.IsClosed;
            return acadPoly;
        }

        /// <summary>
        /// Domain Point2D → AutoCAD Point3d
        /// </summary>
        public static Point3d ToAcadPoint3d(this Point2D pt)
        {
            return new Point3d(pt.X, pt.Y, 0);
        }

        /// <summary>
        /// AutoCAD Point3d → Domain Point2D
        /// </summary>
        public static Point2D ToDomainPoint2D(this Point3d pt)
        {
            return new Point2D(pt.X, pt.Y);
        }

        /// <summary>
        /// Domain Point2D[] → AutoCAD Point3d[]
        /// </summary>
        public static Point3d[] ToAcadPoint3dArray(this Point2D[] points)
        {
            var result = new Point3d[points.Length];
            for (int i = 0; i < points.Length; i++)
                result[i] = points[i].ToAcadPoint3d();
            return result;
        }
    }
}
