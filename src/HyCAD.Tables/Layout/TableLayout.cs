using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Layout;

/// <summary>
/// 由 <see cref="TableGrid"/> + 基点构建的 mm 布局视图（平台无关）。
/// </summary>
public sealed class TableLayout
{
    private readonly double[] _columnLefts;
    private readonly double[] _rowTops;
    private readonly double[] _columnBoundariesX;
    private readonly double[] _rowBoundariesY;

    private TableLayout(TableGrid grid, LayoutPoint origin, GrowDirection direction, double scale)
    {
        Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        Origin = origin;
        Direction = direction;
        Scale = scale;

        var topology = grid.Structure.Topology;
        _columnLefts = BuildColumnLefts(topology, origin.X, scale);
        _rowTops = BuildRowTops(topology, origin.Y, direction, scale);

        TotalWidth = SumTrackSizes(topology.Cols, scale);
        TotalHeight = SumTrackSizes(topology.Rows, scale);
        TableBounds = new LayoutRect(
            origin.X,
            origin.Y,
            origin.X + TotalWidth,
            GetRowTop(topology.RowCount));

        _columnBoundariesX = BuildColumnBoundaries(_columnLefts, Origin.X + TotalWidth);
        _rowBoundariesY = BuildRowBoundaries(_rowTops, GetRowTop(topology.RowCount));
    }

    /// <summary>源表格。</summary>
    public TableGrid Grid { get; }

    /// <summary>表左上角原点（mm）。</summary>
    public LayoutPoint Origin { get; }

    /// <summary>生长方向。</summary>
    public GrowDirection Direction { get; }

    /// <summary>几何放大比例（口径 B：纸面 mm × Scale = 模型空间 mm）。</summary>
    public double Scale { get; }

    /// <summary>表总宽（mm）。</summary>
    public double TotalWidth { get; }

    /// <summary>表总高（mm）。</summary>
    public double TotalHeight { get; }

    /// <summary>整张表外包络。</summary>
    public LayoutRect TableBounds { get; }

    /// <summary>
    /// 创建布局视图。
    /// </summary>
    /// <param name="grid">表格。</param>
    /// <param name="originX">原点 X（mm）。</param>
    /// <param name="originY">原点 Y（mm）。</param>
    /// <param name="direction">生长方向；AC1 仅支持 <see cref="GrowDirection.Down"/>。</param>
    /// <param name="scale">几何放大比例（口径 B；默认 1.0 即纸面 mm）。</param>
    public static TableLayout Create(
        TableGrid grid,
        double originX,
        double originY,
        GrowDirection direction = GrowDirection.Down,
        double scale = 1.0)
    {
        if (direction != GrowDirection.Down)
            throw new NotSupportedException("TableLayout AC1 仅支持 GrowDirection.Down；Up 见 008 V2。");

        if (scale <= 0)
            scale = 1.0;

        return new TableLayout(grid, new LayoutPoint(originX, originY), direction, scale);
    }

    /// <summary>第 col 列左边缘 X（mm）。</summary>
    public double GetColumnLeft(int col)
    {
        ValidateColumnIndex(col);
        return _columnLefts[col];
    }

    /// <summary>第 row 行顶边 Y（mm，Down 方向）。</summary>
    public double GetRowTop(int row)
    {
        ValidateRowIndex(row);
        return _rowTops[row];
    }

    /// <summary>列边界 X 坐标（长度 ColCount+1）。</summary>
    public IReadOnlyList<double> ColumnBoundariesX => _columnBoundariesX;

    /// <summary>行边界 Y 坐标（长度 RowCount+1，Down 方向单调递减）。</summary>
    public IReadOnlyList<double> RowBoundariesY => _rowBoundariesY;

    /// <summary>可见格枚举（行优先，与 HtmlTableAdapter 相同）。</summary>
    public IEnumerable<VisibleCellLayout> EnumerateVisibleCells()
    {
        var structure = Grid.Structure;
        var topology = structure.Topology;

        for (var row = 0; row < topology.RowCount; row++)
        {
            for (var col = 0; col < topology.ColCount; col++)
            {
                var addr = new CellAddr(row, col);
                if (structure.IsHidden(addr))
                    continue;

                if (!TryBuildVisibleCell(structure, addr, out var visible))
                    continue;

                yield return visible;
            }
        }
    }

