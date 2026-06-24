using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Data;

/// <summary>
/// 表格数据层（填值，Grid Data）。
/// </summary>
public sealed record GridData(IReadOnlyDictionary<CellAddr, CellValue> Cells)
{
    /// <summary>空数据层。</summary>
    public static GridData Empty { get; } = new(new Dictionary<CellAddr, CellValue>());
}
