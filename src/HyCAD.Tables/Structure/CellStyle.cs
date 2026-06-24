namespace HyCAD.Tables.Structure;

/// <summary>
/// 单元格样式（Cell Style），平台无关，落端时翻译。
/// </summary>
/// <param name="Orientation">文本方向。</param>
/// <param name="HAlign">水平对齐。</param>
/// <param name="VAlign">垂直对齐。</param>
/// <param name="TextHeight">字高（mm）。</param>
/// <param name="FontKey">抽象字体键。</param>
/// <param name="Borders">边框。</param>
/// <param name="BackColor">背景色（CSS 色值或命名色），可空。</param>
public sealed record CellStyle(
    TextOrientation Orientation = TextOrientation.Horizontal,
    TextAlign HAlign = TextAlign.Start,
    TextAlign VAlign = TextAlign.Center,
    double TextHeight = 3.5,
    string FontKey = "default",
    BorderSet? Borders = null,
    string? BackColor = null);
