using HyCAD.Tables.Data;
using HyCAD.Tables.Layout;
using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Inference;

internal static class TextAssigner
{
    internal sealed class AssignResult
    {
        public AssignResult(
            GridData data,
            IReadOnlyList<CellAddr> uncertainCells,
            IReadOnlyList<string> messages)
        {
            Data = data;
            UncertainCells = uncertainCells;
            Messages = messages;
        }

        public GridData Data { get; }

        public IReadOnlyList<CellAddr> UncertainCells { get; }

        public IReadOnlyList<string> Messages { get; }
    }

    internal static AssignResult Assign(
        GridLineSet grid,
        IReadOnlyList<MergeRegion> merges,
        IReadOnlyList<TextBox2d> texts,
        TableInferOptions options)
    {
        var rows = grid.RowCount;
        var cols = grid.ColCount;
        var xs = grid.XBoundaries;
        var ys = grid.YBoundaries;
        var structure = BuildStructure(grid, merges);
        var anchorBuckets = new Dictionary<CellAddr, List<(string Text, double Y, double X)>>();
        var uncertain = new HashSet<CellAddr>();
        var messages = new List<string>();

        foreach (var text in texts ?? Array.Empty<TextBox2d>())
        {
            var center = text.Bounds.Center;
            if (!TryResolveAnchor(
                    structure,
                    xs,
                    ys,
                    center.X,
                    center.Y,
                    options,
                    out var anchor))
            {
                messages.Add($"文字未归属任何格：\"{text.Content}\"。");
                continue;
            }

            if (!anchorBuckets.TryGetValue(anchor, out var bucket))
            {
                bucket = new List<(string, double, double)>();
                anchorBuckets[anchor] = bucket;
            }

            bucket.Add((text.Content ?? string.Empty, center.Y, center.X));
        }

        foreach (var pair in anchorBuckets.Where(p => p.Value.Count > 1))
            uncertain.Add(pair.Key);

        var cells = new Dictionary<CellAddr, CellValue>();
        foreach (var pair in anchorBuckets)
        {
            var combined = string.Join(
                " ",
                pair.Value
                    .OrderByDescending(t => t.Y)
                    .ThenBy(t => t.X)
                    .Select(t => t.Text));
            cells[pair.Key] = new CellValue(combined);
        }

        return new AssignResult(new GridData(cells), uncertain.ToList(), messages);
    }

    private static GridStructure BuildStructure(GridLineSet grid, IReadOnlyList<MergeRegion> merges)
    {
        var rowTracks = new List<GridTrack>();
        for (var r = 0; r < grid.RowCount; r++)
            rowTracks.Add(new GridTrack(grid.YBoundaries[r] - grid.YBoundaries[r + 1]));

        var colTracks = new List<GridTrack>();
        for (var c = 0; c < grid.ColCount; c++)
            colTracks.Add(new GridTrack(grid.XBoundaries[c + 1] - grid.XBoundaries[c]));

        var topology = new GridTopology(rowTracks, colTracks, GrowDirection.Down, BorderSet.None);
        return new GridStructure(
            topology,
            merges,
            new Dictionary<CellAddr, DiagonalSplit>(),
            new Dictionary<CellAddr, CellStyle>(),
            new Dictionary<CellAddr, CellRole>(),
            new Dictionary<string, CellAddr>());
    }

    private static bool TryResolveAnchor(
        GridStructure structure,
        IReadOnlyList<double> xs,
        IReadOnlyList<double> ys,
        double x,
        double y,
        TableInferOptions options,
        out CellAddr anchor)
    {
        if (TryFindCell(xs, ys, x, y, out var row, out var col))
        {
            anchor = structure.GetAnchorOf(new CellAddr(row, col));
            return true;
        }

        var margin = options.TextInCellMarginMm;
        if (TryFindCell(xs, ys, x + margin, y, out row, out col)
            || TryFindCell(xs, ys, x - margin, y, out row, out col)
            || TryFindCell(xs, ys, x, y + margin, out row, out col)
            || TryFindCell(xs, ys, x, y - margin, out row, out col))
        {
            anchor = structure.GetAnchorOf(new CellAddr(row, col));
            return true;
        }

        anchor = default;
        return false;
    }

    private static bool TryFindCell(
        IReadOnlyList<double> xs,
        IReadOnlyList<double> ys,
        double x,
        double y,
        out int row,
        out int col)
    {
        col = -1;
        for (var c = 0; c < xs.Count - 1; c++)
        {
            if (x >= xs[c] && x <= xs[c + 1])
            {
                col = c;
                break;
            }
        }

        row = -1;
        for (var r = 0; r < ys.Count - 1; r++)
        {
            var top = ys[r];
            var bottom = ys[r + 1];
            if (y <= top && y >= bottom)
            {
                row = r;
                break;
            }
        }

        return row >= 0 && col >= 0;
    }
}
