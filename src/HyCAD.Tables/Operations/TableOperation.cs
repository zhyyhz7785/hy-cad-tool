using HyCAD.Tables.Data;
using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Operations;

/// <summary>
/// 表格语义操作命令（Table Operation），可记录于 OpLog 并委托 <see cref="GridEditor"/> 执行。
/// </summary>
public abstract record TableOperation
{
    /// <summary>将操作应用到指定表格，返回新状态。</summary>
    public abstract TableGrid Apply(TableGrid grid);
}

/// <summary>在指定索引插入一行。</summary>
public sealed record InsertRowOp(int RowIndex) : TableOperation
{
    /// <inheritdoc />
    public override TableGrid Apply(TableGrid grid) => GridEditor.InsertRow(grid, RowIndex);
}

/// <summary>删除指定行。</summary>
public sealed record DeleteRowOp(int RowIndex) : TableOperation
{
    /// <inheritdoc />
    public override TableGrid Apply(TableGrid grid) => GridEditor.DeleteRow(grid, RowIndex);
}

/// <summary>在指定索引插入一列。</summary>
public sealed record InsertColumnOp(int ColIndex) : TableOperation
{
    /// <inheritdoc />
    public override TableGrid Apply(TableGrid grid) => GridEditor.InsertColumn(grid, ColIndex);
}

/// <summary>删除指定列。</summary>
public sealed record DeleteColumnOp(int ColIndex) : TableOperation
{
    /// <inheritdoc />
    public override TableGrid Apply(TableGrid grid) => GridEditor.DeleteColumn(grid, ColIndex);
}

/// <summary>合并矩形区域。</summary>
public sealed record MergeOp(
    CellAddr TopLeft,
    int RowSpan,
    int ColSpan,
    MergeValuePolicy Policy = MergeValuePolicy.AnchorOnly) : TableOperation
{
    /// <inheritdoc />
    public override TableGrid Apply(TableGrid grid) =>
        GridEditor.Merge(grid, TopLeft, RowSpan, ColSpan, Policy);
}

/// <summary>拆分合并区。</summary>
public sealed record UnmergeOp(CellAddr Anchor) : TableOperation
{
    /// <inheritdoc />
    public override TableGrid Apply(TableGrid grid) => GridEditor.Unmerge(grid, Anchor);
}

/// <summary>在指定格添加斜线分割。</summary>
public sealed record SplitDiagonalOp(
    CellAddr Addr,
    DiagonalDirection Direction,
    IReadOnlyList<SubCell>? Parts = null) : TableOperation
{
    /// <inheritdoc />
    public override TableGrid Apply(TableGrid grid) =>
        GridEditor.SplitDiagonal(grid, Addr, Direction, Parts);
}

/// <summary>移除指定格的斜线分割。</summary>
public sealed record ClearDiagonalOp(CellAddr Addr) : TableOperation
{
    /// <inheritdoc />
    public override TableGrid Apply(TableGrid grid) => GridEditor.ClearDiagonal(grid, Addr);
}

/// <summary>写入单元格值。</summary>
public sealed record SetValueOp(CellAddr Addr, CellValue Value) : TableOperation
{
    /// <inheritdoc />
    public override TableGrid Apply(TableGrid grid) => GridEditor.SetValue(grid, Addr, Value);
}

/// <summary>按 FieldKey 写入值。</summary>
public sealed record SetValueByFieldOp(string FieldKey, CellValue Value) : TableOperation
{
    /// <inheritdoc />
    public override TableGrid Apply(TableGrid grid) =>
        GridEditor.SetValueByField(grid, FieldKey, Value);
}

/// <summary>维护 FieldIndex 绑定。</summary>
public sealed record SetFieldKeyOp(CellAddr Addr, string? FieldKey) : TableOperation
{
    /// <inheritdoc />
    public override TableGrid Apply(TableGrid grid) =>
        GridEditor.SetFieldKey(grid, Addr, FieldKey);
}
