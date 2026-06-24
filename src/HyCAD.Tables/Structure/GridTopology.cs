namespace HyCAD.Tables.Structure;

/// <summary>
/// 网格拓扑（行列轨道、方向、默认边框）。
/// </summary>
public sealed record GridTopology(
    IReadOnlyList<GridTrack> Rows,
    IReadOnlyList<GridTrack> Cols,
    GrowDirection Direction = GrowDirection.Down,
    BorderSet DefaultBorder = null!)
{
    /// <summary>行数。</summary>
    public int RowCount => Rows.Count;

    /// <summary>列数。</summary>
    public int ColCount => Cols.Count;

    /// <summary>
    /// 创建等尺寸均匀网格拓扑。
    /// </summary>
    /// <param name="rows">行数。</param>
    /// <param name="cols">列数。</param>
    /// <param name="rowHeight">行高（mm）。</param>
    /// <param name="colWidth">列宽（mm）。</param>
    public static GridTopology CreateUniform(
        int rows,
        int cols,
        double rowHeight,
        double colWidth)
    {
        if (rows < 0)
            throw new ArgumentOutOfRangeException(nameof(rows));
        if (cols < 0)
            throw new ArgumentOutOfRangeException(nameof(cols));

        var rowTracks = new GridTrack[rows];
        for (var i = 0; i < rows; i++)
            rowTracks[i] = new GridTrack(rowHeight);

        var colTracks = new GridTrack[cols];
        for (var i = 0; i < cols; i++)
            colTracks[i] = new GridTrack(colWidth);

        return new GridTopology(rowTracks, colTracks, GrowDirection.Down, BorderSet.None);
    }

    /// <summary>在指定索引插入一行轨道（继承相邻行尺寸）。</summary>
    public GridTopology WithRowInserted(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex > RowCount)
            throw new ArgumentOutOfRangeException(nameof(rowIndex));

        var rows = new List<GridTrack>(Rows.Count + 1);
        for (var i = 0; i < RowCount; i++)
        {
            if (i == rowIndex)
                rows.Add(InheritRowTrack(rowIndex));
            rows.Add(Rows[i]);
        }

        if (rowIndex == RowCount)
            rows.Add(InheritRowTrack(rowIndex));

        return this with { Rows = rows };
    }

    /// <summary>移除指定索引的行轨道。</summary>
    public GridTopology WithRowRemoved(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= RowCount)
            throw new ArgumentOutOfRangeException(nameof(rowIndex));

        var rows = new List<GridTrack>(RowCount - 1);
        for (var i = 0; i < RowCount; i++)
        {
            if (i != rowIndex)
                rows.Add(Rows[i]);
        }

        return this with { Rows = rows };
    }

    /// <summary>在指定索引插入一列轨道（继承相邻列尺寸）。</summary>
    public GridTopology WithColumnInserted(int colIndex)
    {
        if (colIndex < 0 || colIndex > ColCount)
            throw new ArgumentOutOfRangeException(nameof(colIndex));

        var cols = new List<GridTrack>(Cols.Count + 1);
        for (var i = 0; i < ColCount; i++)
        {
            if (i == colIndex)
                cols.Add(InheritColTrack(colIndex));
            cols.Add(Cols[i]);
        }

        if (colIndex == ColCount)
            cols.Add(InheritColTrack(colIndex));

        return this with { Cols = cols };
    }

    /// <summary>移除指定索引的列轨道。</summary>
    public GridTopology WithColumnRemoved(int colIndex)
    {
        if (colIndex < 0 || colIndex >= ColCount)
            throw new ArgumentOutOfRangeException(nameof(colIndex));

        var cols = new List<GridTrack>(ColCount - 1);
        for (var i = 0; i < ColCount; i++)
        {
            if (i != colIndex)
                cols.Add(Cols[i]);
        }

        return this with { Cols = cols };
    }

    private GridTrack InheritRowTrack(int insertIndex)
    {
        if (RowCount == 0)
            return new GridTrack(TableConstants.DefaultRowHeight);

        var sourceIndex = insertIndex <= 0 ? 0 : Math.Min(insertIndex - 1, RowCount - 1);
        return Rows[sourceIndex];
    }

    private GridTrack InheritColTrack(int insertIndex)
    {
        if (ColCount == 0)
            return new GridTrack(TableConstants.DefaultColWidth);

        var sourceIndex = insertIndex <= 0 ? 0 : Math.Min(insertIndex - 1, ColCount - 1);
        return Cols[sourceIndex];
    }
}
