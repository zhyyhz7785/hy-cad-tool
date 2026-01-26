using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Config;
using HyCADTool.HelpClass;
using HyCADTool.Models.Cluster;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.Tools
{
    public static partial class ZTools
    {
        /// <summary>
        /// 生成一组点单方向的标注（用于基础标注）
        /// </summary>
        public static List<RotatedDimension> CreateDimensionsForClusterSingleSide(
            List<Point3d> points,
            bool isXAxis,
            ObjectId layerId,
            ClusterDimOptions options)
        {
            var dims = new List<RotatedDimension>();
            if (points == null || points.Count < 2) return dims;
            var groupedPoints = isXAxis
                ? GroupPointsForX(points)
                : GroupPointsForY(points);
            if (groupedPoints.Count < 2) return dims;
            double offsetBase = 5 * options.Scale;
            double minDist = 3 * options.Scale;
            DimensionFor direction = isXAxis
                ? (options.XDirectionIsUp ? DimensionFor.ForUp : DimensionFor.ForDown)
                : (options.YDirectionIsRight ? DimensionFor.ForRight : DimensionFor.ForLeft);
            for (int i = 0; i < groupedPoints.Count - 1; i++)
            {
                var p1 = groupedPoints[i];
                var p2 = groupedPoints[i + 1];
                double dist = isXAxis
                    ? Math.Abs(p2.X - p1.X)
                    : Math.Abs(p2.Y - p1.Y);
                double offset = dist < minDist ? 2 * offsetBase : offsetBase;
                var dim = GetDimByTwoPoints(p1, p2, offset, direction, true);
                dim.LayerId = layerId;
                dims.Add(dim);
            }
            return dims;
        }
        /// <summary>
        /// 生成一组点的双方向标注（用于一般点聚类，如螺栓聚类）
        /// </summary>
        public static List<RotatedDimension> CreateDimensionsForClusterBothSides(
            List<Point3d> points,
            ObjectId layerIdX,
            ObjectId layerIdY,
            ClusterDimOptions options)
        {
            var dims = new List<RotatedDimension>();
            if (points == null || points.Count < 2) return dims;
            dims.AddRange(CreateDimensionsForClusterSingleSide(points, true, layerIdX, options));  // X方向
            dims.AddRange(CreateDimensionsForClusterSingleSide(points, false, layerIdY, options)); // Y方向
            return dims;
        }
        // 内部辅助分组方法
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
    }
}
