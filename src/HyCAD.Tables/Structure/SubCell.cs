using HyCAD.Tables.Data;

namespace HyCAD.Tables.Structure;

/// <summary>
/// 斜线子格（Sub Cell），隶属同一 BaseCell。
/// </summary>
/// <param name="Value">子格值。</param>
/// <param name="FieldKey">可选语义字段键。</param>
/// <param name="TextAnchor">文字锚点（0..1 相对父格）。</param>
/// <param name="Align">文字对齐。</param>
public sealed record SubCell(
    CellValue Value,
    string? FieldKey = null,
    NormalizedPoint TextAnchor = default,
    TextAlign Align = TextAlign.Center);
