using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Config;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using static HyCADTool.Tools.ZTools;

namespace HyCADTool.Models.Cluster
{


    public class DimHelper
    {
        public static double Scale { get; set; } = BaseConfig.Scale;
        public static double DistanceThreshold { get; set; } = 6000.0;
        public static ClusterConfig ClusterConfigX { get; set; } = new ClusterConfig { EpsilonX = 9000, EpsilonY = 300, MinPoints = 1 };
        public static ClusterConfig ClusterConfigY { get; set; } = new ClusterConfig { EpsilonX = 300, EpsilonY = 9000, MinPoints = 1 };

        public List<RotatedDimension> DimXs { get; private set; } = new List<RotatedDimension>();
        public List<RotatedDimension> DimYs { get; private set; } = new List<RotatedDimension>();
        public List<ClusterResult> Clusters { get; private set; } = new List<ClusterResult>();
        public List<RotatedDimension> AllDimensions => DimXs.Concat(DimYs).ToList();

        public static DimHelper Build(List<Point3d> inputPoints, ClusterConfig cfgX = null, ClusterConfig cfgY = null)
        {
            if (inputPoints == null || inputPoints.Count == 0)
                throw new ArgumentException("输入点集不能为空。", nameof(inputPoints));

            var helper = new DimHelper();
            cfgX = cfgX ?? ClusterConfigX;
            cfgY = cfgY ?? ClusterConfigY;

            List<Point3d> unique = inputPoints.Distinct(new Point3dComparer(0.0001)).ToList();

            var db = Application.DocumentManager.MdiActiveDocument.Database;
            var idX = CreateLayer("00_hy_3公共_标注2_内x", 93, db);
            var idY = CreateLayer("00_hy_3公共_标注2_内y", 45, db);

            var opt = new ClusterDimOptions
            {
                Scale = Scale,
                DistanceThreshold = DistanceThreshold,
                XDirectionIsUp = false,
                YDirectionIsRight = false
            };

            var clustersX = ClusterFactory.Create(unique, cfgX);
            var clustersY = ClusterFactory.Create(unique, cfgY);

            helper.Clusters.AddRange(clustersX);
            helper.Clusters.AddRange(clustersY);

            foreach (var c in clustersX)
                helper.DimXs.AddRange(CreateDimensionsForClusterSingleSide(c.AllPoints.ToList(), true, idX, opt));

            foreach (var c in clustersY)
                helper.DimYs.AddRange(CreateDimensionsForClusterSingleSide(c.Points, false, idY, opt));

            var filtered = FilterDuplicateDimensions(helper.DimXs.Concat(helper.DimYs).ToList(), DistanceThreshold);

            helper.DimXs = filtered.Where(d => Math.Abs(d.Rotation) < 1e-6).ToList();
            helper.DimYs = filtered.Where(d => Math.Abs(Math.Abs(d.Rotation) - Math.PI / 2) < 1e-6).ToList();

            return helper;
        }

        public static DimHelper BuildByRegion(Dictionary<Extents3d, RegionPointInfo> regionPointsMap, ClusterConfig cfgX = null, ClusterConfig cfgY = null)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            var idX = CreateLayer("00_hy_3公共_标注2_内x", 93, db);
            var idY = CreateLayer("00_hy_3公共_标注2_内y", 45, db);

            var opt = new ClusterDimOptions
            {
                Scale = Scale,
                DistanceThreshold = DistanceThreshold,
                XDirectionIsUp = false,
                YDirectionIsRight = false
            };

            var result = new DimHelper();
            var usedX = cfgX ?? ClusterConfigX;
            var usedY = cfgY ?? ClusterConfigY;

            foreach (var kvp in regionPointsMap)
            {
                var info = kvp.Value;
                var regionPoints = info.Points.Distinct(new Point3dComparer(0.0001)).ToList();

                var cx = ClusterFactory.Create(regionPoints, usedX);
                var cy = ClusterFactory.Create(regionPoints, usedY);

                foreach (var cluster in cx)
                {
                    var leftBottom = cluster.Points.OrderBy(p => p.X).ThenBy(p => p.Y).First();

                    if (info.XAxis != null)
                    {
                        var xCross = new Point3d( info.YAxis.Position, leftBottom.Y, 0);
                        if (!cluster.AuxiliaryPoints.Contains(xCross, new Point3dComparer(0.001)))
                            cluster.AuxiliaryPoints.Add(xCross);
                    }                 

                }
                foreach (var cluster in cy)
                {


                    var leftBottom = cluster.Points.OrderBy(p => p.X).ThenBy(p => p.Y).First();

                   

                    if (info.YAxis != null)
                    {
                        var yCross = new Point3d(leftBottom.X, info.XAxis.Position,  0);
                        if (!cluster.AuxiliaryPoints.Contains(yCross, new Point3dComparer(0.001)))
                            cluster.AuxiliaryPoints.Add(yCross);
                    }

                }

                result.Clusters.AddRange(cx);
                result.Clusters.AddRange(cy);

                foreach (var c in cx)
                    result.DimXs.AddRange(CreateDimensionsForClusterSingleSide(c.AllPoints.ToList(), true, idX, opt));

                foreach (var c in cy)
                    result.DimYs.AddRange(CreateDimensionsForClusterSingleSide(c.AllPoints.ToList(), false, idY, opt));
            }

            var filtered = FilterDuplicateDimensions(result.DimXs.Concat(result.DimYs).ToList(), DistanceThreshold);

            result.DimXs = filtered.Where(d => Math.Abs(d.Rotation) < 1e-6).ToList();
            result.DimYs = filtered.Where(d => Math.Abs(Math.Abs(d.Rotation) - Math.PI / 2) < 1e-6).ToList();

            return result;
        }

        private static List<RotatedDimension> FilterDuplicateDimensions(List<RotatedDimension> dims, double threshold)
        {
            const double tol = 0.1;

            var xGroups = dims
                .Where(d => Math.Abs(d.Rotation) < 1e-6)
                .GroupBy(d => new
                {
                    X1 = Math.Round(d.XLine1Point.X / tol),
                    X2 = Math.Round(d.XLine2Point.X / tol)
                });

            var yGroups = dims
                .Where(d => Math.Abs(Math.Abs(d.Rotation) - Math.PI / 2) < 1e-6)
                .GroupBy(d => new
                {
                    Y1 = Math.Round(d.XLine1Point.Y / tol),
                    Y2 = Math.Round(d.XLine2Point.Y / tol)
                });

            var result = new List<RotatedDimension>();
            result.AddRange(FilterGroup(xGroups, d => d.DimLinePoint.Y, threshold));
            result.AddRange(FilterGroup(yGroups, d => d.DimLinePoint.X, threshold));
            return result;
        }

        private static List<RotatedDimension> FilterGroup(
            IEnumerable<IGrouping<object, RotatedDimension>> groups,
            Func<RotatedDimension, double> keySelector,
            double threshold)
        {
            var ret = new List<RotatedDimension>();
            foreach (var g in groups)
            {
                var list = g.OrderBy(keySelector).ToList();
                int cur = 0;
                while (cur < list.Count)
                {
                    var sameLine = new List<RotatedDimension> { list[cur] };
                    int nxt = cur + 1;
                    while (nxt < list.Count && Math.Abs(keySelector(list[nxt]) - keySelector(list[cur])) <= threshold)
                    {
                        sameLine.Add(list[nxt]);
                        nxt++;
                    }
                    ret.Add(sameLine.First());
                    cur = nxt;
                }
            }
            return ret;
        }
    }
}
