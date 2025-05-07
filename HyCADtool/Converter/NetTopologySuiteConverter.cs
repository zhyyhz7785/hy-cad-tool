using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.HelpClass
{
    public static class GeometryConverter
    {
        // 将 AutoCAD Polyline 转换为 NTS 的 Polygon
        public static Polygon ToNetTopologySuite(this Polyline polyline)
        {
            // 获取当前文档的数据库和编辑器
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            // 存储坐标的列表
            List<Coordinate> coordinates = new List<Coordinate>();
            var comparer = new Point2dEqualityComparer(1e-3);
            if (polyline.Closed || comparer.Equals(polyline.GetPoint2dAt(0), polyline.GetPoint2dAt(polyline.NumberOfVertices - 1)))
            {
                // 遍历 Polyline 的顶点，并添加到 NTS 坐标列表中
                int vertexCount = polyline.NumberOfVertices;
                for (int i = 0; i < vertexCount; i++)
                {
                    Point2d vertex = polyline.GetPoint2dAt(i);
                    coordinates.Add(new Coordinate(vertex.X, vertex.Y));
                }
                if (polyline.Closed)
                {
                    coordinates.Add(new Coordinate(coordinates[0].X, coordinates[0].Y)); // 闭合多边形
                }
                var geometryFactory = new GeometryFactory();
                //var linearRing = geometryFactory.CreateLinearRing(coordinates.ToArray());
                var polygon = geometryFactory.CreatePolygon(coordinates.ToArray());
                return polygon; // 返回 Polygon 类型
            }
            else
            {
                ed.WriteMessage("\n警告：Polyline 不闭合，但第一个和最后一个端点相同，自动闭合.");
                return null;
            }
        }
        public static Polygon ToRectNetTopologySuite(this Polyline polyline)
        {
            // 获取当前文档的数据库和编辑器
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            // 存储坐标的列表
            List<Coordinate> coordinates = new List<Coordinate>();
            var comparer = new Point2dEqualityComparer(1e-3);
            var a = polyline.Closed && polyline.NumberOfVertices == 4;
            var b = comparer.Equals(polyline.GetPoint2dAt(0), polyline.GetPoint2dAt(polyline.NumberOfVertices - 1)) && polyline.NumberOfVertices == 5;
            if (a || b)
            {
                // 遍历 Polyline 的顶点，并添加到 NTS 坐标列表中
                int vertexCount = polyline.NumberOfVertices;
                for (int i = 0; i < vertexCount; i++)
                {
                    Point2d vertex = polyline.GetPoint2dAt(i);
                    coordinates.Add(new Coordinate(vertex.X, vertex.Y));
                }
                // 确保多边形闭合
                if (polyline.Closed && polyline.NumberOfVertices <= 4)
                {
                    coordinates.Add(new Coordinate(coordinates[0].X, coordinates[0].Y));
                }
                // 找到左下角的点
                var leftBottom = coordinates.OrderBy(c => c.Y).ThenBy(c => c.X).First();
                // 找到这个点在原始坐标列表中的索引
                int startIndex = coordinates.IndexOf(leftBottom);
                // 重新排列顶点，使左下角为起点
                var reorderedCoordinates = new List<Coordinate>();
                for (int i = startIndex; i < coordinates.Count; i++)
                {
                    reorderedCoordinates.Add(coordinates[i]);
                }
                for (int i = 1; i < startIndex; i++)
                {
                    reorderedCoordinates.Add(coordinates[i]);
                }
                //避免节点顺序已经排序好后，重复添加
                if (reorderedCoordinates.Count == 4)
                {
                    reorderedCoordinates.Add(coordinates[startIndex]);
                }
                // 检查是否需要逆时针排序
                bool isClockwise = IsClockwise(reorderedCoordinates);
                if (isClockwise)
                {
                    reorderedCoordinates.Reverse();
                }
                var geometryFactory = new GeometryFactory();
                var linearRing = geometryFactory.CreateLinearRing(reorderedCoordinates.ToArray());
                var polygon = geometryFactory.CreatePolygon(linearRing);
                if (RectangleChecker.IsRectangle(polygon))
                {
                    return polygon; // 返回 Polygon 类型
                }
                else
                {
                    ed.WriteMessage("\n警告：Polyline 不是长方形请重新选择.");
                    return null;
                }
            }
            else
            {
                ed.WriteMessage("\n警告：Polyline 不是长方形请重新选择.");
                return null;
            }
        }
        private static bool IsClockwise(List<Coordinate> coords)
        {
            double sum = 0;
            for (int i = 0; i < coords.Count - 1; i++)
            {
                sum += (coords[i + 1].X - coords[i].X) * (coords[i + 1].Y + coords[i].Y);
            }
            return sum > 0;
        }
        // 将 NTS 的 Polygon 转换为 AutoCAD Polyline
        public static Polyline ToAutoCadPolyline(this Polygon polygon)
        {
            Polyline polyline = new Polyline();
            // 访问外环的坐标，跳过最后一个重复的点
            for (int i = 0; i < polygon.Shell.Coordinates.Length - 1; i++)  // 注意这里减去最后一个点
            {
                var point = polygon.Shell.Coordinates[i];
                polyline.AddVertexAt(i, new Point2d(point.X, point.Y), 0, 0, 0);
            }
            polyline.Closed = true; // 确保是闭合的
            return polyline;
        }
        // 将 AutoCAD Point3d 转换为 NTS Point
        public static Point ToNetTopologySuite(this Point3d point)
        {
            return new Point(point.X, point.Y);
        }
        // 将 NTS Point 转换为 AutoCAD Point3d
        public static Point3d ToAutoCadPoint(this Point point)
        {
            return new Point3d(point.X, point.Y, 0);
        }
        public static List<Point3d> ToAutoCadPoints(this IEnumerable<Point> points)
        {
            List<Point3d> ps = new List<Point3d>();
            foreach (var point in points)
            {
                ps.Add(point.ToAutoCadPoint()); // 调用 ToNetTopologySuite 转换
            }
            return ps;
        }
        // 将 AutoCAD Polyline 集合转换为 NTS Polygon 集合
        public static List<Polygon> ToNetTopologySuite(this IEnumerable<Polyline> polylines)
        {
            List<Polygon> polygons = new List<Polygon>();
            foreach (var polyline in polylines)
            {
                polygons.Add(polyline.ToNetTopologySuite()); // 调用 ToNetTopologySuite 转换
            }
            return polygons;
        }
        // 将 NTS Polygon 集合转换为 AutoCAD Polyline 集合
        public static List<Polyline> ToAutoCadPolyline(this IEnumerable<Polygon> polygons)
        {
            List<Polyline> polylines = new List<Polyline>();
            foreach (var polygon in polygons)
            {
                polylines.Add(polygon.ToAutoCadPolyline()); // 调用 ToAutoCadPolyline 转换
            }
            return polylines;
        }
    }
    public class RectangleChecker
    {
        public static bool IsRectangle(Polygon polygon)
        {
            // 1. 确保多边形有 4 个顶点
            if (polygon.NumPoints != 5)  // 包含最后一个闭合的点
            {
                return false;
            }
            // 2. 获取多边形的外部边
            var ring = (LinearRing)polygon.ExteriorRing;
            // 3. 获取边的长度
            double edge1 = ring.Coordinates[0].Distance(ring.Coordinates[1]);
            double edge2 = ring.Coordinates[1].Distance(ring.Coordinates[2]);
            double edge3 = ring.Coordinates[2].Distance(ring.Coordinates[3]);
            double edge4 = ring.Coordinates[3].Distance(ring.Coordinates[0]);
            // 4. 确保对边相等
            if (edge1 != edge3 || edge2 != edge4)
            {
                return false;
            }
            // 5. 确保每个角度都是 90 度
            return AreAnglesRightAngles(ring);
        }
        private static bool AreAnglesRightAngles(LinearRing ring)
        {
            for (int i = 0; i < 4; i++)
            {
                // 获取三个连续的点 (p1, p2, p3)
                var p1 = ring.Coordinates[i];
                var p2 = ring.Coordinates[(i + 1) % 4]; // 循环获取顶点
                var p3 = ring.Coordinates[(i + 2) % 4];
                // 计算向量 p1p2 和 p2p3 的夹角
                double angle = GetAngleBetweenVectors(p1, p2, p3);
                // 检查夹角是否接近 90 度
                if (Math.Abs(angle - 90) > 0.1)  // 允许有微小误差
                {
                    return false;
                }
            }
            return true;
        }
        // 计算两向量之间的夹角
        private static double GetAngleBetweenVectors(Coordinate p1, Coordinate p2, Coordinate p3)
        {
            var v1 = new Coordinate(p1.X - p2.X, p1.Y - p2.Y);
            var v2 = new Coordinate(p3.X - p2.X, p3.Y - p2.Y);
            double dotProduct = v1.X * v2.X + v1.Y * v2.Y;
            double magnitudeV1 = Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
            double magnitudeV2 = Math.Sqrt(v2.X * v2.X + v2.Y * v2.Y);
            double cosineAngle = dotProduct / (magnitudeV1 * magnitudeV2);
            return Math.Acos(cosineAngle) * (180 / Math.PI);  // 转换为角度
        }
    }
}
