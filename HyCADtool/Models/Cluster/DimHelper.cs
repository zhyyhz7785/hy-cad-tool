using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Models.Cluster;
using HyCADTool.Tools;
using HyCADTool.Models.Annotation;
using System;
using System.Collections.Generic;
using System.Linq;
using static HyCADTool.Tools.EtGpt;

namespace HyCADTool.Models.Cluster
{
    public class DimHelper
    {
        #region 静态配置属性
        public static double Scale { get; set; } = 40.0;
        public static double DistanceThreshold { get; set; } = 6000.0;

        public static ClusterConfig ClusterConfigX { get; set; } = new ClusterConfig { EpsilonX = 9000, EpsilonY = 600, MinPoints = 1 };
        public static ClusterConfig ClusterConfigY { get; set; } = new ClusterConfig { EpsilonX = 600, EpsilonY = 9000, MinPoints = 1 };

        #endregion

        #region 主执行方法
        public static List<RotatedDimension> Run(List<Point3d> inputPoints)
        {
            if (inputPoints == null || inputPoints.Count == 0)
                throw new ArgumentException("输入点集不能为空。");   

            List<Point3d> uniquePoints = inputPoints.Distinct(new Point3dComparer(0.0001)).ToList();

            List<ClusterResult> clusterX = ClusterFactory.Create(uniquePoints, ClusterConfigX);
            List<ClusterResult> clusterY = ClusterFactory.Create(uniquePoints, ClusterConfigY);

            return GenerateDimensions(clusterX, clusterY);
        }
        #endregion

        #region 标注生成
        private static List<RotatedDimension> GenerateDimensions(List<ClusterResult> clusterX, List<ClusterResult> clusterY)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;

            var layerIdX = EtGpt.CreateLayer("00_hy_3公共_标注2_内x", 93, db);
            var layerIdY = EtGpt.CreateLayer("00_hy_3公共_标注2_内y", 45, db);

            var options = new ClusterDimOptions
            {
                Scale = Scale,
                DistanceThreshold = DistanceThreshold,
                XDirectionIsUp = false,
                YDirectionIsRight = false
            };

            var dims = new List<RotatedDimension>();
            foreach (var cluster in clusterX)
                dims.AddRange(EtGpt.CreateDimensionsForClusterSingleSide(cluster.Points, true, layerIdX, options));

            foreach (var cluster in clusterY)
                dims.AddRange(EtGpt.CreateDimensionsForClusterSingleSide(cluster.Points, false, layerIdY, options));

            return FilterDuplicateDimensions(dims, DistanceThreshold);
        }
        #endregion

        #region 重复标注过滤
        private static List<RotatedDimension> FilterDuplicateDimensions(List<RotatedDimension> dims, double threshold)
        {
            const double tol = 0.1;
            var xGroups = dims
                .Where(d => Math.Abs(d.Rotation) < 1e-6)
                .GroupBy(d => new { X1 = Math.Round(d.XLine1Point.X / tol), X2 = Math.Round(d.XLine2Point.X / tol) });

            var yGroups = dims
                .Where(d => Math.Abs(Math.Abs(d.Rotation) - Math.PI / 2) < 1e-6)
                .GroupBy(d => new { Y1 = Math.Round(d.XLine1Point.Y / tol), Y2 = Math.Round(d.XLine2Point.Y / tol) });

            return FilterGroup(xGroups, d => d.DimLinePoint.Y, threshold)
                .Concat(FilterGroup(yGroups, d => d.DimLinePoint.X, threshold))
                .ToList();
        }

        private static List<RotatedDimension> FilterGroup(
            IEnumerable<IGrouping<object, RotatedDimension>> groups,
            Func<RotatedDimension, double> keySelector,
            double threshold)
        {
            var result = new List<RotatedDimension>();
            foreach (var group in groups)
            {
                var list = group.OrderBy(keySelector).ToList();
                int current = 0;
                while (current < list.Count)
                {
                    var sameLine = new List<RotatedDimension> { list[current] };
                    int next = current + 1;
                    while (next < list.Count &&
                           Math.Abs(keySelector(list[next]) - keySelector(list[current])) <= threshold)
                    {
                        sameLine.Add(list[next]);
                        next++;
                    }
                    result.Add(sameLine.First());
                    current = next;
                }
            }
            return result;
        }
        #endregion
    }

}
