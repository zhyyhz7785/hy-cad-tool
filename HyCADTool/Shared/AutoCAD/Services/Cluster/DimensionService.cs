using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Models.Cluster;
using HyCADTool.Domain.Enums;
using HyCADTool.Domain.Models.Configuration;
using HyCADTool.Domain.ValueObjects.Configuration.User;
using HyCADTool.Shared.Geometry;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Shared.AutoCAD.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Shared.AutoCAD.Services.Cluster
{
    /// <summary>
    /// 标注生成服务
    /// 替代旧 DimHelper + ZTools.CreateDimensionsForClusterSingleSide + GetDimByTwoPoints
    /// </summary>
    public class DimensionService
    {
        /// <summary>
        /// 按区域生成聚类标注
        /// </summary>
        public DimensionResult BuildDimensionsByRegion(
            Dictionary<Extents3d, RegionPointInfo> regionPointsMap,
            ClusterConfig cfgX, ClusterConfig cfgY,
            ClusterDimOptions options,
            ClusterFactoryService clusterFactory,
            string layerNameX = null,
            short layerColorX = 93,
            string layerNameY = null,
            short layerColorY = 45)
        {
            layerNameX = string.IsNullOrWhiteSpace(layerNameX)
                ? UserLayerNameResolver.Get(LayerSemanticIds.CommonDimInsideHorizontal, LayerBuiltinDefaults.CommonDimInsideHorizontal)
                : layerNameX;
            layerNameY = string.IsNullOrWhiteSpace(layerNameY)
                ? UserLayerNameResolver.Get(LayerSemanticIds.CommonDimInsideVertical, LayerBuiltinDefaults.CommonDimInsideVertical)
                : layerNameY;

            var result = new DimensionResult();
            var comparer = new Point2DComparer(0.001);

            foreach (var kvp in regionPointsMap)
            {
                var info = kvp.Value;
                var regionPoints = info.Points.Distinct(new Point3dComparer(0.0001)).ToList();

                var cx = clusterFactory.CreateClusters(regionPoints, cfgX);
                var cy = clusterFactory.CreateClusters(regionPoints, cfgY);

                // 为 X 向聚类添加辅助轴交点
                foreach (var cluster in cx)
                {
                    if (info.YAxis != null)
                    {
                        var leftBottom = cluster.Points.OrderBy(p => p.X).ThenBy(p => p.Y).First();
                        var xCross = new Point2D(info.YAxis.Position, leftBottom.Y);
                        if (!cluster.AuxiliaryPoints.Contains(xCross, comparer))
                            cluster.AuxiliaryPoints.Add(xCross);
                    }
                }

                // 为 Y 向聚类添加辅助轴交点
                foreach (var cluster in cy)
                {
                    if (info.XAxis != null)
                    {
                        var leftBottom = cluster.Points.OrderBy(p => p.X).ThenBy(p => p.Y).First();
                        var yCross = new Point2D(leftBottom.X, info.XAxis.Position);
                        if (!cluster.AuxiliaryPoints.Contains(yCross, comparer))
                            cluster.AuxiliaryPoints.Add(yCross);
                    }
                }

                result.Clusters.AddRange(cx);
                result.Clusters.AddRange(cy);

                // 生成标注（将 Domain Point2D 转换为 AutoCAD Point3d）
                foreach (var c in cx)
                    result.DimXs.AddRange(CreateDimensionsForClusterSingleSide(
                        c.AllPoints.Select(p => new Point3d(p.X, p.Y, 0)).ToList(), true, layerNameX, options));

                foreach (var c in cy)
                    result.DimYs.AddRange(CreateDimensionsForClusterSingleSide(
                        c.AllPoints.Select(p => new Point3d(p.X, p.Y, 0)).ToList(), false, layerNameY, options));
            }

            // 去重
            var filtered = FilterDuplicateDimensions(
                result.DimXs.Concat(result.DimYs).ToList(),
                options.DistanceThreshold);

            result.DimXs = filtered.Where(d => Math.Abs(d.Rotation) < 1e-6).ToList();
            result.DimYs = filtered.Where(d => Math.Abs(Math.Abs(d.Rotation) - Math.PI / 2) < 1e-6).ToList();

            return result;
        }

        /// <summary>
        /// 为单个聚类生成单方向标注
        /// </summary>
        public List<RotatedDimension> CreateDimensionsForClusterSingleSide(
            List<Point3d> points, bool isXAxis, string layerName, ClusterDimOptions options)
        {
            var dims = new List<RotatedDimension>();
            if (points == null || points.Count < 2) return dims;

            var grouped = isXAxis ? GroupPointsForX(points) : GroupPointsForY(points);
            if (grouped.Count < 2) return dims;

            double offsetBase = options.Offset;
            double minDist = options.MinSpacing;

            DimensionFor direction = isXAxis
                ? (options.XDirectionIsUp ? DimensionFor.ForUp : DimensionFor.ForDown)
                : (options.YDirectionIsRight ? DimensionFor.ForRight : DimensionFor.ForLeft);

            var db = AcApp.DocumentManager.MdiActiveDocument.Database;

            for (int i = 0; i < grouped.Count - 1; i++)
            {
                var p1 = grouped[i];
                var p2 = grouped[i + 1];

                double dist = isXAxis
                    ? Math.Abs(p2.X - p1.X)
                    : Math.Abs(p2.Y - p1.Y);

                double offset = dist < minDist ? 2 * offsetBase : offsetBase;

                var dim = GetDimByTwoPoints(p1, p2, offset, direction);
                dim.DimensionStyle = db.Dimstyle;
                dim.Layer = layerName;
                dims.Add(dim);
            }

            return dims;
        }

        /// <summary>
        /// 根据两个点创建旋转标注
        /// </summary>
        public RotatedDimension GetDimByTwoPoints(Point3d p1, Point3d p2, double offsetDist, DimensionFor direction)
        {
            double angle = 0;
            var pointDistance = new Point3d();

            switch (direction)
            {
                case DimensionFor.ForLeft:
                    angle = Math.PI / 2;
                    pointDistance = p1.X < p2.X
                        ? p1 - new Vector3d(offsetDist, 0, 0)
                        : p2 - new Vector3d(offsetDist, 0, 0);
                    break;
                case DimensionFor.ForRight:
                    angle = Math.PI / 2;
                    pointDistance = p1.X < p2.X
                        ? p2 + new Vector3d(offsetDist, 0, 0)
                        : p1 + new Vector3d(offsetDist, 0, 0);
                    break;
                case DimensionFor.ForUp:
                    angle = 0;
                    pointDistance = p1.Y < p2.Y
                        ? p2 + new Vector3d(0, offsetDist, 0)
                        : p1 + new Vector3d(0, offsetDist, 0);
                    break;
                case DimensionFor.ForDown:
                    angle = 0;
                    pointDistance = p1.Y < p2.Y
                        ? p1 - new Vector3d(0, offsetDist, 0)
                        : p2 - new Vector3d(0, offsetDist, 0);
                    break;
            }

            return new RotatedDimension
            {
                XLine1Point = p1,
                XLine2Point = p2,
                DimLinePoint = pointDistance,
                Rotation = angle
            };
        }

        #region 私有工具

        private static List<Point3d> GroupPointsForX(List<Point3d> points)
        {
            return points
                .GroupBy(p => Math.Round(p.X, 4))
                .Select(g => g.OrderBy(p => p.Y).First())
                .OrderBy(p => p.X)
                .ToList();
        }

        private static List<Point3d> GroupPointsForY(List<Point3d> points)
        {
            return points
                .GroupBy(p => Math.Round(p.Y, 4))
                .Select(g => g.OrderBy(p => p.X).First())
                .OrderBy(p => p.Y)
                .ToList();
        }

        private static List<RotatedDimension> FilterDuplicateDimensions(
            List<RotatedDimension> dims, double threshold)
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
                    while (nxt < list.Count &&
                           Math.Abs(keySelector(list[nxt]) - keySelector(list[cur])) <= threshold)
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

        #endregion
    }

    /// <summary>
    /// 标注生成结果 DTO
    /// </summary>
    public class DimensionResult
    {
        public List<RotatedDimension> DimXs { get; set; } = new List<RotatedDimension>();
        public List<RotatedDimension> DimYs { get; set; } = new List<RotatedDimension>();
        public List<ClusterResult> Clusters { get; set; } = new List<ClusterResult>();

        public List<RotatedDimension> AllDimensions => DimXs.Concat(DimYs).ToList();
    }
}
