using HyCAD.Tables.Layout;

namespace HyCAD.Tables.Inference;

internal static class LineSegmentClusterer
{
    internal sealed class ClusterResult
    {
        public ClusterResult(
            IReadOnlyList<LineSegment2d> segments,
            IReadOnlyList<string> messages)
        {
            Segments = segments;
            Messages = messages;
        }

        public IReadOnlyList<LineSegment2d> Segments { get; }

        public IReadOnlyList<string> Messages { get; }
    }

    internal static ClusterResult Process(TableInferInput input, TableInferOptions options)
    {
        var messages = new List<string>();
        var working = new List<LineSegment2d>();

        foreach (var raw in input.Segments ?? Array.Empty<LineSegment2d>())
        {
            if (raw.Length < options.GridLineMinLengthMm)
                continue;

            if (input.ClipBounds is { } clip && !IsMidpointInside(raw, clip))
                continue;

            if (!TryClassify(raw, options, out var oriented))
            {
                messages.Add($"已丢弃近 45° 线段：({raw.X1:F1},{raw.Y1:F1})-({raw.X2:F1},{raw.Y2:F1})");
                continue;
            }

            working.Add(Normalize(oriented, options.EndpointSnapMm));
        }

        var merged = MergeCollinear(working, options);
        return new ClusterResult(merged, messages);
    }

    private static bool TryClassify(LineSegment2d raw, TableInferOptions options, out LineSegment2d oriented)
    {
        var dx = Math.Abs(raw.X2 - raw.X1);
        var dy = Math.Abs(raw.Y2 - raw.Y1);
        var max = Math.Max(dx, dy);
        if (max < options.EndpointSnapMm)
        {
            oriented = default;
            return false;
        }

        var min = Math.Min(dx, dy);
        var angleDeg = Math.Atan2(min, max) * 180.0 / Math.PI;
        if (angleDeg > 45.0 - options.AngleToleranceDeg && angleDeg < 45.0 + options.AngleToleranceDeg)
        {
            oriented = default;
            return false;
        }

        var orientation = dx >= dy ? GridLineOrientation.Horizontal : GridLineOrientation.Vertical;
        oriented = raw with { Orientation = orientation };
        return true;
    }

    private static LineSegment2d Normalize(LineSegment2d segment, double snap)
    {
        if (segment.Orientation == GridLineOrientation.Horizontal)
        {
            var y = Snap(segment.FixedCoord, snap);
            var x1 = Snap(Math.Min(segment.X1, segment.X2), snap);
            var x2 = Snap(Math.Max(segment.X1, segment.X2), snap);
            return new LineSegment2d(x1, y, x2, y, GridLineOrientation.Horizontal);
        }

        var x = Snap(segment.FixedCoord, snap);
        var y1 = Snap(Math.Min(segment.Y1, segment.Y2), snap);
        var y2 = Snap(Math.Max(segment.Y1, segment.Y2), snap);
        return new LineSegment2d(x, y1, x, y2, GridLineOrientation.Vertical);
    }

    private static List<LineSegment2d> MergeCollinear(IReadOnlyList<LineSegment2d> segments, TableInferOptions options)
    {
        var groups = segments
            .GroupBy(s => (s.Orientation, Fixed: Snap(s.FixedCoord, options.EndpointSnapMm)))
            .ToList();

        var merged = new List<LineSegment2d>();
        foreach (var group in groups)
        {
            var intervals = group
                .Select(s => (Start: s.IntervalStart, End: s.IntervalEnd))
                .OrderBy(i => i.Start)
                .ToList();

            var current = intervals[0];
            for (var i = 1; i < intervals.Count; i++)
            {
                var next = intervals[i];
                var gap = next.Start - current.End;
                if (gap > options.LineMergeGapMm)
                {
                    merged.Add(ToSegment(group.Key.Orientation, group.Key.Fixed, current.Start, current.End));
                    current = next;
                    continue;
                }

                if (gap < 0)
                {
                    current = (current.Start, Math.Max(current.End, next.End));
                    continue;
                }

                if (gap > 0)
                {
                    current = (current.Start, next.End);
                    continue;
                }

                merged.Add(ToSegment(group.Key.Orientation, group.Key.Fixed, current.Start, current.End));
                current = next;
            }

            merged.Add(ToSegment(group.Key.Orientation, group.Key.Fixed, current.Start, current.End));
        }

        return merged;
    }

    private static LineSegment2d ToSegment(
        GridLineOrientation orientation,
        double fixedCoord,
        double start,
        double end)
    {
        return orientation == GridLineOrientation.Horizontal
            ? new LineSegment2d(start, fixedCoord, end, fixedCoord, orientation)
            : new LineSegment2d(fixedCoord, start, fixedCoord, end, orientation);
    }

    private static bool IsMidpointInside(LineSegment2d segment, LayoutRect clip)
    {
        var mx = (segment.X1 + segment.X2) / 2;
        var my = (segment.Y1 + segment.Y2) / 2;
        return mx >= clip.Left && mx <= clip.Right && my >= clip.Bottom && my <= clip.Top;
    }

    private static double Snap(double value, double snap) =>
        snap <= 0 ? value : Math.Round(value / snap) * snap;
}
