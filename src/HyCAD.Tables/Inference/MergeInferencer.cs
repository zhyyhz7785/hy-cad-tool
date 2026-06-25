using HyCAD.Tables.Layout;
using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Inference;

internal static class MergeInferencer
{
    internal sealed class MergeResult
    {
        public MergeResult(IReadOnlyList<MergeRegion> merges, IReadOnlyList<string> messages)
        {
            Merges = merges;
            Messages = messages;
        }

        public IReadOnlyList<MergeRegion> Merges { get; }

        public IReadOnlyList<string> Messages { get; }
    }

    internal static MergeResult? Infer(
        GridLineSet grid,
        IReadOnlyList<LineSegment2d> segments,
        TableInferOptions options)
    {
        var rows = grid.RowCount;
        var cols = grid.ColCount;
        var xs = grid.XBoundaries;
        var ys = grid.YBoundaries;
        var parent = new int[rows * cols];
        for (var i = 0; i < parent.Length; i++)
            parent[i] = i;

        var messages = new List<string>();

        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                if (c + 1 < cols)
                {
                    var x = xs[c + 1];
                    var yLow = ys[r + 1];
                    var yHigh = ys[r];
                    if (!GridLineExtractor.HasEdgeCoverage(
                            segments,
                            GridLineOrientation.Vertical,
                            x,
                            yLow,
                            yHigh,
                            options,
                            isOuter: false))
                    {
                        Union(parent, Index(r, c, cols), Index(r, c + 1, cols));
                    }
                }

                if (r + 1 < rows)
                {
                    var y = ys[r + 1];
                    var xLeft = xs[c];
                    var xRight = xs[c + 1];
                    if (!GridLineExtractor.HasEdgeCoverage(
                            segments,
                            GridLineOrientation.Horizontal,
                            y,
                            xLeft,
                            xRight,
                            options,
                            isOuter: false))
                    {
                        Union(parent, Index(r, c, cols), Index(r + 1, c, cols));
                    }
                }
            }
        }

        var groups = new Dictionary<int, List<CellAddr>>();
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var root = Find(parent, Index(r, c, cols));
                if (!groups.TryGetValue(root, out var list))
                {
                    list = new List<CellAddr>();
                    groups[root] = list;
                }

                list.Add(new CellAddr(r, c));
            }
        }

        var merges = new List<MergeRegion>();
        foreach (var cells in groups.Values)
        {
            if (cells.Count == 1)
                continue;

            var minRow = cells.Min(a => a.Row);
            var maxRow = cells.Max(a => a.Row);
            var minCol = cells.Min(a => a.Col);
            var maxCol = cells.Max(a => a.Col);
            var expected = (maxRow - minRow + 1) * (maxCol - minCol + 1);
            if (cells.Count != expected)
            {
                messages.Add($"合并块非矩形：Anchor 候选 ({minRow},{minCol})，格数={cells.Count}。");
                return null;
            }

            var rowSpan = maxRow - minRow + 1;
            var colSpan = maxCol - minCol + 1;
            merges.Add(new MergeRegion(new CellAddr(minRow, minCol), rowSpan, colSpan));
        }

        return new MergeResult(merges, messages);
    }

    private static int Index(int row, int col, int cols) => row * cols + col;

    private static int Find(int[] parent, int x)
    {
        while (parent[x] != x)
        {
            parent[x] = Find(parent, parent[x]);
            x = parent[x];
        }

        return x;
    }

    private static void Union(int[] parent, int a, int b)
    {
        var ra = Find(parent, a);
        var rb = Find(parent, b);
        if (ra != rb)
            parent[rb] = ra;
    }
}
