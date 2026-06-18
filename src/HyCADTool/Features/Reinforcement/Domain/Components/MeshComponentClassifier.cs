using HyCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>网格构件判型阶段（N22 初判 / N25 上部 X 打断 / N26 简化 / N27 仅底板 / N28 底板清版 / N16 完整精修）。</summary>
    public enum MeshClassifyStage
    {
        Initial,
        /// <summary>N25：初判 + 上下皆实之横条按上部结构 X 断点打断分段（侧段保留）。</summary>
        InitialUpperSplit,
        /// <summary>N26：5 级简化判型 + 土/气边界接触。</summary>
        InitialSimple,
        /// <summary>N27：仅底板判型（横条+贴组底+土接触）。</summary>
        BottomSlabOnly,
        /// <summary>N28：仅底板判型(清版，横条+h≤1500+下侧土接触，不限标高)。</summary>
        BottomSlabOnlyV2,
        /// <summary>N29：N28 底板 + 楼板判型(横条+h≤300+上下皆气接触→楼板)。</summary>
        BottomSlabAndSlabV3,
        Complete
    }

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
            ComponentParameters parameters,
            MeshClassifyStage stage = MeshClassifyStage.Complete,
            GroupBoundaryProfile boundaryProfile = null)
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

                if (cell.Kind == MeshCellKind.Rectangle && cell.Orientation != MeshCellOrientation.None)
                {
                    if (stage == MeshClassifyStage.InitialSimple
                        || stage == MeshClassifyStage.BottomSlabOnly
                        || stage == MeshClassifyStage.BottomSlabOnlyV2
                        || stage == MeshClassifyStage.BottomSlabAndSlabV3)
                        rectRegions.Add(ToRectangleRegionShell(cell));
                    else
                        rectRegions.Add(ToRectangleRegion(cell, groupMinY, parameters));
                }
                else if (cell.Kind == MeshCellKind.Triangle)
                    triangles.Add(cell);
            }

            if (stage == MeshClassifyStage.InitialSimple)
                ClassifySimple(rectRegions, regions, groupMinY, parameters, boundaryProfile);
            else if (stage == MeshClassifyStage.BottomSlabOnly)
                ClassifyBottomSlabOnly(rectRegions, regions, groupMinY, parameters, boundaryProfile);
            else if (stage == MeshClassifyStage.BottomSlabOnlyV2)
                ClassifyBottomSlabV2(rectRegions, regions, groupMinY, parameters, boundaryProfile);
            else if (stage == MeshClassifyStage.BottomSlabAndSlabV3)
                ClassifyBottomSlabAndSlabV3(rectRegions, regions, groupMinY, parameters, boundaryProfile);
            else if (stage == MeshClassifyStage.InitialUpperSplit)
            {
                var snapshotTypes = rectRegions.Select(r => r.Type).ToList();
                rectRegions = SplitSandwichedByUpperX(rectRegions, snapshotTypes, regions);
            }
            else if (stage == MeshClassifyStage.Complete)
            {
                var snapshotTypes = rectRegions.Select(r => r.Type).ToList();
                rectRegions = RefineRegions(rectRegions, snapshotTypes, regions, parameters);
            }

            result.AddRange(rectRegions);

            foreach (var tri in triangles)
            {
                var type = ResolveTriangleType(tri, rectRegions);
                result.Add(ToTriangleRegion(tri, type));
            }

            return result;
        }

        private static ComponentRegion ToRectangleRegionShell(MeshCell cell)
        {
            GetBounds(cell.Polygon, out double minX, out double maxX, out double minY, out double maxY);
            double w = maxX - minX;
            double h = maxY - minY;

            return new ComponentRegion
            {
                Type = ComponentType.MassConcrete,
                Polygon = new Polyline2D(cell.Polygon.Vertices, isClosed: true),
                ThicknessMm = Math.Min(w, h),
                Priority = PriorityOf(ComponentType.MassConcrete)
            };
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

        /// <summary>N28：仅底板(清版) — 横条 w>=2h w>=200 + h<=1500 + 下侧土接触 -> 底板，余者大体积。</summary>
        private static void ClassifyBottomSlabV2(
            List<ComponentRegion> rectRegions,
            IReadOnlyList<ReinRegion> regions,
            double groupMinY,
            ComponentParameters parameters,
            GroupBoundaryProfile boundaryProfile)
        {
            for (int i = 0; i < rectRegions.Count; i++)
            {
                var region = rectRegions[i];
                GetBounds(region.Polygon, out double minX, out double maxX, out double minY, out double maxY);
                double w = maxX - minX;
                double h = maxY - minY;

                bool horizontalStrip = w >= 2.0 * h && w >= MinSegmentLengthMm;
                bool withinThickness = h <= parameters.BottomSlabMaxThicknessMm;

                var type = ComponentType.MassConcrete;
                if (horizontalStrip && withinThickness
                    && BoundaryContactProbe.BottomContactsSoil(minX, maxX, minY, regions, boundaryProfile))
                {
                    type = ComponentType.BottomSlab;
                }

                region.Type = type;
                region.Priority = PriorityOf(type);
            }
        }

        /// <summary>N29：底板(同 N28) + 楼板 — 横条 w≥2h w≥200 + h≤300 + 上下气段重叠(底面探针) → 楼板(青)。</summary>
        private static void ClassifyBottomSlabAndSlabV3(
            List<ComponentRegion> rectRegions,
            IReadOnlyList<ReinRegion> regions,
            double groupMinY,
            ComponentParameters parameters,
            GroupBoundaryProfile boundaryProfile)
        {
            for (int i = 0; i < rectRegions.Count; i++)
            {
                var region = rectRegions[i];
                GetBounds(region.Polygon, out double minX, out double maxX, out double minY, out double maxY);
                double w = maxX - minX;
                double h = maxY - minY;

                bool horizontalStrip = w >= 2.0 * h && w >= MinSegmentLengthMm;

                var type = ComponentType.MassConcrete;
                if (horizontalStrip
                    && h <= parameters.BottomSlabMaxThicknessMm
                    && BoundaryContactProbe.BottomContactsSoil(minX, maxX, minY, regions, boundaryProfile))
                {
                    type = ComponentType.BottomSlab;
                }
                else if (horizontalStrip
                    && h <= parameters.SlabMaxThicknessMm
                    && (BoundaryContactProbe.IsSlabSandwichCandidate(
                            minX, maxX, minY, maxY, regions, boundaryProfile)
                        || BoundaryContactProbe.IsThinAirToAirBand(
                            minX, maxX, minY, maxY, parameters.SlabMaxThicknessMm,
                            regions, boundaryProfile)))
                {
                    type = ComponentType.Slab;
                }

                region.Type = type;
                region.Priority = PriorityOf(type);
            }
        }

        /// <summary>N27：仅底板 — 横条 w≥2h w≥200 贴组底 h≤1500 下侧土接触。</summary>
        private static void ClassifyBottomSlabOnly(
            List<ComponentRegion> rectRegions,
            IReadOnlyList<ReinRegion> regions,
            double groupMinY,
            ComponentParameters parameters,
            GroupBoundaryProfile boundaryProfile)
        {
            for (int i = 0; i < rectRegions.Count; i++)
            {
                var region = rectRegions[i];
                GetBounds(region.Polygon, out double minX, out double maxX, out double minY, out double maxY);
                double w = maxX - minX;
                double h = maxY - minY;

                var type = TryClassifyBottomSlab(
                    minX, maxX, minY, w, h, groupMinY, regions, parameters, boundaryProfile);
                region.Type = type;
                region.Priority = PriorityOf(type);
            }
        }

        private static ComponentType TryClassifyBottomSlab(
            double minX,
            double maxX,
            double yBot,
            double w,
            double h,
            double groupMinY,
            IReadOnlyList<ReinRegion> regions,
            ComponentParameters parameters,
            GroupBoundaryProfile boundaryProfile)
        {
            if (!BoundaryContactProbe.IsHorizontalStrip(w, h)
                || h > parameters.BottomSlabMaxThicknessMm)
            {
                return ComponentType.MassConcrete;
            }

            if (BoundaryContactProbe.BottomContactsSoil(minX, maxX, yBot, regions, boundaryProfile))
                return ComponentType.BottomSlab;

            return ComponentType.MassConcrete;
        }

        /// <summary>N26：5 级简化判型（Pass1 P1-P3 + Pass2 梁升级）。</summary>
        private static void ClassifySimple(
            List<ComponentRegion> rectRegions,
            IReadOnlyList<ReinRegion> regions,
            double groupMinY,
            ComponentParameters parameters,
            GroupBoundaryProfile boundaryProfile)
        {
            var types = new List<ComponentType>(rectRegions.Count);

            for (int i = 0; i < rectRegions.Count; i++)
            {
                var region = rectRegions[i];
                GetBounds(region.Polygon, out double minX, out double maxX, out double minY, out double maxY);
                double w = maxX - minX;
                double h = maxY - minY;

                var type = ClassifySimplePhase1(
                    minX, maxX, minY, maxY, w, h,
                    groupMinY, regions, parameters, boundaryProfile);
                types.Add(type);
                region.Type = type;
                region.Priority = PriorityOf(type);
            }

            for (int i = 0; i < rectRegions.Count; i++)
            {
                if (types[i] != ComponentType.MassConcrete)
                    continue;

                var region = rectRegions[i];
                GetBounds(region.Polygon, out double minX, out double maxX, out double minY, out double maxY);
                double w = maxX - minX;
                double h = maxY - minY;

                if (w > parameters.BeamMaxWidthMm || h > parameters.BeamMaxHeightMm)
                    continue;
                if (BoundaryContactProbe.IsHorizontalStrip(w, h)
                    || BoundaryContactProbe.IsVerticalStrip(w, h, parameters))
                    continue;

                double midX = (minX + maxX) / 2.0;
                var upperType = FindUpperNeighborTypeAt(midX, maxY, i, rectRegions, types);
                if (upperType != ComponentType.Slab)
                    continue;
                if (!BoundaryContactProbe.ContactsAirAt(midX, minY, isAbove: false, regions, boundaryProfile))
                    continue;

                types[i] = ComponentType.Beam;
                region.Type = ComponentType.Beam;
                region.Priority = PriorityOf(ComponentType.Beam);
            }
        }

        private static ComponentType ClassifySimplePhase1(
            double minX,
            double maxX,
            double yBot,
            double yTop,
            double w,
            double h,
            double groupMinY,
            IReadOnlyList<ReinRegion> regions,
            ComponentParameters parameters,
            GroupBoundaryProfile boundaryProfile)
        {
            if (BoundaryContactProbe.IsHorizontalStrip(w, h))
            {
                if (TryClassifyBottomSlab(
                        minX, maxX, yBot, w, h, groupMinY, regions, parameters, boundaryProfile)
                    == ComponentType.BottomSlab)
                {
                    return ComponentType.BottomSlab;
                }

                if (h <= parameters.SlabMaxThicknessMm
                    && BoundaryContactProbe.ContactsAirAlongSpan(minX, maxX, yTop, isAbove: true, regions, boundaryProfile)
                    && BoundaryContactProbe.ContactsAirAlongSpan(minX, maxX, yBot, isAbove: false, regions, boundaryProfile))
                {
                    return ComponentType.Slab;
                }
            }

            if (BoundaryContactProbe.IsVerticalStrip(w, h, parameters))
                return ComponentType.Wall;

            return ComponentType.MassConcrete;
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

        /// <summary>
        /// 对上下均为混凝土内部的横向条带：按上部邻居 X 断点打断为满宽多段（侧段不删），正下方随上部归型。
        /// 仅在条带中心判定一次上下皆实；子段不再重复判定，避免侧段因上部较窄被误删。
        /// </summary>
        private static List<ComponentRegion> SplitSandwichedByUpperX(
            IReadOnlyList<ComponentRegion> rectRegions,
            IReadOnlyList<ComponentType> snapshotTypes,
            IReadOnlyList<ReinRegion> regions)
        {
            var result = new List<ComponentRegion>(rectRegions.Count);
            for (int i = 0; i < rectRegions.Count; i++)
            {
                var strip = rectRegions[i];
                if (strip?.Polygon == null || strip.Polygon.VertexCount < 3)
                    continue;

                if (TrySplitSandwichedStrip(strip, i, rectRegions, snapshotTypes, regions, out var split))
                    result.AddRange(split);
                else
                    result.Add(strip);
            }

            return result;
        }

        private static bool TrySplitSandwichedStrip(
            ComponentRegion strip,
            int stripIndex,
            IReadOnlyList<ComponentRegion> allRects,
            IReadOnlyList<ComponentType> snapshotTypes,
            IReadOnlyList<ReinRegion> regions,
            out List<ComponentRegion> split)
        {
            split = null;
            GetBounds(strip.Polygon, out double sMinX, out double sMaxX, out double sMinY, out double sMaxY);
            double w = sMaxX - sMinX;
            double h = sMaxY - sMinY;
            if (w < 2.0 * h || w < MinSegmentLengthMm)
                return false;

            double midX = (sMinX + sMaxX) / 2.0;
            if (!IsSandwichedInternal(midX, sMinY, sMaxY, stripIndex, allRects, snapshotTypes, regions))
                return false;

            var breakpoints = CollectUpperSplitBreakpoints(sMinX, sMaxX, sMaxY, stripIndex, allRects);
            if (breakpoints == null || breakpoints.Count < 2)
                return false;

            double stripHeight = sMaxY - sMinY;
            var initialType = snapshotTypes[stripIndex];
            var segments = new List<(double X0, double X1, ComponentType Type)>();

            for (int k = 0; k < breakpoints.Count - 1; k++)
            {
                double xa = breakpoints[k];
                double xb = breakpoints[k + 1];
                if (xb - xa < EdgeToleranceMm)
                    continue;

                double segMid = (xa + xb) / 2.0;
                var upperType = FindUpperNeighborTypeAt(
                    segMid, sMaxY, stripIndex, allRects, snapshotTypes);
                var type = upperType ?? initialType;
                segments.Add((xa, xb, type));
            }

            if (segments.Count == 0)
                return false;

            segments = MergeAdjacentSegments(segments);
            if (segments.Count == 1
                && segments[0].Type == strip.Type
                && Math.Abs(segments[0].X0 - sMinX) <= EdgeToleranceMm
                && Math.Abs(segments[0].X1 - sMaxX) <= EdgeToleranceMm)
            {
                return false;
            }

            split = new List<ComponentRegion>(segments.Count);
            foreach (var seg in segments)
            {
                split.Add(CreateRectRegion(
                    seg.X0, seg.X1, sMinY, sMaxY, seg.Type,
                    Math.Min(seg.X1 - seg.X0, stripHeight)));
            }

            return true;
        }

        /// <summary>条带两端 + 上部邻居左右边 X，用于全宽打断（不丢弃侧段）。</summary>
        private static List<double> CollectUpperSplitBreakpoints(
            double sMinX,
            double sMaxX,
            double stripTopY,
            int stripIndex,
            IReadOnlyList<ComponentRegion> allRects)
        {
            var breakpoints = new SortedSet<double> { sMinX, sMaxX };
            bool hasUpper = false;

            for (int j = 0; j < allRects.Count; j++)
            {
                if (j == stripIndex)
                    continue;

                var neighbor = allRects[j];
                if (neighbor?.Polygon == null || neighbor.Polygon.VertexCount < 3)
                    continue;

                GetBounds(neighbor.Polygon, out double nMinX, out double nMaxX, out double nMinY, out double _);
                if (Math.Abs(nMinY - stripTopY) > EdgeToleranceMm)
                    continue;
                if (nMaxX <= sMinX + EdgeToleranceMm || nMinX >= sMaxX - EdgeToleranceMm)
                    continue;

                hasUpper = true;
                breakpoints.Add(Math.Max(sMinX, nMinX));
                breakpoints.Add(Math.Min(sMaxX, nMaxX));
            }

            if (!hasUpper)
                return null;

            return breakpoints.ToList();
        }

        private static bool IsSandwichedInternal(
            double midX,
            double sMinY,
            double sMaxY,
            int stripIndex,
            IReadOnlyList<ComponentRegion> allRects,
            IReadOnlyList<ComponentType> snapshotTypes,
            IReadOnlyList<ReinRegion> regions)
        {
            var upperType = FindUpperNeighborTypeAt(midX, sMaxY, stripIndex, allRects, snapshotTypes);
            var lowerType = FindLowerNeighborTypeAt(midX, sMinY, stripIndex, allRects, snapshotTypes);
            bool aboveConcrete = upperType.HasValue
                || IsConcrete(new Point2D(midX, sMaxY + ProbeOffsetMm), regions);
            bool belowConcrete = lowerType.HasValue
                || IsConcrete(new Point2D(midX, sMinY - ProbeOffsetMm), regions);
            return aboveConcrete && belowConcrete;
        }

        private static List<ComponentRegion> RefineRegions(
            IReadOnlyList<ComponentRegion> rectRegions,
            IReadOnlyList<ComponentType> snapshotTypes,
            IReadOnlyList<ReinRegion> regions,
            ComponentParameters parameters)
        {
            var refined = new List<ComponentRegion>();
            for (int i = 0; i < rectRegions.Count; i++)
            {
                var region = rectRegions[i];
                if (region?.Polygon == null || region.Polygon.VertexCount < 3)
                    continue;

                var snap = snapshotTypes[i];
                if (snap != ComponentType.Slab
                    && snap != ComponentType.MassConcrete
                    && snap != ComponentType.LocalConcrete)
                {
                    refined.Add(region);
                    continue;
                }

                refined.AddRange(SplitStrip(region, i, rectRegions, snapshotTypes, regions, parameters, snap));
            }

            return refined;
        }

        private static List<ComponentRegion> SplitStrip(
            ComponentRegion strip,
            int stripIndex,
            IReadOnlyList<ComponentRegion> allRects,
            IReadOnlyList<ComponentType> snapshotTypes,
            IReadOnlyList<ReinRegion> regions,
            ComponentParameters parameters,
            ComponentType originalType)
        {
            GetBounds(strip.Polygon, out double sMinX, out double sMaxX, out double sMinY, out double sMaxY);
            double stripHeight = sMaxY - sMinY;

            var breakpoints = new SortedSet<double> { sMinX, sMaxX };
            for (int j = 0; j < allRects.Count; j++)
            {
                if (j == stripIndex)
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

                var type = ClassifySegment(
                    xa,
                    xb,
                    sMinY,
                    sMaxY,
                    stripIndex,
                    allRects,
                    snapshotTypes,
                    regions,
                    parameters);
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

        private static ComponentType ClassifySegment(
            double xa,
            double xb,
            double sMinY,
            double sMaxY,
            int stripIndex,
            IReadOnlyList<ComponentRegion> allRects,
            IReadOnlyList<ComponentType> snapshotTypes,
            IReadOnlyList<ReinRegion> regions,
            ComponentParameters parameters)
        {
            double midX = (xa + xb) / 2.0;
            double h = sMaxY - sMinY;
            ComponentType? upperNeighborType = FindUpperNeighborTypeAt(
                midX,
                sMaxY,
                stripIndex,
                allRects,
                snapshotTypes);
            ComponentType? lowerNeighborType = FindLowerNeighborTypeAt(
                midX,
                sMinY,
                stripIndex,
                allRects,
                snapshotTypes);

            bool aboveConcrete = upperNeighborType.HasValue
                || IsConcrete(new Point2D(midX, sMaxY + ProbeOffsetMm), regions);
            bool belowConcrete = lowerNeighborType.HasValue
                || IsConcrete(new Point2D(midX, sMinY - ProbeOffsetMm), regions);

            if (!belowConcrete)
                return ComponentType.Slab;

            if (upperNeighborType.HasValue)
                return upperNeighborType.Value;

            if (lowerNeighborType == ComponentType.Wall || lowerNeighborType == ComponentType.Beam)
                return ComponentType.Slab;

            if (!aboveConcrete
                && (lowerNeighborType == ComponentType.BottomSlab || lowerNeighborType == ComponentType.Slab)
                && h <= parameters.LocalBumpMaxHeightMm)
            {
                return ComponentType.LocalConcrete;
            }

            return ComponentType.MassConcrete;
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
                if (j == stripIndex)
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
                if (j == stripIndex)
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
