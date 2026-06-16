using HyCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>
    /// 网格单元 → 构件类型：矩形按尺寸+位置判型，三角形并入相邻最大矩形构件。
    /// </summary>
    public static class MeshComponentClassifier
    {
        private const double AnchorYToleranceMm = 80.0;
        private const double EdgeToleranceMm = 1.0;
        private const double MinSegmentLengthMm = 200.0;

        public static List<ComponentRegion> Classify(
            IReadOnlyList<MeshCell> cells,
            double groupMinY,
            ComponentParameters parameters)
        {
            var result = new List<ComponentRegion>();
            if (cells == null || cells.Count == 0 || parameters == null)
                return result;

            var rectRegions = new List<ComponentRegion>();
            var triangles = new List<MeshCell>();

            foreach (var cell in cells)
            {
                if (cell?.Polygon == null || cell.Polygon.VertexCount < 3)
                    continue;

                if (cell.Kind == MeshCellKind.Rectangle)
                    rectRegions.Add(ToRectangleRegion(cell, groupMinY, parameters));
                else
                    triangles.Add(cell);
            }

            result.AddRange(rectRegions);

            foreach (var tri in triangles)
            {
                var type = ResolveTriangleType(tri, rectRegions);
                result.Add(ToTriangleRegion(tri, type));
            }

            return result;
        }

        private static ComponentRegion ToRectangleRegion(
            MeshCell cell,
            double groupMinY,
            ComponentParameters parameters)
        {
            GetBounds(cell.Polygon, out double minX, out double maxX, out double minY, out double maxY);
            double w = maxX - minX;
            double h = maxY - minY;
            var type = ClassifyRectangle(w, h, minY, groupMinY, parameters);

            return new ComponentRegion
            {
                Type = type,
                Polygon = new Polyline2D(cell.Polygon.Vertices, isClosed: true),
                ThicknessMm = Math.Min(w, h),
                Priority = PriorityOf(type)
            };
        }

        private static ComponentRegion ToTriangleRegion(MeshCell cell, ComponentType type)
        {
            GetBounds(cell.Polygon, out double minX, out double maxX, out double minY, out double maxY);
            return new ComponentRegion
            {
                Type = type,
                Polygon = new Polyline2D(cell.Polygon.Vertices, isClosed: true),
                ThicknessMm = Math.Min(maxX - minX, maxY - minY),
                Priority = PriorityOf(type)
            };
        }

        private static ComponentType ClassifyRectangle(
            double widthMm,
            double heightMm,
            double yBot,
            double groupMinY,
            ComponentParameters parameters)
        {
            double w = widthMm;
            double h = heightMm;
            double shortSide = Math.Min(w, h);
            bool atBottom = yBot <= groupMinY + AnchorYToleranceMm;

            if (shortSide >= parameters.MassConcreteMinSizeMm)
                return ComponentType.MassConcrete;

            if (h >= 2.0 * w
                && w <= parameters.WallMaxThicknessMm
                && h >= MinSegmentLengthMm)
            {
                return ComponentType.Wall;
            }

            if (w >= 2.0 * h && w >= MinSegmentLengthMm)
            {
                if (atBottom && h <= parameters.BottomSlabMaxThicknessMm)
                    return ComponentType.BottomSlab;
                if (h <= parameters.SlabMaxThicknessMm)
                    return ComponentType.Slab;
                return ComponentType.MassConcrete;
            }

            if (w <= parameters.BeamMaxWidthMm && h <= parameters.BeamMaxHeightMm)
                return ComponentType.Beam;

            return ComponentType.LocalConcrete;
        }

        private static ComponentType ResolveTriangleType(MeshCell triangle, IReadOnlyList<ComponentRegion> rectRegions)
        {
            if (rectRegions == null || rectRegions.Count == 0)
                return ComponentType.LocalConcrete;

            GetBounds(triangle.Polygon, out double tMinX, out double tMaxX, out double tMinY, out double tMaxY);
            var triCentroid = CentroidOf(triangle.Polygon);

            ComponentRegion bestAdjacent = null;
            double bestArea = 0;

            foreach (var region in rectRegions)
            {
                if (region?.Polygon == null || region.Polygon.VertexCount < 3)
                    continue;

                GetBounds(region.Polygon, out double rMinX, out double rMaxX, out double rMinY, out double rMaxY);
                if (!AreBoundsAdjacent(tMinX, tMaxX, tMinY, tMaxY, rMinX, rMaxX, rMinY, rMaxY))
                    continue;

                double area = (rMaxX - rMinX) * (rMaxY - rMinY);
                if (area > bestArea)
                {
                    bestArea = area;
                    bestAdjacent = region;
                }
            }

            if (bestAdjacent != null)
                return bestAdjacent.Type;

            ComponentRegion nearest = null;
            double nearestDist = double.MaxValue;
            foreach (var region in rectRegions)
            {
                if (region?.Polygon == null)
                    continue;

                double dist = triCentroid.DistanceTo(CentroidOf(region.Polygon));
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = region;
                }
            }

            return nearest?.Type ?? ComponentType.LocalConcrete;
        }

        private static bool AreBoundsAdjacent(
            double ax0, double ax1, double ay0, double ay1,
            double bx0, double bx1, double by0, double by1)
        {
            bool yOverlap = ay0 < by1 + EdgeToleranceMm && ay1 > by0 - EdgeToleranceMm;
            bool xOverlap = ax0 < bx1 + EdgeToleranceMm && ax1 > bx0 - EdgeToleranceMm;

            bool touchLeft = Math.Abs(ax1 - bx0) <= EdgeToleranceMm && yOverlap;
            bool touchRight = Math.Abs(bx1 - ax0) <= EdgeToleranceMm && yOverlap;
            bool touchBottom = Math.Abs(ay1 - by0) <= EdgeToleranceMm && xOverlap;
            bool touchTop = Math.Abs(by1 - ay0) <= EdgeToleranceMm && xOverlap;

            return touchLeft || touchRight || touchBottom || touchTop;
        }

        private static void GetBounds(Polygon2D poly, out double minX, out double maxX, out double minY, out double maxY)
        {
            minX = double.MaxValue;
            maxX = double.MinValue;
            minY = double.MaxValue;
            maxY = double.MinValue;

            foreach (var p in poly.Vertices)
            {
                minX = Math.Min(minX, p.X);
                maxX = Math.Max(maxX, p.X);
                minY = Math.Min(minY, p.Y);
                maxY = Math.Max(maxY, p.Y);
            }
        }

        private static void GetBounds(Polyline2D poly, out double minX, out double maxX, out double minY, out double maxY)
        {
            minX = double.MaxValue;
            maxX = double.MinValue;
            minY = double.MaxValue;
            maxY = double.MinValue;

            for (int i = 0; i < poly.VertexCount; i++)
            {
                var p = poly.GetPointAt(i);
                minX = Math.Min(minX, p.X);
                maxX = Math.Max(maxX, p.X);
                minY = Math.Min(minY, p.Y);
                maxY = Math.Max(maxY, p.Y);
            }
        }

        private static Point2D CentroidOf(Polygon2D poly)
        {
            if (poly == null || poly.VertexCount == 0)
                return Point2D.Origin;

            double x = 0;
            double y = 0;
            foreach (var p in poly.Vertices)
            {
                x += p.X;
                y += p.Y;
            }

            int n = poly.VertexCount;
            return new Point2D(x / n, y / n);
        }

        private static Point2D CentroidOf(Polyline2D poly)
        {
            if (poly == null || poly.VertexCount == 0)
                return Point2D.Origin;

            double x = 0;
            double y = 0;
            for (int i = 0; i < poly.VertexCount; i++)
            {
                var p = poly.GetPointAt(i);
                x += p.X;
                y += p.Y;
            }

            int n = poly.VertexCount;
            return new Point2D(x / n, y / n);
        }

        private static int PriorityOf(ComponentType type)
        {
            switch (type)
            {
                case ComponentType.BottomSlab: return 1;
                case ComponentType.Slab: return 2;
                case ComponentType.MassConcrete: return 3;
                case ComponentType.LocalConcrete: return 4;
                case ComponentType.Wall: return 5;
                case ComponentType.Beam: return 6;
                default: return 2;
            }
        }
    }
}
