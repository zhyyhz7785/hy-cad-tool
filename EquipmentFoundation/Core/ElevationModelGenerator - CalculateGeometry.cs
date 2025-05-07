using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using EquipmentFoundation.Models;
using System;
using System.Collections.Generic;

namespace EquipmentFoundation
{
    public partial class ElevationModelGenerator
    {
        #region 几何计算
        private List<GeometryData> CalculateGeometry(List<GeometryData> geometryDatas, Dictionary<Polyline, List<BoundaryCondition>> boundaryConditions)
        {
            if (boundaryConditions == null) throw new ArgumentNullException(nameof(boundaryConditions));
            if (geometryDatas == null) throw new ArgumentNullException(nameof(geometryDatas));

            var resultGeometryDataList = new List<GeometryData>();
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;

            foreach (var geomData in geometryDatas)
            {
                if (geomData == null || geomData.Polygon == null)
                {
                    ed?.WriteMessage("\n警告: GeometryData 或其 Polygon 为 null，跳过处理。");
                    continue;
                }

                // 使用 GeometryData 中的 Elevation 属性，而不是外部 ElevationsDic
                double elevation = geomData.Elevation;
                var newGeomData = new GeometryData(geomData.Polygon, 0); // 创建新的 GeometryData 实例
                newGeomData.Elevation = elevation;

                var conditions = boundaryConditions.ContainsKey(geomData.Polygon) ? boundaryConditions[geomData.Polygon] : null;
                if (conditions != null)
                {
                    foreach (var condition in conditions)
                    {
                        double innerElevation = elevation;
                        double outerElevation = condition.IsSoilBoundary ? 0.000 :
                            (condition.AdjacentPolygon != null && geometryDatas.Any(g => g.Polygon == condition.AdjacentPolygon) ?
                            geometryDatas.First(g => g.Polygon == condition.AdjacentPolygon).Elevation : 0.000);
                        newGeomData.Walls.Add(new WallData(condition.Edge, innerElevation, outerElevation, 0, condition));
                    }
                }
                resultGeometryDataList.Add(newGeomData);
            }
            return resultGeometryDataList;
        }

        private void CalculateBaseAndWallThickness(List<GeometryData> geometryDatas)
        {
            if (geometryDatas == null) throw new ArgumentNullException(nameof(geometryDatas));
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;

            foreach (var geomData in geometryDatas)
            {
                if (geomData == null)
                {
                    ed?.WriteMessage("\n警告: GeometryData 为 null，跳过处理。");
                    continue;
                }
                double span = CalculateSpan(geomData);
                try
                {
                    var bolt = AnchorBoltFactory.CreateBolt("1");
                    geomData.BaseThickness = bolt != null ? CalculateBaseThickness(span, bolt) : MinBaseThickness;
                }
                catch (ArgumentException ex)
                {
                    ed?.WriteMessage($"\n警告: 创建 AnchorBolt 失败: {ex.Message}，使用默认厚度。");
                    geomData.BaseThickness = MinBaseThickness;
                }
                CalculateWallThickness(geomData);
                geomData.UpdateWallIndex();
            }
        }

        private void CalculateWallThickness(GeometryData geomData)
        {
            if (geomData?.Walls == null) return;
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            const double minThickness = 200;
            var updatedWalls = new List<WallData>();

            foreach (var wall in geomData.Walls)
            {
                if (!wall.Boundary.IsWall)
                {
                    updatedWalls.Add(new WallData(wall.Edge, wall.InnerElevation, wall.OuterElevation, 0, wall.Boundary));
                    continue;
                }
                double h = Math.Abs(wall.InnerElevation - wall.OuterElevation);
                double l = wall.Edge.Length;
                if (double.IsNaN(h) || double.IsNaN(l) || l <= 0)
                {
                    ed?.WriteMessage($"\n警告: 无效的墙体计算参数 [Height={h}, EdgeLength={l}]，使用默认厚度 {minThickness}。");
                    updatedWalls.Add(new WallData(wall.Edge, wall.InnerElevation, wall.OuterElevation, minThickness, wall.Boundary));
                    continue;
                }
                switch (TopFixityOption)
                {
                    case TopFixity.Hinged: h *= 1.25; break;
                    case TopFixity.Cantilever: h *= 1.5; break;
                    case TopFixity.Fixed: default: break;
                }
                double thickness = Math.Min(h, l) / 12;
                thickness = Math.Round(thickness / 50) * 50;
                thickness = Math.Max(thickness, minThickness);
                updatedWalls.Add(new WallData(wall.Edge, wall.InnerElevation, wall.OuterElevation, thickness, wall.Boundary));
            }
            geomData.Walls.Clear();
            geomData.Walls.AddRange(updatedWalls);
        }

        private double CalculateSpan(GeometryData geomData)
        {
            if (geomData?.Walls == null || geomData.Walls.Count == 0)
                return geomData?.Polygon.Length ?? 0;

            var edges = geomData.Walls.Select(w => w.Edge).ToList();
            if (edges.Count < 2) return geomData.Polygon.Length;

            double edgeLength = edges.Min(e => e.Length);
            double intersectionSpan = double.MaxValue;

            for (int i = 0; i < edges.Count - 1; i++)
            {
                for (int j = i + 1; j < edges.Count; j++)
                {
                    if (AreLinesIntersecting(edges[i], edges[j]))
                    {
                        double dist = edges[i].StartPoint.DistanceTo(edges[j].StartPoint);
                        intersectionSpan = Math.Min(intersectionSpan, dist);
                    }
                }
            }
            double span1 = edgeLength;
            double span2 = intersectionSpan == double.MaxValue ? span1 : intersectionSpan;
            return UseAverageSpan ? (span1 + span2) / 2 : Math.Max(span1, span2);
        }

        private double CalculateBaseThickness(double span, AnchorBolt bolt)
        {
            double spanBasedThickness = span / 10;
            double boltBasedThickness = bolt.NutHeight + 150;
            return Math.Max(Math.Max(spanBasedThickness, boltBasedThickness), MinBaseThickness);
        }
        #endregion
    }
}