using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using NetTopologySuite.Geometries;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Helpers
{
    /// <summary>
    /// Polyline 辅助类：AutoCAD Polyline 与 NTS Polygon 转换
    /// </summary>
    public static class PolylineHelper
    {
        /// <summary>
        /// 提示用户选择闭合多段线，并转换为 NTS Polygon
        /// </summary>
        public static Polygon PromptAndGetPolygon(Editor ed, Transaction tr)
        {
            var peo = new PromptEntityOptions("\n请选择一个闭合的多段线:");
            peo.SetRejectMessage("\n所选对象必须是闭合的多段线！\n");
            peo.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.Polyline), exactMatch: false);

            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n未选择有效的多段线，命令结束。");
                return null;
            }

            var polyline = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Polyline;
            if (polyline == null || !polyline.Closed)
            {
                ed.WriteMessage("\n所选对象不是闭合多段线！");
                return null;
            }

            return ConvertToNtsPolygon(polyline);
        }

        /// <summary>
        /// 将 AutoCAD Polyline 转换为 NTS Polygon
        /// </summary>
        public static Polygon ConvertToNtsPolygon(Autodesk.AutoCAD.DatabaseServices.Polyline polyline)
        {
            var coordinates = new List<Coordinate>();
            int vertexCount = polyline.NumberOfVertices;

            for (int i = 0; i < vertexCount; i++)
            {
                Point2d vertex = polyline.GetPoint2dAt(i);
                coordinates.Add(new Coordinate(vertex.X, vertex.Y));
            }

            // 闭合多边形
            coordinates.Add(new Coordinate(coordinates[0].X, coordinates[0].Y));

            var geometryFactory = new GeometryFactory();
            var linearRing = geometryFactory.CreateLinearRing(coordinates.ToArray());
            var polygon = geometryFactory.CreatePolygon(linearRing);

            return polygon;
        }

        /// <summary>
        /// 将 NTS Polygon 转换为 AutoCAD Polyline
        /// </summary>
        public static Autodesk.AutoCAD.DatabaseServices.Polyline ConvertToAcadPolyline(Polygon polygon)
        {
            var pl = new Autodesk.AutoCAD.DatabaseServices.Polyline();
            var coords = polygon.Coordinates;

            for (int i = 0; i < coords.Length - 1; i++) // 跳过最后一个重复点
            {
                pl.AddVertexAt(i, new Point2d(coords[i].X, coords[i].Y), 0, 0, 0);
            }

            pl.Closed = true;
            return pl;
        }

        /// <summary>
        /// 绘制 NTS Polygon 到 AutoCAD
        /// </summary>
        public static void DrawPolygon(BlockTableRecord btr, Transaction tr, Polygon polygon, string layerName)
        {
            var pl = ConvertToAcadPolyline(polygon);
            pl.Layer = layerName;
            btr.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
        }
    }
}
