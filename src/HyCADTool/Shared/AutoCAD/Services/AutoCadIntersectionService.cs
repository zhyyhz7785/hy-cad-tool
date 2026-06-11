using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCAD.Geometry.Interfaces;
using HyCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Shared.AutoCAD.Services
{
    /// <summary>
    /// AutoCAD 平台的射线-多段线交点计算实现
    /// 封装 Line.IntersectWith + Intersect.ExtendThis
    /// 对应旧代码 GetIntersectionByLinetWithBoundary
    /// </summary>
    public class AutoCadIntersectionService : ILineIntersectionService
    {
        public Point2D GetNearestForwardIntersection(
            Point2D segmentEndPoint, Vector2D direction, Polyline2D boundary)
        {
            if (TryGetNearestForwardIntersection(segmentEndPoint, direction, boundary, out Point2D hit))
                return hit;

            return segmentEndPoint;
        }

        public bool TryGetNearestForwardIntersection(
            Point2D segmentEndPoint,
            Vector2D direction,
            Polyline2D boundary,
            out Point2D intersection)
        {
            intersection = segmentEndPoint;

            if (boundary == null || !direction.TryNormalize(out _))
                return false;

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return false;

            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                Line line = null;
                Polyline acadBoundary = null;
                try
                {
                    var pt1 = new Point3d(segmentEndPoint.X, segmentEndPoint.Y, 0);
                    var rawDir = new Vector3d(direction.X, direction.Y, 0);
                    if (rawDir.Length < 1e-10)
                        return false;

                    var dir = rawDir.GetNormal();
                    var pt2 = pt1 + dir;

                    line = new Line(pt1, pt2);
                    acadBoundary = ToAcadPolyline(boundary);

                    var points = new Point3dCollection();
                    line.IntersectWith(acadBoundary, Intersect.ExtendThis, points, IntPtr.Zero, IntPtr.Zero);

                    var forwardPoints = new List<Point3d>();
                    for (int i = 0; i < points.Count; i++)
                    {
                        var vec = points[i] - pt1;
                        if (vec.Length > 1e-10)
                        {
                            var vecNorm = vec.GetNormal();
                            if (dir.DotProduct(vecNorm) > 0.99)
                                forwardPoints.Add(points[i]);
                        }
                    }

                    Point3d closest;
                    if (forwardPoints.Any())
                    {
                        closest = forwardPoints.OrderBy(p => pt1.DistanceTo(p)).First();
                    }
                    else if (points.Count > 0)
                    {
                        closest = Enumerable.Range(0, points.Count)
                            .Select(idx => points[idx])
                            .OrderBy(p => pt1.DistanceTo(p))
                            .First();
                    }
                    else
                    {
                        return false;
                    }

                    tr.Commit();
                    intersection = new Point2D(closest.X, closest.Y);
                    return true;
                }
                catch (System.Exception)
                {
                    return false;
                }
                finally
                {
                    line?.Dispose();
                    acadBoundary?.Dispose();
                }
            }
        }

        private static Polyline ToAcadPolyline(Polyline2D poly)
        {
            var acadPoly = new Polyline();
            for (int i = 0; i < poly.VertexCount; i++)
            {
                var pt = poly.GetPointAt(i);
                acadPoly.AddVertexAt(i, new Point2d(pt.X, pt.Y), 0, 0, 0);
            }
            acadPoly.Closed = poly.IsClosed;
            return acadPoly;
        }
    }
}