    /// <summary>单址查询；hidden 格返回 false。</summary>
    public bool TryGetCellRect(CellAddr addr, out LayoutRect rect, out MergeRegion? merge)
    {
        var structure = Grid.Structure;
        if (structure.IsHidden(addr))
        {
            rect = default;
            merge = null;
            return false;
        }

        if (!TryBuildCellRect(structure, addr, out rect, out merge))
        {
            rect = default;
            merge = null;
            return false;
        }

        return true;
    }

    private bool TryBuildVisibleCell(GridStructure structure, CellAddr addr, out VisibleCellLayout visible)
    {
        if (!TryBuildCellRect(structure, addr, out var rect, out var merge))
        {
            visible = default;
            return false;
        }

        var rowSpan = merge?.RowSpan ?? 1;
        var colSpan = merge?.ColSpan ?? 1;
        var isAnchor = merge == null || merge.Anchor == addr;

        visible = new VisibleCellLayout(addr, rect, isAnchor, rowSpan, colSpan, merge);
        return true;
    }

    private bool TryBuildCellRect(
        GridStructure structure,
        CellAddr addr,
        out LayoutRect rect,
        out MergeRegion? merge)
    {
        var topology = structure.Topology;
        if (addr.Row < 0 || addr.Row >= topology.RowCount
            || addr.Col < 0 || addr.Col >= topology.ColCount)
        {
            rect = default;
            merge = null;
            return false;
        }

        structure.TryGetMergeAt(addr, out merge);

        var anchor = merge?.Anchor ?? addr;
        var rowSpan = merge?.RowSpan ?? 1;
        var colSpan = merge?.ColSpan ?? 1;

        var left = GetColumnLeft(anchor.Col);
        var top = GetRowTop(anchor.Row);
        var right = GetColumnLeft(anchor.Col + colSpan);
        var bottom = GetRowTop(anchor.Row + rowSpan);

        rect = new LayoutRect(left, top, right, bottom);
        return true;
    }

    private void ValidateColumnIndex(int col)
    {
        var colCount = Grid.Structure.Topology.ColCount;
        if (col < 0 || col > colCount)
            throw new ArgumentOutOfRangeException(nameof(col));
    }

    private void ValidateRowIndex(int row)
    {
        var rowCount = Grid.Structure.Topology.RowCount;
        if (row < 0 || row > rowCount)
            throw new ArgumentOutOfRangeException(nameof(row));
    }

    private static double[] BuildColumnLefts(GridTopology topology, double originX, double scale)
    {
        var result = new double[topology.ColCount + 1];
        var x = originX;
        for (var col = 0; col < topology.ColCount; col++)
        {
            result[col] = x;
            x += topology.Cols[col].Size * scale;
        }

        result[topology.ColCount] = x;
        return result;
    }

    private static double[] BuildRowTops(GridTopology topology, double originY, GrowDirection direction, double scale)
    {
        var result = new double[topology.RowCount + 1];
        if (direction == GrowDirection.Down)
        {
            var y = originY;
            for (var row = 0; row < topology.RowCount; row++)
            {
                result[row] = y;
                y -= topology.Rows[row].Size * scale;
            }

            result[topology.RowCount] = y;
            return result;
        }

        throw new NotSupportedException("TableLayout AC1 仅支持 GrowDirection.Down。");
    }

    private static double SumTrackSizes(IReadOnlyList<GridTrack> tracks, double scale)
    {
        var sum = 0.0;
        for (var i = 0; i < tracks.Count; i++)
            sum += tracks[i].Size * scale;
        return sum;
    }

    private static double[] BuildColumnBoundaries(double[] columnLefts, double rightEdge)
    {
        var result = new double[columnLefts.Length];
        Array.Copy(columnLefts, result, columnLefts.Length);
        result[result.Length - 1] = rightEdge;
        return result;
    }

    private static double[] BuildRowBoundaries(double[] rowTops, double bottomEdge)
    {
        var result = new double[rowTops.Length];
        Array.Copy(rowTops, result, rowTops.Length);
        result[result.Length - 1] = bottomEdge;
        return result;
    }
}
