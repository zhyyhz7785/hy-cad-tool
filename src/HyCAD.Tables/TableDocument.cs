namespace HyCAD.Tables;

/// <summary>
/// 表格文档（可含多表）。
/// </summary>
public sealed record TableDocument : IDocumentNode
{
    /// <inheritdoc />
    public Guid Id { get; init; }

    /// <inheritdoc />
    public NodeKind Kind => NodeKind.Table;

    /// <summary>文档名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>文档内所有表格。</summary>
    public IReadOnlyList<TableGrid> Tables { get; init; } = Array.Empty<TableGrid>();

    /// <summary>来源信息。</summary>
    public TableSourceInfo Source { get; init; } = new(TableSourceKind.Unknown);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = EmptyMetadata;

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new Dictionary<string, string>();

    /// <summary>
    /// 创建仅含一张空表的文档。
    /// </summary>
    public static TableDocument CreateWithEmptyTable(
        string name,
        int rows,
        int cols,
        double rowHeight = TableConstants.DefaultRowHeight,
        double colWidth = TableConstants.DefaultColWidth)
    {
        var grid = TableGrid.CreateEmpty(rows, cols, rowHeight, colWidth);
        return new TableDocument
        {
            Id = Guid.NewGuid(),
            Name = name,
            Tables = new[] { grid }
        };
    }
}
