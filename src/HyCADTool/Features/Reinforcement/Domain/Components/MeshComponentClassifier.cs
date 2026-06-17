using HyCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>
    /// 网格单元 → 构件类型：矩形按尺寸+位置判型，板需上下皆空；三角形并入相邻最大矩形构件。
    /// </summary>
    public static class MeshComponentClassifier
    {
        private const double AnchorYToleranceMm = 80.0;
        private const double EdgeToleranceMm = 1.0;
        private const double MinSegmentLengthMm = 200.0;
        private const double ProbeOffsetMm = 1.0;

        public static List<ComponentRegion> Classify(
            IReadOnlyList<MeshCell> cells,
            IReadOnlyList<ReinRegion> regions,
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

            var snapshotTypes = rectRegions.Select(r => r.Type).ToList();
            rectRegions = RefineSlabRegions(rectRegions, snapshotTypes, regions);
            var snapshotAfterSlab = rectRegions.Select(r => r.Type).ToList();
            rectRegions = RefineLocalUnderlay(rectRegions, snapshotAfterSlab);
            DowngradeSmallMassConcrete(rectRegions, parameters);

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

        private static List<ComponentRegion> RefineSlabRegions(
            IReadOnlyList<ComponentRegion> rectRegions,
            IReadOnlyList<ComponentType> snapshotTypes,
            IReadOnlyList<ReinRegion> regions)
        {
            var refined = new List<ComponentRegion>();
            for (int i = 0; i < rectRegions.Count; i++)
            {
                var region = rectRegions[i];
                if (region?.Polygon == null || region.Polygon.VertexCount < 3)
                    continue;

                if (snapshotTypes[i] != ComponentType.Slab)
                {
                    refined.Add(region);
                    continue;
                }

                refined.AddRange(SplitSlabStrip(region, i, rectRegions, snapshotTypes, regions));
            }

            return refined;
        }

        private static List<ComponentRegion> RefineLocalUnderlay(
            IReadOnlyList<ComponentRegion> rectRegions,
            IReadOnlyList<ComponentType> snapshotTypes)
        {
            var refined = new List<ComponentRegion>();
            for (int i = 0; i < rectRegions.Count; i++)
            {
                var region = rectRegions[i];
                if (region?.Polygon == null || region.Polygon.VertexCount < 3)
                    continue;

                var snap = snapshotTypes[i];
                if (snap != ComponentType.BottomSlab && snap != ComponentType.MassConcrete)
                {
                    refined.Add(region);
                    continue;
                }

                refined.AddRange(SplitLocalUnderlayStrip(region, i, rectRegions, snapshotTypes, snap));
            }

            return refined;
        }

        private static void DowngradeSmallMassConcrete(
            IReadOnlyList<ComponentRegion> rectRegions,
            ComponentParameters parameters)
        {
            foreach (var region in rectRegions)
            {
                if (region?.Polygon == null || region.Type != ComponentType.MassConcrete)
                    continue;

                GetBounds(region.Polygon, out double minX, out double maxX, out double minY, out double maxY);
                double minSide = Math.Min(maxX - minX, maxY - minY);
                if (minSide < parameters.MassConcreteMinSizeMm)
                {
                    region.Type = ComponentType.LocalConcrete;
                    region.Priority = PriorityOf(ComponentType.LocalConcrete);
                }
            }
        }

        private static List<ComponentRegion> SplitLocalUnderlayStrip(
            ComponentRegion strip,
            int stripIndex,
            IReadOnlyList<ComponentRegion> allRects,
            IReadOnlyList<ComponentType> snapshotTypes,
            ComponentType originalType)
        {
            GetBounds(strip.Polygon, out double sMinX, out double sMaxX, out double sMinY, out double sMaxY);
            double stripHeight = sMaxY - sMinY;

            var breakpoints = new SortedSet<double> { sMinX, sMaxX };
            bool hasUpperLocal = false;
            for (int j = 0; j < allRects.Count; j++)
            {
                if (j == stripIndex || snapshotTypes[j] != ComponentType.LocalConcrete)
                    continue;

                var neighbor = allRects[j];
                if (neighbor?.Polygon == null || neighbor.Polygon.VertexCount < 3)
                    continue;

                GetBounds(neighbor.Polygon, out double nMinX, out double nMaxX, out double nMinY, out double _);
                if (Math.Abs(nMinY - sMaxY) > EdgeToleranceMm)
                    continue;
                if (nMaxX <= sMinX + EdgeToleranceMm || nMinX >= sMaxX - EdgeToleranceMm)
                    continue;

                hasUpperLocal = true;
                breakpoints.Add(Math.Max(sMinX, nMinX));
                breakpoints.Add(Math.Min(sMaxX, nMaxX));
            }

            if (!hasUpperLocal)
                return new List<ComponentRegion> { strip };

            var xs = breakpoints.ToList();
            var segments = new List<(double X0, double X1, ComponentType Type)>();

            for (int k = 0; k < xs.Count - 1; k++)
            {
                double xa = xs[k];
                double xb = xs[k + 1];
                if (xb - xa < EdgeToleranceMm)
                    continue;

                var type = ClassifyUnderlaySubSegment(
                    xa,
                    xb,
                    sMinY,
                    sMaxY,
                    stripIndex,
                    allRects,
                    snapshotTypes,
                    originalType);
                segments.Add((xa, xb, type));
            }

            segments = MergeAdjacentSegments(segments);

            if (segments.Count == 1 && segments[0].Type == originalType)
                return new List<ComponentRegion> { strip };

            var result = new List<ComponentRegion>(segments.Count);
            foreach (var seg in segments)
            {
                result.Add(CreateRectRegion(
                    seg.X0,
                    seg.X1,
                    sMinY,
                    sMaxY,
                    seg.Type,
                    Math.Min(seg.X1 - seg.X0, stripHeight)));
            }

            return result;
        }

        private static ComponentType ClassifyUnderlaySubSegment(
            double xa,
            double xb,
            double sMinY,
            double sMaxY,
            int stripIndex,
            IReadOnlyList<ComponentRegion> allRects,
            IReadOnlyList<ComponentType> snapshotTypes,
            ComponentType originalType)
        {
            double midX = (xa + xb) / 2.0;
            ComponentType? upType = FindUpperNeighborTypeAt(
                midX,
                sMaxY,
                stripIndex,
                allRects,
                snapshotTypes);
            ComponentType? downType = FindLowerNeighborTypeAt(
                midX,
                sMinY,
                stripIndex,
                allRects,
                snapshotTypes);

            if (upType == ComponentType.LocalConcrete && downType == ComponentType.BottomSlab)
                return ComponentType.LocalConcrete;

            return originalType;
        }

        private static List<ComponentRegion> SplitSlabStrip(
            ComponentRegion strip,
            int stripIndex,
            IReadOnlyList<ComponentRegion> allRects,
            IReadOnlyList<ComponentType> snapshotTypes,
            IReadOnlyList<ReinRegion> regions)
        {
            GetBounds(strip.Polygon, out double sMinX, out double sMaxX, out double sMinY, out double sMaxY);
            double stripHeight = sMaxY - sMinY;

            var breakpoints = new SortedSet<double> { sMinX, sMaxX };
            for (int j = 0; j < allRects.Count; j++)
            {
                if (j == stripIndex || snapshotTypes[j] == ComponentType.Slab)
                    continue;

                var neighbor = allRects[j];
                if (neighbor?.Polygon == null || neighbor.Polygon.VertexCount < 3)
                    continue;

                GetBounds(neighbor.Polygon, out double nMinX, out double nMaxX, out double nMinY, out double nMaxY);
                bool isUpper = Math.Abs(nMinY - sMaxY) <= EdgeToleranceMm;
                bool isLower = Math.Abs(nMaxY - sMinY) <= EdgeToleranceMm;
                if (!isUpper && !isLower)
                    continue;
                if (nMaxX <= sMinX + EdgeToleranceMm || nMinX >= sMaxX - EdgeToleranceMm)
                    continue;

                breakpoints.Add(Math.Max(sMinX, nMinX));
                breakpoints.Add(Math.Min(sMaxX, nMaxX));
            }

            var xs = breakpoints.ToList();
            var segments = new List<(double X0, double X1, ComponentType Type)>();

            for (int k = 0; k < xs.Count - 1; k++)
            {
                double xa = xs[k];
                double xb = xs[k + 1];
                if (xb - xa < EdgeToleranceMm)
                    continue;

                var type = ClassifySubSegment(xa, xb, sMinY, sMaxY, stripIndex, allRects, snapshotTypes, regions);
                segments.Add((xa, xb, type));
            }

            segments = MergeAdjacentSegments(segments);

            if (segments.Count == 1 && segments[0].Type == ComponentType.Slab)
                return new List<ComponentRegion> { strip };

            var result = new List<ComponentRegion>(segments.Count);
            foreach (var seg in segments)
            {
                result.Add(CreateRectRegion(
                    seg.X0,
                    seg.X1,
                    sMinY,
                    sMaxY,
                    seg.Type,
                    Math.Min(seg.X1 - seg.X0, stripHeight)));
            }

            return result;
        }

        private static ComponentType ClassifySubSegment(
            double xa,
            double xb,
            double sMinY,
            double sMaxY,
            int stripIndex,
            IReadOnlyList<ComponentRegion> allRects,
            IReadOnlyList<ComponentType> snapshotTypes,
            IReadOnlyList<ReinRegion> regions)
        {
            double midX = (xa + xb) / 2.0;
            ComponentType? upperNeighborType = FindUpperNeighborTypeAt(
                midX,
                sMaxY,
                stripIndex,
                allRects,
                snapshotTypes);

            bool aboveConcrete = upperNeighborType.HasValue
                || IsConcrete(new Point2D(midX, sMaxY + ProbeOffsetMm), regions);
            bool belowConcrete = HasLowerNeighborAt(
                    midX,
                    sMinY,
                    stripIndex,
                    allRects,
                    snapshotTypes)
                || IsConcrete(new Point2D(midX, sMinY - ProbeOffsetMm), regions);

            if (!belowConcrete)
                return ComponentType.Slab;

            if (aboveConcrete)
                return upperNeighborType ?? ComponentType.MassConcrete;

            return ComponentType.LocalConcrete;
        }

        private static bool HasLowerNeighborAt(
            double midX,
            double stripBottomY,
            int stripIndex,
            IReadOnlyList<ComponentRegion> allRects,
            IReadOnlyList<ComponentType> snapshotTypes)
        {
            for (int j = 0; j < allRects.Count; j++)
            {
                if (j == stripIndex || snapshotTypes[j] == ComponentType.Slab)
                    continue;

                var neighbor = allRects[j];
                if (neighbor?.Polygon == null || neighbor.Polygon.VertexCount < 3)
                    continue;

                GetBounds(neighbor.Polygon, out double nMinX, out double nMaxX, out double _, out double nMaxY);
                if (Math.Abs(nMaxY - stripBottomY) > EdgeToleranceMm)
                    continue;
                if (midX < nMinX - EdgeToleranceMm || midX > nMaxX + EdgeToleranceMm)
                    continue;

                return true;
            }

            return false;
        }

        private static ComponentType? FindUpperNeighborTypeAt(
            double midX,
            double stripTopY,
            int stripIndex,
            IReadOnlyList<ComponentRegion> allRects,
            IReadOnlyList<ComponentType> snapshotTypes)
        {
            for (int j = 0; j < allRects.Count; j++)
            {
                if (j == stripIndex || snapshotTypes[j] == ComponentType.Slab)
                    continue;

                var neighbor = allRects[j];
                if (neighbor?.Polygon == null || neighbor.Polygon.VertexCount < 3)
                    continue;

                GetBounds(neighbor.Polygon, out double nMinX, out double nMaxX, out double nMinY, out double _);
                if (Math.Abs(nMinY - stripTopY) > EdgeToleranceMm)
                    continue;
                if (midX < nMinX - EdgeToleranceMm || midX > nMaxX + EdgeToleranceMm)
                    continue;

                return snapshotTypes[j];
            }

            return null;
        }

        private static ComponentType? FindLowerNeighborTypeAt(
            double midX,
            double stripBottomY,
            int stripIndex,
            IReadOnlyList<ComponentRegion> allRects,
            IReadOnlyList<ComponentType> snapshotTypes)
        {
            for (int j = 0; j < allRects.Count; j++)
            {
                if (j == stripIndex || snapshotTypes[j] == ComponentType.Slab)
                    continue;

                var neighbor = allRects[j];
                if (neighbor?.Polygon == null || neighbor.Polygon.VertexCount < 3)
                    continue;

                GetBounds(neighbor.Polygon, out double nMinX, out double nMaxX, out double _, out double nMaxY);
                if (Math.Abs(nMaxY - stripBottomY) > EdgeToleranceMm)
                    continue;
                if (midX < nMinX - EdgeToleranceMm || midX > nMaxX + EdgeToleranceMm)
                    continue;

                return snapshotTypes[j];
            }

            return null;
        }

        private static List<(double X0, double X1, ComponentType Type)> MergeAdjacentSegments(
            List<(double X0, double X1, ComponentType Type)> segments)
        {
            if (segments.Count == 0)
                return segments;

            var merged = new List<(double X0, double X1, ComponentType Type)> { segments[0] };
            for (int i = 1; i < segments.Count; i++)
            {
                var cur = segments[i];
                var last = merged[merged.Count - 1];
                if (cur.Type == last.Type && Math.Abs(cur.X0 - last.X1) <= EdgeToleranceMm)
                {
                    merged[merged.Count - 1] = (last.X0, cur.X1, last.Type);
                }
                else
                {
                    merged.Add(cur);
                }
            }

            return merged;
        }

        private static ComponentRegion CreateRectRegion(
            double x0,
            double x1,
            double yBot,
            double yTop,
            ComponentType type,
            double thicknessMm)
        {
            return new ComponentRegion
            {
                Type = type,
                Polygon = new Polyline2D(new[]
                {
                    new Point2D(x0, yBot),
                    new Point2D(x1, yBot),
                    new Point2D(x1, yTop),
                    new Point2D(x0, yTop)
                }, isClosed: true),
                ThicknessMm = thicknessMm,
                Priority = PriorityOf(type)
            };
        }

        private static bool IsConcrete(Point2D point, IReadOnlyList<ReinRegion> regions)
        {
            if (regions == null || regions.Count == 0)
                return false;

            foreach (var region in regions)
            {
                if (region?.IsValidRebarPoint(point) == true)
                    return true;
            }

            return false;
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
