using HyCAD.Tables.Data;
using HyCAD.Tables.Inference;
using HyCAD.Tables.Layout;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Tests;

internal static class TableInferTestFixtures
{
    internal static TableInferInput BuildUniformGrid(
        int rows,
        int cols,
        double cellWidth,
        double cellHeight,
        double left = 0,
        double top = 200,
        IEnumerable<int>? omitVerticalBoundaries = null,
        IEnumerable<int>? omitHorizontalBoundaries = null)
    {
        var xs = Enumerable.Range(0, cols + 1).Select(i => left + i * cellWidth).ToArray();
        var ys = Enumerable.Range(0, rows + 1).Select(i => top - i * cellHeight).ToArray();
        return BuildFromBoundaries(xs, ys, omitVerticalBoundaries, omitHorizontalBoundaries);
    }

    internal static TableInferInput BuildFromBoundaries(
        IReadOnlyList<double> xs,
        IReadOnlyList<double> ys,
        IEnumerable<int>? omitVerticalBoundaries = null,
        IEnumerable<int>? omitHorizontalBoundaries = null,
        IReadOnlyList<TextBox2d>? texts = null)
    {
        var segments = new List<LineSegment2d>();
        var omitVertical = omitVerticalBoundaries == null
            ? new HashSet<int>()
            : new HashSet<int>(omitVerticalBoundaries);
        var omitHorizontal = omitHorizontalBoundaries == null
            ? new HashSet<int>()
            : new HashSet<int>(omitHorizontalBoundaries);

        for (var c = 0; c < xs.Count; c++)
        {
            if (omitVertical.Contains(c))
                continue;

            var x = xs[c];
            for (var r = 0; r < ys.Count - 1; r++)
                segments.Add(SegmentVertical(x, ys[r + 1], ys[r]));
        }

        for (var r = 0; r < ys.Count; r++)
        {
            if (omitHorizontal.Contains(r))
                continue;

            var y = ys[r];
            for (var c = 0; c < xs.Count - 1; c++)
                segments.Add(SegmentHorizontal(y, xs[c], xs[c + 1]));
        }

        return new TableInferInput
        {
            Segments = segments,
            Texts = texts ?? Array.Empty<TextBox2d>(),
        };
    }

    internal static TextBox2d TextAtCell(
        IReadOnlyList<double> xs,
        IReadOnlyList<double> ys,
        int row,
        int col,
        string content,
        double height = 3.5)
    {
        var left = xs[col];
        var right = xs[col + 1];
        var top = ys[row];
        var bottom = ys[row + 1];
        var bounds = new LayoutRect(left, top, right, bottom);
        return new TextBox2d(bounds, content, height);
    }

    internal static TableInferInput FromGoldenGrid(TableGrid grid, double originX = 0, double originY = 0)
    {
        var layout = TableLayout.Create(grid, originX, originY);
        var segments = BorderGridResolver.Resolve(layout)
            .Select(ToSegment)
            .ToList();

        var texts = new List<TextBox2d>();
        foreach (var cell in layout.EnumerateVisibleCells())
        {
            if (!grid.Data.Cells.TryGetValue(cell.Addr, out var value))
                continue;

            var text = GetDisplayText(value);
            if (string.IsNullOrEmpty(text))
                continue;

            texts.Add(new TextBox2d(cell.Bounds, text, 3.5));
        }

        return new TableInferInput { Segments = segments, Texts = texts };
    }

    internal static TableInferInput WithCarrierBounds(TableInferInput input, LayoutRect bounds)
    {
        var segments = input.Segments.ToList();
        segments.Add(SegmentHorizontal(bounds.Top, bounds.Left, bounds.Right));
        segments.Add(SegmentHorizontal(bounds.Bottom, bounds.Left, bounds.Right));
        segments.Add(SegmentVertical(bounds.Left, bounds.Bottom, bounds.Top));
        segments.Add(SegmentVertical(bounds.Right, bounds.Bottom, bounds.Top));
        return new TableInferInput(segments, input.Texts);
    }

    internal static TableInferInput WithInsetRect(TableInferInput input, LayoutRect bounds, double inset)
    {
        var inner = new LayoutRect(
            bounds.Left + inset,
            bounds.Top - inset,
            bounds.Right - inset,
            bounds.Bottom + inset);
        var segments = input.Segments.ToList();
        segments.Add(SegmentHorizontal(inner.Top, inner.Left, inner.Right));
        segments.Add(SegmentHorizontal(inner.Bottom, inner.Left, inner.Right));
        segments.Add(SegmentVertical(inner.Left, inner.Bottom, inner.Top));
        segments.Add(SegmentVertical(inner.Right, inner.Bottom, inner.Top));
        return new TableInferInput(segments, input.Texts);
    }

