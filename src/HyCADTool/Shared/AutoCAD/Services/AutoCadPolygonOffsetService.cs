using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Shared.Geometry.Interfaces;
using HyCADTool.Shared.Geometry;
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
                return polyline;

            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // 创建临时 AutoCAD Polyline
                    var acadPoly = ToAcadPolyline(polyline);

                    // 执行偏移
                    var offsetCurves = acadPoly.GetOffsetCurves(offsetDistance);
                    if (offsetCurves.Count == 0)
                        return polyline;

                    var offsetPoly = offsetCurves[0] as Polyline;
                    if (offsetPoly == null)
                        return polyline;

                    // 转回 Polyline2D
                    var result = FromAcadPolyline(offsetPoly);

                    // 清理临时对象
                    offsetPoly.Dispose();
                    acadPoly.Dispose();

                    tr.Commit();
                    return result;
                }
                catch (System.Exception)
                {
                    return polyline;
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
