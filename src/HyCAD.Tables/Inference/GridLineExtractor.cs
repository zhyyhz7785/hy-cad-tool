using HyCAD.Tables.Layout;

namespace HyCAD.Tables.Inference;

internal static class GridLineExtractor
{
    internal sealed class ExtractResult
    {
        public ExtractResult(GridLineSet gridLines, IReadOnlyList<string> messages)
        {
            GridLines = gridLines;
            Messages = messages;
        }

        public GridLineSet GridLines { get; }

        public IReadOnlyList<string> Messages { get; }
    }

    internal static ExtractAttempt TryExtract(
        IReadOnlyList<LineSegment2d> segments,
        TableInferOptions options)
    {
        var messages = new List<string>();
        if (!TryBuildGridAxes(segments, options, out var xs, out var ys, out var envelope))
        {
            messages.Add("网格边界不足：无法形成行列。");
            return ExtractAttempt.Fail(messages);
        }

        if (xs.Count < 2 || ys.Count < 2)
        {
            messages.Add("网格边界不足：无法形成行列。");
            return ExtractAttempt.Fail(messages);
        }

        var grid = new GridLineSet(xs, ys);
        if (grid.RowCount < options.MinRows || grid.ColCount < options.MinCols)
        {
            messages.Add($"网格尺寸过小：{grid.RowCount}×{grid.ColCount}。");
            return ExtractAttempt.Fail(messages);
        }

        if (!ValidateOuterFrame(segments, envelope, options, messages))
            return ExtractAttempt.Fail(messages);

        return ExtractAttempt.Success(new ExtractResult(grid, messages), messages);
    }

    internal sealed class ExtractAttempt
    {
        private ExtractAttempt(ExtractResult? result, IReadOnlyList<string> messages)
        {
            Result = result;
            Messages = messages;
        }

        public ExtractResult? Result { get; }

        public IReadOnlyList<string> Messages { get; }

        public static ExtractAttempt Success(ExtractResult result, IReadOnlyList<string> messages) =>
            new(result, messages);

        public static ExtractAttempt Fail(IReadOnlyList<string> messages) =>
            new(null, messages);
    }

    internal static bool HasEdgeCoverage(
        IReadOnlyList<LineSegment2d> segments,
        GridLineOrientation orientation,
        double fixedCoord,
        double start,
        double end,
        TableInferOptions options,
        bool isOuter)
    {
        var ratio = isOuter ? options.OuterEdgeCoverageRatio : options.InnerEdgeCoverageRatio;
        var edgeStart = Math.Min(start, end);
        var edgeEnd = Math.Max(start, end);
        var edgeLength = edgeEnd - edgeStart;
        if (edgeLength <= options.EndpointSnapMm)
            return true;

        var tolerance = isOuter
            ? Math.Max(options.EndpointSnapMm, options.OuterEdgeMatchToleranceMm)
            : options.EndpointSnapMm;

        var intervals = new List<(double Start, double End)>();
        foreach (var segment in segments)
        {
            if (segment.Orientation != orientation)
                continue;

            if (Math.Abs(segment.FixedCoord - fixedCoord) > tolerance)
                continue;

            var overlapStart = Math.Max(segment.IntervalStart, edgeStart);
            var overlapEnd = Math.Min(segment.IntervalEnd, edgeEnd);
            if (overlapEnd > overlapStart)
                intervals.Add((overlapStart, overlapEnd));
        }

        var covered = InferenceGeometry.UnionLength(intervals);
        return covered / edgeLength >= ratio;
    }

    private static bool TryBuildGridAxes(
        IReadOnlyList<LineSegment2d> segments,
        TableInferOptions options,
        out List<double> xs,
        out List<double> ys,
        out LayoutRect envelope)
    {
        xs = new List<double>();
        ys = new List<double>();
        envelope = default;

        if (segments.Count == 0)
            return false;

        envelope = InferenceGeometry.ComputeEnvelope(segments);
        var snap = options.EndpointSnapMm;
        var tableWidth = Math.Max(envelope.Width, snap);
        var tableHeight = Math.Max(envelope.Height, snap);
        var minVerticalAxis = tableHeight * options.MinAxisCoverageRatio;
        var minHorizontalAxis = tableWidth * options.MinAxisCoverageRatio;

        var xCandidates = new HashSet<double>
        {
            InferenceGeometry.Snap(envelope.Left, snap),
            InferenceGeometry.Snap(envelope.Right, snap),
        };
        var yCandidates = new HashSet<double>
        {
            InferenceGeometry.Snap(envelope.Top, snap),
            InferenceGeometry.Snap(envelope.Bottom, snap),
        };

        foreach (var group in segments
                     .Where(s => s.Orientation == GridLineOrientation.Vertical)
                     .GroupBy(s => InferenceGeometry.Snap(s.FixedCoord, snap)))
        {
            var x = group.Key;
            if (InferenceGeometry.IsNear(x, envelope.Left, snap)
                || InferenceGeometry.IsNear(x, envelope.Right, snap))
            {
                xCandidates.Add(x);
                continue;
            }

            if (InferenceGeometry.MeasureVerticalCoverage(x, segments, snap) >= minVerticalAxis)
                xCandidates.Add(x);
        }

        foreach (var group in segments
                     .Where(s => s.Orientation == GridLineOrientation.Horizontal)
                     .GroupBy(s => InferenceGeometry.Snap(s.FixedCoord, snap)))
        {
            var y = group.Key;
            if (InferenceGeometry.IsNear(y, envelope.Top, snap)
                || InferenceGeometry.IsNear(y, envelope.Bottom, snap))
            {
                yCandidates.Add(y);
                continue;
            }

            if (InferenceGeometry.MeasureHorizontalCoverage(y, segments, snap) >= minHorizontalAxis)
                yCandidates.Add(y);
        }

        SupplementJunctionAxes(segments, snap, envelope, xCandidates, yCandidates);

        xs = xCandidates.OrderBy(v => v).ToList();
        ys = yCandidates.OrderByDescending(v => v).ToList();
        return xs.Count >= 2 && ys.Count >= 2;
    }