    internal static TableInferInput WithExplodeOffsetOuterFrames(TableInferInput input, LayoutRect bounds, double offset = 0.175)
    {
        var segments = input.Segments.ToList();
        var ys = new[] { bounds.Bottom, bounds.Top };
        var rowHeight = bounds.Height / 7.0;
        for (var r = 0; r < 7; r++)
        {
            var y1 = bounds.Top - r * rowHeight;
            var y2 = y1 - rowHeight;
            segments.Add(SegmentVertical(bounds.Left - offset, y2, y1));
            segments.Add(SegmentVertical(bounds.Right + offset, y2, y1));
        }

        return new TableInferInput(segments, input.Texts);
    }

    internal static TableInferInput BuildUniformGridWithOuterVerticalGaps(
        int rows,
        int cols,
        double cellWidth,
        double cellHeight,
        double gap,
        double left = 0,
        double top = 200)
    {
        var xs = Enumerable.Range(0, cols + 1).Select(i => left + i * cellWidth).ToArray();
        var ys = Enumerable.Range(0, rows + 1).Select(i => top - i * cellHeight).ToArray();
        var segments = new List<LineSegment2d>();

        for (var c = 0; c < xs.Length; c++)
        {
            var x = xs[c];
            for (var r = 0; r < ys.Length - 1; r++)
            {
                var yTop = ys[r];
                var yBottom = ys[r + 1];
                if (c == 0 || c == xs.Length - 1)
                {
                    var mid = (yTop + yBottom) / 2;
                    segments.Add(SegmentVertical(x, yBottom, mid - gap / 2));
                    segments.Add(SegmentVertical(x, mid + gap / 2, yTop));
                    continue;
                }

                segments.Add(SegmentVertical(x, yBottom, yTop));
            }
        }

        for (var r = 0; r < ys.Length; r++)
        {
            var y = ys[r];
            for (var c = 0; c < xs.Length - 1; c++)
                segments.Add(SegmentHorizontal(y, xs[c], xs[c + 1]));
        }

        return new TableInferInput { Segments = segments };
    }

    internal static double CompareGolden(TableGrid inferred, TableGrid golden, double snap = 1.0)
    {
        var topology = CompareTopology(inferred, golden, snap);
        var merge = CompareMerges(inferred, golden);
        var text = CompareTexts(inferred, golden);
        return 0.4 * topology + 0.3 * merge + 0.3 * text;
    }

    private static LineSegment2d SegmentHorizontal(double y, double x1, double x2) =>
        new(x1, y, x2, y, GridLineOrientation.Horizontal);

    private static LineSegment2d SegmentVertical(double x, double y1, double y2) =>
        new(x, y1, x, y2, GridLineOrientation.Vertical);

    private static LineSegment2d ToSegment(BorderGridSegment segment)
    {
        return segment.Orientation == GridLineOrientation.Vertical
            ? SegmentVertical(segment.FixedCoord, segment.Start, segment.End)
            : SegmentHorizontal(segment.FixedCoord, segment.Start, segment.End);
    }

    private static string GetDisplayText(CellValue value)
    {
        if (value.Runs != null && value.Runs.Count > 0)
            return string.Concat(value.Runs.Select(r => r.Text));

        return value.Text ?? string.Empty;
    }

    private static double CompareTopology(TableGrid inferred, TableGrid golden, double snap)
    {
        var a = inferred.Structure.Topology;
        var b = golden.Structure.Topology;
        if (a.RowCount != b.RowCount || a.ColCount != b.ColCount)
            return 0;

        for (var i = 0; i < a.RowCount; i++)
        {
            if (Math.Abs(a.Rows[i].Size - b.Rows[i].Size) > snap)
                return 0;
        }

        for (var i = 0; i < a.ColCount; i++)
        {
            if (Math.Abs(a.Cols[i].Size - b.Cols[i].Size) > snap)
                return 0;
        }

        return 1;
    }

    private static double CompareMerges(TableGrid inferred, TableGrid golden)
    {
        var a = NormalizeMerges(inferred.Structure.Merges);
        var b = NormalizeMerges(golden.Structure.Merges);
        return a.SetEquals(b) ? 1 : 0;
    }

    private static HashSet<(int Row, int Col, int RowSpan, int ColSpan)> NormalizeMerges(
        IReadOnlyList<MergeRegion> merges)
    {
        return merges
            .Select(m => (m.TopLeft.Row, m.TopLeft.Col, m.RowSpan, m.ColSpan))
            .ToHashSet();
    }

    private static double CompareTexts(TableGrid inferred, TableGrid golden)
    {
        var total = 0;
        var matched = 0;
        foreach (var entry in golden.Data.Cells)
        {
            var addr = entry.Key;
            if (golden.Structure.IsHidden(addr))
                continue;

            var goldenText = NormalizeText(GridEditor.GetValue(golden, addr).Text);
            if (string.IsNullOrEmpty(goldenText))
                continue;

            total++;
            var inferredText = NormalizeText(GridEditor.GetValue(inferred, golden.Structure.GetAnchorOf(addr)).Text);
            if (goldenText == inferredText)
                matched++;
        }

        return total == 0 ? 1 : (double)matched / total;
    }

    private static string NormalizeText(string? text) =>
        string.Join(" ", (text ?? string.Empty).Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
