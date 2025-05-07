using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
namespace CadUtils
{
    public static partial class EtGpt
    {
        #region PolyLine相关操作     
        public static Polyline ResetPolyVertex(this Polyline poly)
        {
            poly = poly.RemovePolyDuplicateVertices();
            poly = poly.SetPolyLineClockWise();
            var pointMin = ((Extents3d)poly.Bounds).MinPoint.Point3dTo2d();
            var points = poly.GetPolyPoint2ds();
            var pointsIndexs = points.Select((x, y) => (Point: x, Index: y))
                .OrderBy(x => x.Point.GetDistanceTo(pointMin));
            var index = pointsIndexs.FirstOrDefault().Index;
            var ps = points.Take(index);
            var pe = points.Skip(index);
            int i = 0;
            foreach (var point in pe)
            {
                poly.SetPointAt(i, point);
                i++;
            }
            foreach (var point in ps)
            {
                poly.SetPointAt(i, point);
                i++;
            }
            return poly;
        }
        public static Polyline RemovePolyDuplicateVertices(this Polyline polyline, Database db = null, string space = null)
        {
            db = db ?? Application.DocumentManager.MdiActiveDocument.Database;
            var pt1 = polyline.GetPoint3dAt(0);
            var pt2 = polyline.GetPoint3dAt(polyline.NumberOfVertices - 1);
            var tolerance = new Tolerance(Tolerance.Global.EqualPoint, 1e-6);
            if (pt1.IsEqualTo(pt2, tolerance))
            {
                polyline.RemoveVertexAt(polyline.NumberOfVertices - 1);
            }
            polyline.Closed = true;
            return polyline;
        }
        /// <summary>
        /// 设置PolyLine的转动方向，如果不为逆时针，改为逆时针转动
        /// </summary>
        /// <param name="poly"></param>
        /// <returns></returns>
        public static Polyline SetPolyLineClockWise(this Polyline poly)
        {
            double area = poly.GetArea();
            if (area < 0)
            {
                poly.ChangeEntityPropertyInDb((x) =>
                {
                    x.ReverseCurve();
                });
            }
            return poly;
        }
        public static Point2d[] GetPolyPoint2ds(this Polyline poly)
        {
            var points = new Point2d[poly.NumberOfVertices];
            for (int i = 0; i < poly.NumberOfVertices; i++)
            {
                points[i] = poly.GetPoint2dAt(i);
            }
            return points;
        }
        public static Point3d[] GetPolyPoint3ds(this Polyline poly)
        {
            var points = new Point3d[poly.NumberOfVertices];
            for (int i = 0; i < poly.NumberOfVertices; i++)
            {
                points[i] = poly.GetPoint3dAt(i);
            }
            return points;
        }
        public static double[] GetPolySegmentAngle(this Polyline pline)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            double[] angles = new double[pline.NumberOfVertices];
            LineSegment3d lineEnd = pline.GetLineSegmentAt(pline.NumberOfVertices - 1);
            LineSegment3d lineStart = pline.GetLineSegmentAt(0);
            double angleStart = GetPolylineShape(lineEnd, lineStart, pline.Normal);
            angles[0] = angleStart;
            //ed.WriteMessage("\nIndex:0  Angle between {0} and {1}: {2}", pline.NumberOfVertices, 0, Converter.AngleToString(angleStart, AngularUnitFormat.Degrees, 2));
            //if (angleStart > Math.PI)
            //    ed.WriteMessage(" ({0:0.0000})", (angleStart - Math.PI * 2.0) * 180.0 / Math.PI);
            for (int i = 0; i < pline.NumberOfVertices - 1; i++)
            {
                LineSegment3d l1 = pline.GetLineSegmentAt(i);
                LineSegment3d l2 = pline.GetLineSegmentAt(i + 1);
                double angle = GetPolylineShape(l1, l2, pline.Normal);
                angles[i + 1] = angle;
                //ed.WriteMessage("\nIndex:{0} Angle between {1} and {2}: {3}", i + 1, i, i + 1, Converter.AngleToString(angle, AngularUnitFormat.Degrees, 2));
                //if (angle > Math.PI)
                //    ed.WriteMessage(" ({0:0.0000})", (angle - Math.PI * 2.0) * 180.0 / Math.PI);
            }
            return angles;
        }
        public static double GetPolylineShape(LineSegment3d l1, LineSegment3d l2, Vector3d normal)
        {
            Vector3d v1 = l1.EndPoint - l1.StartPoint;
            Vector3d v2 = l2.EndPoint - l2.StartPoint;
            return v1.GetAngleTo(v2, normal);
        }
        #endregion
    }
    #region 辅助类型
    /// <summary>
    /// 判断封闭图示PolyLine 是顺指针，还是逆时针
    /// </summary>
    public static class AlgebraicArea
    {
        public static double GetArea(Point2d pt1, Point2d pt2, Point2d pt3)
        {
            return (((pt2.X - pt1.X) * (pt3.Y - pt1.Y)) -
                        ((pt3.X - pt1.X) * (pt2.Y - pt1.Y))) / 2.0;
        }
        public static double GetArea(this CircularArc2d arc)
        {
            double rad = arc.Radius;
            double ang = arc.IsClockWise ? arc.StartAngle - arc.EndAngle : arc.EndAngle - arc.StartAngle;
            return rad * rad * (ang - Math.Sin(ang)) / 2.0;
        }
        public static double GetArea(this Polyline pline)
        {
            CircularArc2d arc = new CircularArc2d();
            double area = 0.0;
            int last = pline.NumberOfVertices - 1;
            Point2d p0 = pline.GetPoint2dAt(0);
            if (pline.GetBulgeAt(0) != 0.0)
            {
                area += pline.GetArcSegment2dAt(0).GetArea();
            }
            for (int i = 1; i < last; i++)
            {
                area += GetArea(p0, pline.GetPoint2dAt(i), pline.GetPoint2dAt(i + 1));
                if (pline.GetBulgeAt(i) != 0.0)
                {
                    area += pline.GetArcSegment2dAt(i).GetArea(); ;
                }
            }
            if ((pline.GetBulgeAt(last) != 0.0) && pline.Closed)
            {
                area += pline.GetArcSegment2dAt(last).GetArea();
            }
            return area;
        }
    }
    #endregion
}
