namespace HyCAD.Tables.Structure;

/// <summary>
/// 单元格溢出策略（012 L3 子集；M1 仅 AllowWrap，渲染读入留 M2）。
/// </summary>
public sealed record CellOverflowFlags(bool AllowWrap = false);
