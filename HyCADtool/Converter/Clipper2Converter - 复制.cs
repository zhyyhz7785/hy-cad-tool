using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Clipper2Lib;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
namespace HyCADTool.HelpClass
{
    public static class Clipper2Converter
    {
        #region Clipper2Lib <-> NetTopologySuite
        public static PathD ToClipper2PathD(this Geometry geometry)
        {
            if (geometry is Polygon polygon)
            {
                return polygon.ToClipper2PathD();
            }
            else if (geometry is MultiPolygon multiPolygon && multiPolygon.NumGeometries > 0)
            {
                return ((Polygon)multiPolygon.GetGeometryN(0)).ToClipper2PathD();
            }
            throw new ArgumentException("Geometry 必须是 Polygon 或非空的 MultiPolygon 类型", nameof(geometry));
        }
        // 将 NTS Polygon 转换为 Clipper2Lib Path64
        public static Path64 ToClipper2Path(this Polygon polygon)
        {
            Path64 path = new Path64();
            // 注意：Clipper2Lib 不需要闭合的点 (跳过最后一个点)
            for (int i = 0; i < polygon.Shell.Coordinates.Length - 1; i++)
            {
                var point = polygon.Shell.Coordinates[i];
                // Clipper2 使用整数坐标，需要缩放
                path.Add(new Point64(
                    (long)(point.X * 1000000),
                    (long)(point.Y * 1000000)));
            }
            return path;
        }
        // 将 NTS Polygon 转换为 Clipper2Lib PathD
        public static PathD ToClipper2PathD(this Polygon polygon)
        {
            PathD path = new PathD();
            // 注意：Clipper2Lib 不需要闭合的点 (跳过最后一个点)
            for (int i = 0; i < polygon.Shell.Coordinates.Length - 1; i++)
            {
                var point = polygon.Shell.Coordinates[i];
                path.Add(new PointD(point.X, point.Y));
            }
            return path;
        }
        // 将 NTS MultiPolygon 转换为 Clipper2Lib Paths64
        public static Paths64 ToClipper2Paths(this MultiPolygon multiPolygon)
        {
            Paths64 paths = new Paths64();
            foreach (var polygon in multiPolygon.Geometries)
            {
                paths.Add(((Polygon)polygon).ToClipper2Path());
            }
            return paths;
        }
        // 将 NTS MultiPolygon 转换为 Clipper2Lib PathsD
        public static PathsD ToClipper2PathsD(this MultiPolygon multiPolygon)
        {
            PathsD paths = new PathsD();
            foreach (var polygon in multiPolygon.Geometries)
            {
                paths.Add(((Polygon)polygon).ToClipper2PathD());
            }
            return paths;
        }
        // 将 Clipper2Lib Path64 转换为 NTS Polygon
        public static Polygon ToNetTopologySuite(this Path64 path)
        {
            var geometryFactory = new GeometryFactory();
            List<Coordinate> coordinates = new List<Coordinate>();
            foreach (var point in path)
            {
                // 反向缩放回原始坐标系
                coordinates.Add(new Coordinate(
                    point.X / 1000000.0,
                    point.Y / 1000000.0));
            }
            // 添加闭合点
            if (coordinates.Count > 0)
            {
                coordinates.Add(new Coordinate(coordinates[0].X, coordinates[0].Y));
            }
            return geometryFactory.CreatePolygon(coordinates.ToArray());
        }
        // 将 Clipper2Lib PathD 转换为 NTS Polygon
        public static Polygon ToNetTopologySuite(this PathD path)
        {
            var geometryFactory = new GeometryFactory();
            List<Coordinate> coordinates = new List<Coordinate>();
            foreach (var point in path)
            {
                coordinates.Add(new Coordinate(point.x, point.y));
            }
            // 添加闭合点
            if (coordinates.Count > 0)
            {
                coordinates.Add(new Coordinate(coordinates[0].X, coordinates[0].Y));
            }
            return geometryFactory.CreatePolygon(coordinates.ToArray());
        }
        // 将 Clipper2Lib Paths64 转换为 NTS MultiPolygon
        public static MultiPolygon ToNetTopologySuite(this Paths64 paths)
        {
            var geometryFactory = new GeometryFactory();
            List<Polygon> polygons = new List<Polygon>();
            foreach (var path in paths)
            {
                polygons.Add(path.ToNetTopologySuite());
            }
            return geometryFactory.CreateMultiPolygon(polygons.ToArray());
        }
        // 将 Clipper2Lib PathsD 转换为 NTS MultiPolygon
        public static MultiPolygon ToNetTopologySuite(this PathsD paths)
        {
            var geometryFactory = new GeometryFactory();
            List<Polygon> polygons = new List<Polygon>();
            foreach (var path in paths)
            {
                polygons.Add(path.ToNetTopologySuite());
            }
            return geometryFactory.CreateMultiPolygon(polygons.ToArray());
        }
        // 将 NTS Point 转换为 Clipper2Lib Point64
        public static Point64 ToClipper2Point64(this NetTopologySuite.Geometries.Point point)
        {
            return new Point64(
                (long)(point.X * 1000000),
                (long)(point.Y * 1000000));
        }
        // 将 NTS Point 转换为 Clipper2Lib PointD
        public static PointD ToClipper2PointD(this NetTopologySuite.Geometries.Point point)
        {
            return new PointD(point.X, point.Y);
        }
        // 将 Clipper2Lib Point64 转换为 NTS Point
        public static NetTopologySuite.Geometries.Point ToNetTopologySuite(this Point64 point)
        {
            var geometryFactory = new GeometryFactory();
            return geometryFactory.CreatePoint(new Coordinate(
                point.X / 1000000.0,
                point.Y / 1000000.0));
        }
        // 将 Clipper2Lib PointD 转换为 NTS Point
        public static NetTopologySuite.Geometries.Point ToNetTopologySuite(this PointD point)
        {
            var geometryFactory = new GeometryFactory();
            return geometryFactory.CreatePoint(new Coordinate(point.x, point.y));
        }
        #endregion
        #region Clipper2Lib <-> AutoCAD
        // 将 AutoCAD Polyline 转换为 Clipper2Lib Path64
        public static Path64 ToClipper2Path(this Polyline polyline)
        {
            Path64 path = new Path64();
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            if (polyline.Closed || IsFirstLastPointsEqual(polyline))
            {
                // 转换所有点（对于闭合的多段线，跳过最后一个点以避免重复）
                int vertexCount = polyline.NumberOfVertices;
                int pointsToProcess = polyline.Closed ? vertexCount : vertexCount - 1;
                for (int i = 0; i < pointsToProcess; i++)
                {
                    Point2d vertex = polyline.GetPoint2dAt(i);
                    path.Add(new Point64(
                        (long)(vertex.X * 1000000),
                        (long)(vertex.Y * 1000000)));
                }
                return path;
            }
            else
            {
                ed.WriteMessage("\n警告：Polyline 不闭合，无法转换为 Clipper2 Path.");
                return null;
            }
        }
        // 将 AutoCAD Polyline 转换为 Clipper2Lib PathD
        public static PathD ToClipper2PathD(this Polyline polyline)
        {
            PathD path = new PathD();
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            if (polyline.Closed || IsFirstLastPointsEqual(polyline))
            {
                // 转换所有点（对于闭合的多段线，跳过最后一个点以避免重复）
                int vertexCount = polyline.NumberOfVertices;
                int pointsToProcess = polyline.Closed ? vertexCount : vertexCount - 1;
                for (int i = 0; i < pointsToProcess; i++)
                {
                    Point2d vertex = polyline.GetPoint2dAt(i);
                    path.Add(new PointD(vertex.X, vertex.Y));
                }
                return path;
            }
            else
            {
                ed.WriteMessage("\n警告：Polyline 不闭合，无法转换为 Clipper2 PathD.");
                return null;
            }
        }
        // 将 Clipper2Lib Path64 转换为 AutoCAD Polyline
        public static Polyline ToAutoCadPolyline(this Path64 path)
        {
            Polyline polyline = new Polyline();
            for (int i = 0; i < path.Count; i++)
            {
                var point = path[i];
                polyline.AddVertexAt(i, new Point2d(
                    point.X / 1000000.0,
                    point.Y / 1000000.0), 0, 0, 0);
            }
            polyline.Closed = true;
            return polyline;
        }
        // 将 Clipper2Lib PathD 转换为 AutoCAD Polyline
        public static Polyline ToAutoCadPolyline(this PathD path)
        {
            Polyline polyline = new Polyline();
            for (int i = 0; i < path.Count; i++)
            {
                var point = path[i];
                polyline.AddVertexAt(i, new Point2d(point.x, point.y), 0, 0, 0);
            }
            polyline.Closed = true;
            return polyline;
        }
        // 将 Clipper2Lib Paths64 转换为 AutoCAD Polyline 列表
        public static List<Polyline> ToAutoCadPolylines(this Paths64 paths)
        {
            List<Polyline> polylines = new List<Polyline>();
            foreach (var path in paths)
            {
                polylines.Add(path.ToAutoCadPolyline());
            }
            return polylines;
        }
        // 将 Clipper2Lib PathsD 转换为 AutoCAD Polyline 列表
        public static List<Polyline> ToAutoCadPolylines(this PathsD paths)
        {
            List<Polyline> polylines = new List<Polyline>();
            foreach (var path in paths)
            {
                polylines.Add(path.ToAutoCadPolyline());
            }
            return polylines;
        }
        // 将 AutoCAD Point3d 转换为 Clipper2Lib Point64
        public static Point64 ToClipper2Point64(this Point3d point)
        {
            return new Point64(
                (long)(point.X * 1000000),
                (long)(point.Y * 1000000));
        }
        // 将 AutoCAD Point3d 转换为 Clipper2Lib PointD
        public static PointD ToClipper2PointD(this Point3d point)
        {
            return new PointD(point.X, point.Y);
        }
        // 将 Clipper2Lib Point64 转换为 AutoCAD Point3d
        public static Point3d ToAutoCadPoint(this Point64 point)
        {
            return new Point3d(
                point.X / 1000000.0,
                point.Y / 1000000.0,
                0);
        }
        // 将 Clipper2Lib PointD 转换为 AutoCAD Point3d
        public static Point3d ToAutoCadPoint(this PointD point)
        {
            return new Point3d(point.x, point.y, 0);
        }
        // 将 AutoCAD Polyline 列表转换为 Clipper2Lib Paths64
        public static Paths64 ToClipper2Paths(this IEnumerable<Polyline> polylines)
        {
            Paths64 paths = new Paths64();
            foreach (var polyline in polylines)
            {
                var path = polyline.ToClipper2Path();
                if (path != null)
                {
                    paths.Add(path);
                }
            }
            return paths;
        }
        // 将 AutoCAD Polyline 列表转换为 Clipper2Lib PathsD
        public static PathsD ToClipper2PathsD(this IEnumerable<Polyline> polylines)
        {
            PathsD paths = new PathsD();
            foreach (var polyline in polylines)
            {
                var path = polyline.ToClipper2PathD();
                if (path != null)
                {
                    paths.Add(path);
                }
            }
            return paths;
        }
        #endregion
        #region Helper Methods
        // 检查第一个和最后一个点是否相同（考虑误差）
        private static bool IsFirstLastPointsEqual(Polyline polyline)
        {
            if (polyline.NumberOfVertices < 2)
                return false;
            Point2d first = polyline.GetPoint2dAt(0);
            Point2d last = polyline.GetPoint2dAt(polyline.NumberOfVertices - 1);
            return Math.Abs(first.X - last.X) < 1e-3 && Math.Abs(first.Y - last.Y) < 1e-3;
        }
        #endregion
    }
}