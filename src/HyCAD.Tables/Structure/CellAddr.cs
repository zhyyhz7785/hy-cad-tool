namespace HyCAD.Tables.Structure;

/// <summary>
/// 单元格地址（Cell Address），表格字典主键。
/// </summary>
/// <param name="Row">行索引（0-based）。</param>
/// <param name="Col">列索引（0-based）。</param>
public readonly record struct CellAddr(int Row, int Col);