    private static void SupplementJunctionAxes(
        IReadOnlyList<LineSegment2d> segments,
        double snap,
        LayoutRect envelope,
        HashSet<double> xCandidates,
        HashSet<double> yCandidates)
    {
        var xJunction = CountHorizontalEndpointHits(segments, snap);
        foreach (var entry in xJunction)
        {
            if (entry.Value < 2)
                continue;

            var x = entry.Key;
            if (x < envelope.Left - snap || x > envelope.Right + snap)
                continue;

            xCandidates.Add(x);
        }

        var yJunction = CountVerticalEndpointHits(segments, snap);
        foreach (var entry in yJunction)
        {
            if (entry.Value < 2)
                continue;

            var y = entry.Key;
            if (y < envelope.Bottom - snap || y > envelope.Top + snap)
                continue;

            yCandidates.Add(y);
        }

        foreach (var segment in segments)
        {
            if (segment.Orientation == GridLineOrientation.Horizontal)
            {
                AddEndpointIfOnAxis(segment.IntervalStart, xCandidates, snap);
                AddEndpointIfOnAxis(segment.IntervalEnd, xCandidates, snap);
            }
            else
            {
                AddEndpointIfOnAxis(segment.IntervalStart, yCandidates, snap);
                AddEndpointIfOnAxis(segment.IntervalEnd, yCandidates, snap);
            }
        }
    }

    private static void AddEndpointIfOnAxis(double value, HashSet<double> axes, double snap)
    {
        var snapped = InferenceGeometry.Snap(value, snap);
        foreach (var axis in axes)
        {
            if (InferenceGeometry.IsNear(snapped, axis, snap))
            {
                axes.Add(snapped);
                return;
            }
        }
    }

    private static Dictionary<double, int> CountHorizontalEndpointHits(
        IReadOnlyList<LineSegment2d> segments,
        double snap)
    {
        var counts = new Dictionary<double, int>();
        foreach (var segment in segments)
        {
            if (segment.Orientation != GridLineOrientation.Horizontal)
                continue;

            IncrementEndpoint(counts, InferenceGeometry.Snap(segment.IntervalStart, snap));
            IncrementEndpoint(counts, InferenceGeometry.Snap(segment.IntervalEnd, snap));
        }

        return counts;
    }

    private static Dictionary<double, int> CountVerticalEndpointHits(
        IReadOnlyList<LineSegment2d> segments,
        double snap)
    {
        var counts = new Dictionary<double, int>();
        foreach (var segment in segments)
        {
            if (segment.Orientation != GridLineOrientation.Vertical)
                continue;

            IncrementEndpoint(counts, InferenceGeometry.Snap(segment.IntervalStart, snap));
            IncrementEndpoint(counts, InferenceGeometry.Snap(segment.IntervalEnd, snap));
        }

        return counts;
    }

    private static void IncrementEndpoint(Dictionary<double, int> counts, double value)
    {
        if (counts.TryGetValue(value, out var count))
            counts[value] = count + 1;
        else
            counts[value] = 1;
    }

    private static bool ValidateOuterFrame(
        IReadOnlyList<LineSegment2d> segments,
        LayoutRect envelope,
        TableInferOptions options,
        List<string> messages)
    {
        var ok = true;
        ok &= CheckEdge(
            segments,
            GridLineOrientation.Vertical,
            envelope.Left,
            envelope.Bottom,
            envelope.Top,
            options,
            messages,
            "左外框");
        ok &= CheckEdge(
            segments,
            GridLineOrientation.Vertical,
            envelope.Right,
            envelope.Bottom,
            envelope.Top,
            options,
            messages,
            "右外框");
        ok &= CheckEdge(
            segments,
            GridLineOrientation.Horizontal,
            envelope.Top,
            envelope.Left,
            envelope.Right,
            options,
            messages,
            "上外框");
        ok &= CheckEdge(
            segments,
            GridLineOrientation.Horizontal,
            envelope.Bottom,
            envelope.Left,
            envelope.Right,
            options,
            messages,
            "下外框");
        return ok;
    }

    private static bool CheckEdge(
        IReadOnlyList<LineSegment2d> segments,
        GridLineOrientation orientation,
        double fixedCoord,
        double start,
        double end,
        TableInferOptions options,
        List<string> messages,
        string label)
    {
        if (HasEdgeCoverage(segments, orientation, fixedCoord, start, end, options, isOuter: true))
            return true;

        messages.Add($"{label}覆盖不足。");
        return false;
    }
}
