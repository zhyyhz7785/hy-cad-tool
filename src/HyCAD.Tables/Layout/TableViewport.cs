namespace HyCAD.Tables.Layout;

/// <summary>
/// 表格排版视口（012 L0）；M0 仅会话级，M3 接 LayoutEngine。
/// </summary>
public sealed class TableViewport
{
    public PaperPreset PaperPreset { get; set; } = PaperPreset.A3;

    /// <summary>目标总宽（模型空间 mm，1:1）。</summary>
    public double TargetWidthMm { get; set; } = PaperPresetCatalog.DefaultTargetWidthMm(PaperPreset.A3);

    /// <summary>左右边距合计的一半用于推导时的单边距（mm）。</summary>
    public double MarginMm { get; set; } = PaperPresetCatalog.DefaultMarginMm;

    /// <summary>纸张方向；影响目标可用宽推导。</summary>
    public PaperOrientation Orientation { get; set; } = PaperOrientation.Landscape;

    public static TableViewport CreateDefault() => new TableViewport();
}
