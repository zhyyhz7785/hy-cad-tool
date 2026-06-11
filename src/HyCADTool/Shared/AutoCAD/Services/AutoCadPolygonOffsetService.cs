using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCAD.Geometry.Interfaces;
using HyCAD.Geometry;
using System;
using System.Collections.Generic;

namespace HyCADTool.Shared.AutoCAD.Services
{
    /// <summary>
    /// AutoCAD 平台的多段线偏移实现
    /// 封装 Polyline.GetOffsetCurves
    /// </summary>
    public class AutoCadPolygonOffsetService : IPolygonOffsetService
    {
        public Polyline2D Offset(Polyline2D polyline, double offsetDistance)
        {
            if (polyline == null || polyline.VertexCount < 2)
                return null;

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return null;

            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                Polyline acadPoly = null;
                try
                {
                    acadPoly = ToAcadPolyline(polyline);
                    var offsetCurves = acadPoly.GetOffsetCurves(offsetDistance);
                    if (offsetCurves == null || offsetCurves.Count == 0)
                        return null;

                    var result = PickLargestOffset(offsetCurves);
                    tr.Commit();
                    return result;
                }
                catch (System.Exception)
                {
                    return null;
                }
                finally
                {
                    acadPoly?.Dispose();
                }
            }
        }

        private static Polyline2D PickLargestOffset(DBObjectCollection offsetCurves)
        {
            Polyline2D best = null;
            double bestArea = 0;

            foreach (Entity ent in offsetCurves)
            {
                try
                {
                    if (ent is Polyline p)
                    {
                        var domain = FromAcadPolyline(p);
                        double area = Math.Abs(domain.GetSignedArea());
                        if (area > bestArea)
                        {
                            bestArea = area;
                            best = domain;
                        }
                    }
                }
                finally
                {
                    ent?.Dispose();
                }
            }

            return best;
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

        private static Polyline2D FromAcadPolyline(Polyline acadPoly)
        {
            var vertices = new List<Point2D>();
            for (int i = 0; i < acadPoly.NumberOfVertices; i++)
            {
                var pt = acadPoly.GetPoint2dAt(i);
                vertices.Add(new Point2D(pt.X, pt.Y));
            }
            return new Polyline2D(vertices, acadPoly.Closed);
        }
    }
}
