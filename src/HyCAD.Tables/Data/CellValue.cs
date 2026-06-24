namespace HyCAD.Tables.Data;

/// <summary>
/// 单元格值（Cell Value）。
/// </summary>
/// <param name="Text">显示文本（公式结果亦缓存于此）。</param>
/// <param name="Kind">值种类。</param>
/// <param name="Formula">公式文本（Kind=Formula 时）。</param>
/// <param name="BindingExpr">绑定表达式（Kind=Bound 时）。</param>
/// <param name="Runs">可选富文本片段。</param>
public sealed record CellValue(
    string Text = "",
    CellValueKind Kind = CellValueKind.Static,
    string? Formula = null,
    string? BindingExpr = null,
    IReadOnlyList<TextRun>? Runs = null)
{
    /// <summary>空值。</summary>
    public static CellValue Empty { get; } = new();
}
