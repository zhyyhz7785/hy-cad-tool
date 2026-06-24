namespace HyCAD.Tables.Structure;

/// <summary>
/// 单元格内斜线分割（Diagonal Split）。
/// </summary>
/// <param name="Direction">斜线方向。</param>
/// <param name="Parts">子格列表（MVP 通常为 2 分）。</param>
public sealed record DiagonalSplit(
    DiagonalDirection Direction,
    IReadOnlyList<SubCell> Parts);
