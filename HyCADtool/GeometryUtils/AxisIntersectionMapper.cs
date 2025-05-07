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
        /// <summary>
        /// 将每个 ClusterResult 映射到最近的轴线交点
        /// </summary>
        public Dictionary<ClusterResult, Point3d> MapClustersToIntersections(
            List<ClusterResult> clusters,
            List<Line> xAxes,
            List<Line> yAxes)
        {
            var result = new Dictionary<ClusterResult, Point3d>();

            foreach (var cluster in clusters)
            {
                var center = cluster.Center;

                var nearestX = GetNearestAxis(xAxes, center, vertical: false);
                var nearestY = GetNearestAxis(yAxes, center, vertical: true);
                var intersection = new Point3d(nearestY.StartPoint.X, nearestX.StartPoint.Y, 0);
                result[cluster] = intersection;

                // 添加 MBR 内部的额外交点
                var internalPts = FindIntersectionsInside(cluster, xAxes, yAxes);
                cluster.AdditionalIntersections.AddRange(internalPts);
            }

            return result;
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
