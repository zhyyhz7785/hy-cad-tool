using HyCAD.Tables.Data;
using HyCAD.Tables.Structure;

namespace HyCAD.Tables;

/// <summary>
/// 单张表格（结构 + 数据）。
/// </summary>
public sealed record TableGrid : IDocumentNode
{
    /// <inheritdoc />
    public Guid Id { get; init; }

    /// <inheritdoc />
    public NodeKind Kind => NodeKind.Table;

    /// <summary>结构层（模板）。</summary>
    public GridStructure Structure { get; init; } = null!;

    /// <summary>数据层（填值）。</summary>
    public GridData Data { get; init; } = GridData.Empty;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = EmptyMetadata;

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new Dictionary<string, string>();

    /// <summary>
    /// 创建空表格（等尺寸行列，无合并/无填值）。
    /// </summary>
    /// <param name="rows">行数。</param>
    /// <param name="cols">列数。</param>
    /// <param name="rowHeight">行高（mm）。</param>
    /// <param name="colWidth">列宽（mm）。</param>
    public static TableGrid CreateEmpty(
        int rows,
        int cols,
        double rowHeight = TableConstants.DefaultRowHeight,
        double colWidth = TableConstants.DefaultColWidth)
    {
        if (rows < 1)
            throw new ArgumentOutOfRangeException(nameof(rows));
        if (cols < 1)
            throw new ArgumentOutOfRangeException(nameof(cols));

        var topology = GridTopology.CreateUniform(rows, cols, rowHeight, colWidth);
        return new TableGrid
        {
            Id = Guid.NewGuid(),
            Structure = GridStructure.CreateEmpty(topology),
            Data = GridData.Empty
        };
    }

    /// <summary>深拷贝结构/数据并分配新 Id（AC7 再渲染）。</summary>
    public TableGrid CloneWithNewId() =>
        new()
        {
            Id = Guid.NewGuid(),
            Structure = Structure,
            Data = Data,
            Metadata = Metadata,
        };

    /// <summary>从已有结构层构造表格（测试 / 诊断）。</summary>
    public static TableGrid FromStructure(GridStructure structure, GridData? data = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Structure = structure,
            Data = data ?? GridData.Empty,
        };
}
