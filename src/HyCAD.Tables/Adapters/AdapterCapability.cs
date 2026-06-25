namespace HyCAD.Tables.Adapters;

/// <summary>
/// 适配器能力矩阵（005 §6.2）。
/// </summary>
public sealed class AdapterCapability
{
    /// <summary>规整网格。</summary>
    public AdapterFeatureLevel Grid { get; init; } = AdapterFeatureLevel.Full;

    /// <summary>矩形合并（rowspan/colspan）。</summary>
    public AdapterFeatureLevel RectangularMerge { get; init; } = AdapterFeatureLevel.Full;

    /// <summary>斜线分割。</summary>
    public AdapterFeatureLevel DiagonalSplit { get; init; } = AdapterFeatureLevel.NotSupported;

    /// <summary>竖排堆字。</summary>
    public AdapterFeatureLevel VerticalStacked { get; init; } = AdapterFeatureLevel.Full;

    /// <summary>公式求值。</summary>
    public AdapterFeatureLevel Formula { get; init; } = AdapterFeatureLevel.NotSupported;

    /// <summary>富样式/边框。</summary>
    public AdapterFeatureLevel RichStyles { get; init; } = AdapterFeatureLevel.Degraded;

    /// <summary>HTML 导出默认能力。</summary>
    public static AdapterCapability HtmlDefaults { get; } = new()
    {
        Grid = AdapterFeatureLevel.Full,
        RectangularMerge = AdapterFeatureLevel.Full,
        DiagonalSplit = AdapterFeatureLevel.Degraded,
        VerticalStacked = AdapterFeatureLevel.Full,
        Formula = AdapterFeatureLevel.Degraded,
        RichStyles = AdapterFeatureLevel.Degraded
    };
}
