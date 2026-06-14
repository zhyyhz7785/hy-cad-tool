using HyCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>
    /// 外轮廓土/气分析：每组内仅 ymin 最低的基础图形按割线切分；
    /// 组内其余独立图形外轮廓 100% 空气。
    /// </summary>
    public static class RegionBoundaryAnalyzer
    {
        private const double YTolerance = 1e-6;
        private const double FoundationGraphicTolerance = 1e-3;

        public static int FindPrimarySoilGroupIndex(IReadOnlyList<IReadOnlyList<ReinRegion>> groups)
        {
            if (groups == null || groups.Count == 0)
                return -1;

            int primaryIndex = 0;
            double globalMinY = double.MaxValue;

            for (int g = 0; g < groups.Count; g++)
            {
                var groupRegions = groups[g];
                if (groupRegions == null)
                    continue;

                double groupMinY = double.MaxValue;
                foreach (var region in groupRegions)
                {
                    if (region?.Outer == null || region.Outer.VertexCount < 3)
                        continue;
                    groupMinY = Math.Min(groupMinY, GetMinY(region.Outer));
                }

                if (groupMinY < globalMinY)
                {
                    globalMinY = groupMinY;
                    primaryIndex = g;
                }
            }

            return groups.Count == 1 ? 0 : primaryIndex;
        }

        /// <summary>分析全部计算组：每组内 ymin 最低的基础图形独立做土/气切分。</summary>
        public static List<GroupBoundaryProfile> AnalyzeAllGroups(
            IReadOnlyList<IReadOnlyList<ReinRegion>> groups,
            double embedmentDepth,
            double foundationElevation)
        {
            var profiles = new List<GroupBoundaryProfile>();
            if (groups == null || groups.Count == 0)
                return profiles;

            int primarySoilGroupIndex = FindPrimarySoilGroupIndex(groups);
            for (int g = 0; g < groups.Count; g++)
            {
                var profile = AnalyzeGroup(groups[g], g, embedmentDepth, foundationElevation);
                profile.IsPrimarySoilGroup = g == primarySoilGroupIndex;
                profiles.Add(profile);
            }

            return profiles;
        }

        public static GroupBoundaryProfile AnalyzeGroup(
            IReadOnlyList<ReinRegion> regions,
            int groupIndex,
            double embedmentDepth,
            double foundationElevation)
        {
            var profiles = new List<RegionBoundaryProfile>();
            double groupMinX = double.MaxValue, groupMaxX = double.MinValue;
            double groupMinY = double.MaxValue;

            if (regions == null || regions.Count == 0)
            {
                return new GroupBoundaryProfile
                {
                    Context = new RegionElevationContext(groupIndex, 0, embedmentDepth, foundationElevation),
                    Regions = profiles,
                    GroupMinX = 0,
                    GroupMaxX = 0,
                    GroupMinY = 0
                };
            }

            var boundsList = new List<RegionBounds>();
            for (int r = 0; r < regions.Count; r++)
            {
                var region = regions[r];
                if (region?.Outer == null || region.Outer.VertexCount < 3)
                    continue;

                var bounds = ComputeBounds(region.Outer, r);
                boundsList.Add(bounds);
                groupMinY = Math.Min(groupMinY, bounds.MinY);
                groupMinX = Math.Min(groupMinX, bounds.MinX);
                groupMaxX = Math.Max(groupMaxX, bounds.MaxX);
            }

            if (groupMinY == double.MaxValue)
            {
                groupMinY = 0;
                groupMinX = 0;
                groupMaxX = 0;
            }

            var context = new RegionElevationContext(groupIndex, groupMinY, embedmentDepth, foundationElevation);
            double cutY = context.CutY;
            int foundationIndex = FindFoundationGraphicIndex(boundsList);
            double cutMinX = double.NaN, cutMaxX = double.NaN;

            if (foundationIndex >= 0 && foundationIndex < regions.Count)
            {
                var foundationOuter = regions[foundationIndex]?.Outer;
                if (foundationOuter != null && foundationOuter.VertexCount >= 3)
                    GetCutLineIntersectionXs(foundationOuter, cutY, out cutMinX, out cutMaxX);
            }

            for (int r = 0; r < regions.Count; r++)
            {
                var region = regions[r];
                if (region?.Outer == null || region.Outer.VertexCount < 3)
                {
                    profiles.Add(EmptyProfile(context, r));
                    continue;
                }

                double graphicMinY = GetMinY(region.Outer);
                bool isFoundation = r == foundationIndex;
                // 组内仅基础图形（ymin 最低）做土/气；其余独立图形外轮廓全部空气
                profiles.Add(isFoundation
                    ? AnalyzeFoundationGraphic(region, context, r, graphicMinY, cutY)
                    : AnalyzeAllAirGraphic(region, context, r, graphicMinY));
            }

            return new GroupBoundaryProfile
            {
                Context = context,
                Regions = profiles,
                GroupMinX = groupMinX,
                GroupMaxX = groupMaxX,
                GroupMinY = groupMinY,
                FoundationGraphicIndex = foundationIndex,
                CutY = cutY,
                CutIntersectionMinX = cutMinX,
                CutIntersectionMaxX = cutMaxX
            };
        }

        private static RegionBoundaryProfile AnalyzeFoundationGraphic(
            ReinRegion region,
            RegionElevationContext context,
            int regionIndex,
            double graphicMinY,
            double cutY)
        {
            var edges = ClassifyOuterByContourArc(region.Outer, cutY);
            return BuildProfile(context, regionIndex, edges, isFoundation: true, graphicMinY, cutY);
        }

        /// <summary>组内非基础图形：外轮廓全部标记为空气接触；预览整圈橙色。</summary>
        private static RegionBoundaryProfile AnalyzeAllAirGraphic(
            ReinRegion region,
            RegionElevationContext context,
            int regionIndex,
            double graphicMinY)
        {
            var edges = new List<ClassifiedBoundaryEdge>();
            var outer = region.Outer;

            for (int i = 0; i < outer.SegmentCount; i++)
            {
                var seg = outer.GetSegmentAt(i);
                if (seg.Length <= YTolerance)
                    continue;

                edges.Add(new ClassifiedBoundaryEdge
                {
                    Edge = seg,
                    Role = BoundaryEdgeRole.Air,
                    MidYElevation = (seg.StartPoint.Y + seg.EndPoint.Y) / 2.0,
                    SegmentIndex = i
                });
            }

            return BuildProfile(context, regionIndex, edges, isFoundation: false, graphicMinY, double.NaN);
        }

        /// <summary>
        /// 割线与外环求交，取最左/最右交点；左交点沿 CCW 至右交点弧段为土壤（绿），其余为空气（橙）。
        /// </summary>
        private static List<ClassifiedBoundaryEdge> ClassifyOuterByContourArc(Polyline2D outer, double cutY)
        {
            var edges = new List<ClassifiedBoundaryEdge>();
            var hits = CollectCutHits(outer, cutY);
            double perimeter = outer.GetTotalLength();

            if (hits.Count < 2 || perimeter <= YTolerance)
            {
                for (int i = 0; i < outer.SegmentCount; i++)
                {
                    var seg = outer.GetSegmentAt(i);
                    if (seg.Length <= YTolerance)
                        continue;

                    AddEdge(edges, seg, BoundaryEdgeRole.Soil, i);
                }

                return edges;
            }

            var leftHit = hits.OrderBy(h => h.X).ThenBy(h => h.Position).First();
            var rightHit = hits.OrderByDescending(h => h.X).ThenBy(h => h.Position).First();
            double leftPos = leftHit.Position;
            double rightPos = rightHit.Position;

            double cumulative = 0;
            for (int i = 0; i < outer.SegmentCount; i++)
            {
                var seg = outer.GetSegmentAt(i);
                if (seg.Length <= YTolerance)
                    continue;

                var pieces = SplitSegmentGeometry(seg, cutY);
                double traversed = 0;
                foreach (var piece in pieces)
                {
                    if (piece.Length <= YTolerance)
                        continue;

                    double midPos = cumulative + traversed + piece.Length / 2.0;
                    bool isSoil = IsOnCcwArc(midPos, leftPos, rightPos, perimeter);
                    AddEdge(edges, piece, isSoil ? BoundaryEdgeRole.Soil : BoundaryEdgeRole.Air, i);
                    traversed += piece.Length;
                }

                cumulative += seg.Length;
            }

            return edges;
        }

        private static bool IsOnCcwArc(double pos, double leftPos, double rightPos, double perimeter)
        {
            if (perimeter <= YTolerance || Math.Abs(leftPos - rightPos) <= YTolerance)
                return false;

            pos = NormalizePos(pos, perimeter);
            leftPos = NormalizePos(leftPos, perimeter);
            rightPos = NormalizePos(rightPos, perimeter);

            if (leftPos <= rightPos)
                return pos + YTolerance >= leftPos && pos - YTolerance <= rightPos;

            return pos + YTolerance >= leftPos || pos - YTolerance <= rightPos;
        }

        private static double NormalizePos(double pos, double perimeter)
        {
            if (perimeter <= YTolerance)
                return 0;

            pos %= perimeter;
            if (pos < 0)
                pos += perimeter;
            return pos;
        }

        private static List<Line2D> SplitSegmentGeometry(Line2D seg, double cutY)
        {
            var result = new List<Line2D>();
            if (seg.Length <= YTolerance)
                return result;

            var p0 = seg.StartPoint;
            var p1 = seg.EndPoint;
            double y0 = p0.Y, y1 = p1.Y;

            if (IsAbove(y0, cutY) && IsAbove(y1, cutY))
            {
                result.Add(seg);
                return result;
            }

            if (IsBelow(y0, cutY) && IsBelow(y1, cutY))
            {
                result.Add(seg);
                return result;
            }

            if (IsOnCutLine(y0, cutY) && IsOnCutLine(y1, cutY))
            {
                result.Add(seg);
                return result;
            }

            if (IsOnCutLine(y0, cutY))
            {
                result.Add(seg);
                return result;
            }

            if (IsOnCutLine(y1, cutY))
            {
                result.Add(seg);
                return result;
            }

            if ((y0 - cutY) * (y1 - cutY) >= 0)
            {
                result.Add(seg);
                return result;
            }

            double t = (cutY - y0) / (y1 - y0);
            var cutPoint = new Point2D(p0.X + t * (p1.X - p0.X), cutY);
            var lower = new Line2D(p0, cutPoint);
            var upper = new Line2D(cutPoint, p1);
            if (lower.Length > YTolerance)
                result.Add(lower);
            if (upper.Length > YTolerance)
                result.Add(upper);
            return result;
        }

        private static List<ContourHit> CollectCutHits(Polyline2D outer, double cutY)
        {
            var hits = new List<ContourHit>();
            double cumulative = 0;

            for (int i = 0; i < outer.SegmentCount; i++)
            {
                var seg = outer.GetSegmentAt(i);
                CollectSegmentCutHits(seg, cutY, cumulative, i, hits);
                cumulative += seg.Length;
            }

            return DeduplicateHits(hits);
        }

        private static void CollectSegmentCutHits(
            Line2D seg,
            double cutY,
            double segStartPos,
            int segmentIndex,
            List<ContourHit> hits)
        {
            double x0 = seg.StartPoint.X, y0 = seg.StartPoint.Y;
            double x1 = seg.EndPoint.X, y1 = seg.EndPoint.Y;

            if (Math.Abs(y0 - cutY) <= YTolerance && Math.Abs(y1 - cutY) <= YTolerance)
            {
                hits.Add(new ContourHit(x0, segStartPos, segmentIndex));
                hits.Add(new ContourHit(x1, segStartPos + seg.Length, segmentIndex));
                return;
            }

            if (Math.Abs(y0 - cutY) <= YTolerance)
                hits.Add(new ContourHit(x0, segStartPos, segmentIndex));
            if (Math.Abs(y1 - cutY) <= YTolerance)
                hits.Add(new ContourHit(x1, segStartPos + seg.Length, segmentIndex));

            if ((y0 - cutY) * (y1 - cutY) < 0)
            {
                double t = (cutY - y0) / (y1 - y0);
                double x = x0 + t * (x1 - x0);
                hits.Add(new ContourHit(x, segStartPos + t * seg.Length, segmentIndex));
            }
        }

        private static List<ContourHit> DeduplicateHits(List<ContourHit> hits)
        {
            if (hits.Count <= 1)
                return hits;

            const double posTol = 1e-3;
            return hits
                .OrderBy(h => h.Position)
                .Aggregate(new List<ContourHit>(), (acc, h) =>
                {
                    if (acc.Count == 0 || Math.Abs(acc[acc.Count - 1].Position - h.Position) > posTol)
                        acc.Add(h);
                    return acc;
                });
        }

        private static bool IsAbove(double y, double cutY) => y > cutY + YTolerance;
        private static bool IsBelow(double y, double cutY) => y < cutY - YTolerance;
        private static bool IsOnCutLine(double y, double cutY) => Math.Abs(y - cutY) <= YTolerance;

        private static void AddEdge(
            List<ClassifiedBoundaryEdge> edges,
            Line2D seg,
            BoundaryEdgeRole role,
            int segmentIndex)
        {
            if (seg.Length <= YTolerance)
                return;

            edges.Add(new ClassifiedBoundaryEdge
            {
                Edge = seg,
                Role = role,
                MidYElevation = (seg.StartPoint.Y + seg.EndPoint.Y) / 2.0,
                SegmentIndex = segmentIndex
            });
        }

        private static RegionBoundaryProfile BuildProfile(
            RegionElevationContext context,
            int regionIndex,
            List<ClassifiedBoundaryEdge> edges,
            bool isFoundation,
            double graphicMinY,
            double cutY)
        {
            var profile = new RegionBoundaryProfile
            {
                GroupIndex = context.GroupIndex,
                RegionIndexInGroup = regionIndex,
                Context = context,
                Edges = edges,
                IsFoundationGraphic = isFoundation,
                GraphicMinY = graphicMinY,
                CutY = cutY
            };

            foreach (var e in edges)
            {
                if (e.Role == BoundaryEdgeRole.Soil)
                    profile.SoilCount++;
                else
                    profile.AirCount++;
            }

            return profile;
        }

        private static void GetCutLineIntersectionXs(
            Polyline2D outer,
            double cutY,
            out double minX,
            out double maxX)
        {
            var hits = CollectCutHits(outer, cutY);
            if (hits.Count == 0)
            {
                minX = double.NaN;
                maxX = double.NaN;
                return;
            }

            minX = hits.Min(h => h.X);
            maxX = hits.Max(h => h.X);
        }

        private static int FindFoundationGraphicIndex(IReadOnlyList<RegionBounds> boundsList)
        {
            if (boundsList == null || boundsList.Count == 0)
                return -1;

            double groupMinY = boundsList.Min(b => b.MinY);
            int bestIndex = boundsList[0].Index;
            double bestArea = -1;

            foreach (var bounds in boundsList)
            {
                if (Math.Abs(bounds.MinY - groupMinY) > FoundationGraphicTolerance)
                    continue;

                double area = Math.Max(0, bounds.MaxX - bounds.MinX) * Math.Max(0, bounds.MaxY - bounds.MinY);
                if (area > bestArea)
                {
                    bestArea = area;
                    bestIndex = bounds.Index;
                }
            }

            return bestIndex;
        }

        private static RegionBounds ComputeBounds(Polyline2D poly, int index)
        {
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            for (int i = 0; i < poly.VertexCount; i++)
            {
                var p = poly.GetPointAt(i);
                minX = Math.Min(minX, p.X);
                maxX = Math.Max(maxX, p.X);
                minY = Math.Min(minY, p.Y);
                maxY = Math.Max(maxY, p.Y);
            }

            return new RegionBounds
            {
                Index = index,
                MinX = minX,
                MaxX = maxX,
                MinY = minY,
                MaxY = maxY
            };
        }

        private static double GetMinY(Polyline2D poly)
        {
            double minY = double.MaxValue;
            for (int i = 0; i < poly.VertexCount; i++)
                minY = Math.Min(minY, poly.GetPointAt(i).Y);
            return minY == double.MaxValue ? 0 : minY;
        }

        private static RegionBoundaryProfile EmptyProfile(RegionElevationContext context, int regionIndex)
        {
            return new RegionBoundaryProfile
            {
                GroupIndex = context?.GroupIndex ?? 0,
                RegionIndexInGroup = regionIndex,
                Context = context,
                Edges = Array.Empty<ClassifiedBoundaryEdge>()
            };
        }

        private readonly struct ContourHit
        {
            public ContourHit(double x, double position, int segmentIndex)
            {
                X = x;
                Position = position;
                SegmentIndex = segmentIndex;
            }

            public double X { get; }
            public double Position { get; }
            public int SegmentIndex { get; }
        }

        private sealed class RegionBounds
        {
            public int Index { get; set; }
            public double MinX { get; set; }
            public double MaxX { get; set; }
            public double MinY { get; set; }
            public double MaxY { get; set; }
        }
    }
}
