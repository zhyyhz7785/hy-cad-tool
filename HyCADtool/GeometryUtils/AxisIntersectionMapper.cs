using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.DatabaseServices;
using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Models.Cluster;
namespace HyCADTool.GeoUtils
{
    public class AxisIntersectionMapper
    {
        public Dictionary<ClusterResult, Tuple<Point3d, Point3d>> MapClustersToIntersections(
      List<ClusterResult> clusters,
      List<Line> xAxes,
      List<Line> yAxes)
        {
            var result = new Dictionary<ClusterResult, Tuple<Point3d, Point3d>>();

            foreach (var cluster in clusters)
            {
                var center = cluster.Center;
                var nearestX = GetNearestAxis(xAxes, center, vertical: false);
                var nearestY = GetNearestAxis(yAxes, center, vertical: true);
                var intersection = new Point3d(nearestY.StartPoint.X, nearestX.StartPoint.Y, 0);

                // 取左下角点
                var minPt = cluster.EnvelopeExtents.MinPoint;

                // 点1：交点X，左下Y；点2：左下X，交点Y
                var pt1 = new Point3d(intersection.X, minPt.Y, 0);
                var pt2 = new Point3d(minPt.X, intersection.Y, 0);

                result[cluster] = Tuple.Create(pt1, pt2);

                // 添加 MBR 内部的额外交点
                var internalPts = FindIntersectionsInside(cluster, xAxes, yAxes);
                cluster.AdditionalIntersections.AddRange(internalPts);
            }

            // 排序逻辑：按 pt1.x, pt1.y 升序
            var sorted = result.OrderBy(kv => kv.Value.Item1.X)
                               .ThenBy(kv => kv.Value.Item1.Y)
                               .ToDictionary(kv => kv.Key, kv => kv.Value);

            return sorted;
        }


        private Line GetNearestAxis(List<Line> axes, Point3d center, bool vertical)
        {
            if (axes == null || axes.Count == 0)
                throw new InvalidOperationException("找不到有效的轴线，无法确定最近交点。");
            return axes.OrderBy(line =>
                vertical ? Math.Abs(center.X - line.StartPoint.X)
                         : Math.Abs(center.Y - line.StartPoint.Y)).First();
        }
        private List<Point3d> FindIntersectionsInside(ClusterResult cluster, List<Line> xAxes, List<Line> yAxes)
        {
            List<Point3d> internalPts = new List<Point3d>();
            if (cluster.EnvelopeExtents == null)
                return internalPts;
            foreach (Line x in xAxes)
            {
                foreach (Line y in yAxes)
                {
                    Point3d pt = new Point3d(y.StartPoint.X, x.StartPoint.Y, 0);
                    if (IsInside(pt, cluster.EnvelopeExtents))
                        internalPts.Add(pt);
                }
            }
            return internalPts;
        }
        private bool IsInside(Point3d pt, Extents3d ext)
        {
            return pt.X >= ext.MinPoint.X && pt.X <= ext.MaxPoint.X &&
                   pt.Y >= ext.MinPoint.Y && pt.Y <= ext.MaxPoint.Y;
        }
    }
}
