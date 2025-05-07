using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;
namespace PolygonGrouping
{
    public class PolygonGroup
    {
        public static List<List<Polyline>> GroupPolygonsByContainment(List<Polyline> polygonsA, List<Polyline> polygonsB)
        {
            // 结果集合
            List<List<Polyline>> groupedPolygons = new List<List<Polyline>>();
            foreach (var polyA in polygonsA)
            {
                List<Polyline> containedPolygons = new List<Polyline>();
                foreach (var polyB in polygonsB)
                {
                    if (IsPolygonInside(polyA, polyB))
                    {
                        containedPolygons.Add(polyB);
                    }
                }
                // 将当前多边形及其包含的多边形加入分组结果
                if (containedPolygons.Count > 0)
                {
                    containedPolygons.Insert(0, polyA); // 把 polyA 放在包含组的第一个位置
                    groupedPolygons.Add(containedPolygons);
                }
            }
            return groupedPolygons;
        }
        // 判断 polyB 是否完全位于 polyA 内部
        public static bool IsPolygonInside(Polyline polyA, Polyline polyB)
        {
            // polyA 和 polyB 都是四边形，检查 polyB 的四个顶点是否都在 polyA 内部
            for (int i = 0; i < polyB.NumberOfVertices; i++)
            {
                Point3d pointB = polyB.GetPoint3dAt(i);
                if (!IsPointInsidePolygon(pointB, polyA))
                {
                    return false; // 只要有一个点不在内部，则 polyB 不在 polyA 内
                }
            }
            return true;
        }
        // 判断点是否在多边形内部
        public static bool IsPointInsidePolygon(Point3d point, Polyline polygon)
        {
            // 使用 Autodesk.AutoCAD.Geometry 的方法进行点在多边形内部的判断
            return polygon.IsInsidePolygon(point);
        }
    }
    // 扩展方法，用于判断点是否在多边形内部
    public static class PolylineExtensions
    {
        public static bool IsInsidePolygon(this Polyline polyline, Point3d point)
        {
            Point3dCollection polygonPoints = new Point3dCollection();
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                polygonPoints.Add(polyline.GetPoint3dAt(i));
            }
            return IsPointInPolygon(polygonPoints, point);
        }
        private static bool IsPointInPolygon(Point3dCollection polygonPoints, Point3d point)
        {
            bool isInside = false;
            int n = polygonPoints.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Point3d pi = polygonPoints[i];
                Point3d pj = polygonPoints[j];
                if (((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                     (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X))
                {
                    isInside = !isInside;
                }
            }
            return isInside;
        }
    }
}
