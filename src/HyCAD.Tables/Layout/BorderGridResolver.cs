using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Layout;

/// <summary>
/// 按 HTML border-collapse 语义解析网格边框线段（平台无关，AC6）。
/// </summary>
public static class BorderGridResolver
{
    public static IEnumerable<BorderGridSegment> Resolve(
        TableLayout layout,
        double fallbackWidthMm = 0.35,
        bool includeOuterEdges = true)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        var structure = layout.Grid.Structure;
        var topology = structure.Topology;
        var colBounds = layout.ColumnBoundariesX;
        var rowBounds = layout.RowBoundariesY;

        for (var c = includeOuterEdges ? 0 : 1; c <= (includeOuterEdges ? topology.ColCount : topology.ColCount - 1); c++)
        {
            if (c > 0 && c < topology.ColCount)
            {
                for (var r = 0; r < topology.RowCount; r++)
                {
                    if (IsInternalVerticalEdgeSkipped(structure, r, c))
                        continue;

                    var leftWidth = GetEdgeWidth(structure, new CellAddr(r, c - 1), BorderEdge.Right, fallbackWidthMm);
                    var rightWidth = GetEdgeWidth(structure, new CellAddr(r, c), BorderEdge.Left, fallbackWidthMm);
                    var width = Math.Max(leftWidth, rightWidth);
                    yield return VerticalSegment(colBounds[c], rowBounds[r + 1], rowBounds[r], width);
                }
            }
            else if (c == 0)
            {
                for (var r = 0; r < topology.RowCount; r++)
                {
                    var width = GetEdgeWidth(structure, new CellAddr(r, 0), BorderEdge.Left, fallbackWidthMm);
                    yield return VerticalSegment(colBounds[0], rowBounds[r + 1], rowBounds[r], width);
                }
            }
            else
            {
                var lastCol = topology.ColCount - 1;
                for (var r = 0; r < topology.RowCount; r++)
                {
                    var width = GetEdgeWidth(structure, new CellAddr(r, lastCol), BorderEdge.Right, fallbackWidthMm);
                    yield return VerticalSegment(colBounds[topology.ColCount], rowBounds[r + 1], rowBounds[r], width);
                }
            }
        }

        for (var r = includeOuterEdges ? 0 : 1; r <= (includeOuterEdges ? topology.RowCount : topology.RowCount - 1); r++)
        {
            if (r > 0 && r < topology.RowCount)
            {
                for (var c = 0; c < topology.ColCount; c++)
                {
                    if (IsInternalHorizontalEdgeSkipped(structure, r, c))
                        continue;

                    var topWidth = GetEdgeWidth(structure, new CellAddr(r - 1, c), BorderEdge.Bottom, fallbackWidthMm);
                    var bottomWidth = GetEdgeWidth(structure, new CellAddr(r, c), BorderEdge.Top, fallbackWidthMm);
                    var width = Math.Max(topWidth, bottomWidth);
                    yield return HorizontalSegment(rowBounds[r], colBounds[c], colBounds[c + 1], width);
                }
            }
            else if (r == 0)
            {
                for (var c = 0; c < topology.ColCount; c++)
                {
                    var width = GetEdgeWidth(structure, new CellAddr(0, c), BorderEdge.Top, fallbackWidthMm);
                    yield return HorizontalSegment(rowBounds[0], colBounds[c], colBounds[c + 1], width);
                }
            }
            else
            {
                var lastRow = topology.RowCount - 1;
                for (var c = 0; c < topology.ColCount; c++)
                {
                    var width = GetEdgeWidth(structure, new CellAddr(lastRow, c), BorderEdge.Bottom, fallbackWidthMm);
                    yield return HorizontalSegment(rowBounds[topology.RowCount], colBounds[c], colBounds[c + 1], width);
                }
            }
        }
    }

    private enum BorderEdge
    {
        Top,
        Right,
        Bottom,
        Left,
    }

    private static bool IsInternalVerticalEdgeSkipped(GridStructure structure, int row, int colBoundary)
    {
        var leftAnchor = structure.GetAnchorOf(new CellAddr(row, colBoundary - 1));
        var rightAnchor = structure.GetAnchorOf(new CellAddr(row, colBoundary));
        return leftAnchor == rightAnchor;
    }

    private static bool IsInternalHorizontalEdgeSkipped(GridStructure structure, int rowBoundary, int col)
    {
        var topAnchor = structure.GetAnchorOf(new CellAddr(rowBoundary - 1, col));
        var bottomAnchor = structure.GetAnchorOf(new CellAddr(rowBoundary, col));
        return topAnchor == bottomAnchor;
    }

    private static double GetEdgeWidth(
        GridStructure structure,
        CellAddr addr,
        BorderEdge edge,
        double fallbackWidthMm)
    {
        var anchor = structure.GetAnchorOf(addr);
        structure.Styles.TryGetValue(anchor, out var style);

        var borders = style?.Borders ?? structure.Topology.DefaultBorder ?? BorderSet.None;
        var width = edge switch
        {
            BorderEdge.Top => borders.Top,
            BorderEdge.Right => borders.Right,
            BorderEdge.Bottom => borders.Bottom,
            _ => borders.Left,
        };

        return width > 0 ? width : fallbackWidthMm;
    }

    private static BorderGridSegment VerticalSegment(double x, double yLow, double yHigh, double widthMm)
    {
        var start = Math.Min(yLow, yHigh);
        var end = Math.Max(yLow, yHigh);
        return new BorderGridSegment(GridLineOrientation.Vertical, x, start, end, widthMm);
    }

    private static BorderGridSegment HorizontalSegment(double y, double xLeft, double xRight, double widthMm)
    {
        var start = Math.Min(xLeft, xRight);
        var end = Math.Max(xLeft, xRight);
        return new BorderGridSegment(GridLineOrientation.Horizontal, y, start, end, widthMm);
    }
}
