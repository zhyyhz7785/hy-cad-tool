using HyCAD.Tables.Layout;

namespace HyCAD.Tables.Inference;

/// <summary>
/// 过滤 PhotoSlot 内框等短装饰线段，保留主网格结构线（AC9）。
/// </summary>
internal static class StructuralGridFilter
{
    internal static IReadOnlyList<LineSegment2d> Filter(
        IReadOnlyList<LineSegment2d> segments,
        TableInferOptions options)
    {
        if (segments.Count == 0)
            return segments;

        var envelope = InferenceGeometry.ComputeEnvelope(segments);
        var tableWidth = Math.Max(envelope.Width, options.EndpointSnapMm);
        var tableHeight = Math.Max(envelope.Height, options.EndpointSnapMm);
        var snap = options.EndpointSnapMm;
        var ratio = options.StructuralMinSpanRatio;

        var filtered = new List<LineSegment2d>();
        foreach (var segment in segments)
        {
            if (IsStructural(segment, segments, envelope, tableWidth, tableHeight, snap, ratio))
                filtered.Add(segment);
        }

        return filtered.Count > 0 ? filtered : segments;
    }

    private static bool IsStructural(
        LineSegment2d segment,
        IReadOnlyList<LineSegment2d> all,
        LayoutRect envelope,
        double tableWidth,
        double tableHeight,
        double snap,
        double ratio)
    {
        if (TouchesEnvelope(segment, envelope, snap))
            return true;

        if (segment.Orientation == GridLineOrientation.Vertical)
        {
            if (segment.Length >= tableHeight * ratio)
                return true;

            return InferenceGeometry.MeasureVerticalCoverage(segment.FixedCoord, all, snap) >= tableHeight * ratio;
        }

        if (segment.Length >= tableWidth * ratio)
            return true;

        return InferenceGeometry.MeasureHorizontalCoverage(segment.FixedCoord, all, snap) >= tableWidth * ratio;
    }

    private static bool TouchesEnvelope(LineSegment2d segment, LayoutRect envelope, double snap)
    {
        if (segment.Orientation == GridLineOrientation.Vertical)
        {
            var x = segment.FixedCoord;
            if (Math.Abs(x - envelope.Left) <= snap || Math.Abs(x - envelope.Right) <= snap)
                return true;
        }
        else
        {
            var y = segment.FixedCoord;
            if (Math.Abs(y - envelope.Top) <= snap || Math.Abs(y - envelope.Bottom) <= snap)
                return true;
        }

        return segment.X1 <= envelope.Left + snap
            || segment.X2 >= envelope.Right - snap
            || segment.Y1 <= envelope.Bottom + snap
            || segment.Y2 >= envelope.Top - snap;
    }
}
