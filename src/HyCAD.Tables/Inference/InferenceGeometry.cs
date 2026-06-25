using HyCAD.Tables.Layout;

namespace HyCAD.Tables.Inference;

/// <summary>线段包络、覆盖度与区间并集（AC9 共享）。</summary>
internal static class InferenceGeometry
{
    internal static LayoutRect ComputeEnvelope(IReadOnlyList<LineSegment2d> segments)
    {
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;

        foreach (var segment in segments)
        {
            minX = Math.Min(minX, Math.Min(segment.X1, segment.X2));
            maxX = Math.Max(maxX, Math.Max(segment.X1, segment.X2));
            minY = Math.Min(minY, Math.Min(segment.Y1, segment.Y2));
            maxY = Math.Max(maxY, Math.Max(segment.Y1, segment.Y2));
        }

        return new LayoutRect(minX, maxY, maxX, minY);
    }

    internal static double MeasureVerticalCoverage(
        double x,
        IReadOnlyList<LineSegment2d> segments,
        double snap)
    {
        var intervals = new List<(double Start, double End)>();
        foreach (var segment in segments)
        {
            if (segment.Orientation != GridLineOrientation.Vertical)
                continue;

            if (Math.Abs(segment.FixedCoord - x) > snap)
                continue;

            intervals.Add((segment.IntervalStart, segment.IntervalEnd));
        }

        return UnionLength(intervals);
    }

    internal static double MeasureHorizontalCoverage(
        double y,
        IReadOnlyList<LineSegment2d> segments,
        double snap)
    {
        var intervals = new List<(double Start, double End)>();
        foreach (var segment in segments)
        {
            if (segment.Orientation != GridLineOrientation.Horizontal)
                continue;

            if (Math.Abs(segment.FixedCoord - y) > snap)
                continue;

            intervals.Add((segment.IntervalStart, segment.IntervalEnd));
        }

        return UnionLength(intervals);
    }

    internal static double UnionLength(IReadOnlyList<(double Start, double End)> intervals)
    {
        if (intervals.Count == 0)
            return 0;

        var ordered = intervals.OrderBy(i => i.Start).ToList();
        var current = ordered[0];
        var total = 0.0;

        for (var i = 1; i < ordered.Count; i++)
        {
            var next = ordered[i];
            if (next.Start <= current.End)
            {
                current = (current.Start, Math.Max(current.End, next.End));
                continue;
            }

            total += Math.Max(0, current.End - current.Start);
            current = next;
        }

        total += Math.Max(0, current.End - current.Start);
        return total;
    }

    internal static double Snap(double value, double snap) =>
        snap <= 0 ? value : Math.Round(value / snap) * snap;

    internal static bool IsNear(double value, double target, double tolerance) =>
        Math.Abs(value - target) <= tolerance;

    internal static bool IsOnAxis(double value, IReadOnlyList<double> axes, double tolerance)
    {
        foreach (var axis in axes)
        {
            if (IsNear(value, axis, tolerance))
                return true;
        }

        return false;
    }
}
