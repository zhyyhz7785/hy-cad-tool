namespace HyCAD.Tables.Adapters;

/// <summary>
/// HTML 表格导出选项。
/// </summary>
public sealed class HtmlTableExportOptions
{
    /// <summary>默认选项。</summary>
    public static HtmlTableExportOptions Default { get; } = new();

    /// <summary>HTML 文档标题（&lt;title&gt;）。</summary>
    public string? Title { get; init; }

    /// <summary>是否输出完整 HTML 文档（含 head/style）；false 时仅输出 &lt;table&gt; 片段。</summary>
    public bool FullDocument { get; init; } = true;

    /// <summary>是否内联 CSS（FullDocument=true 时在 &lt;style&gt; 内）。</summary>
    public bool InlineCss { get; init; } = true;

    /// <summary>尺寸单位使用 mm（true）或 px（false，按 <see cref="MmToPxRatio"/> 换算）。</summary>
    public bool UseMillimeters { get; init; } = true;

    /// <summary>mm → px 换算比（与 <see cref="TableMmDefaults.DisplayPxPerMm"/> 同源）。</summary>
    public double MmToPxRatio { get; init; } = TableMmDefaults.DisplayPxPerMm;

    /// <summary>默认单元格边框线宽（mm），当格样式与拓扑默认边框均为 0 时使用。</summary>
    public double DefaultBorderWidthMm { get; init; } = 0.15;

    /// <summary>默认字体族。</summary>
    public string FontFamily { get; init; } = "SimSun, \"Songti SC\", serif";
}
