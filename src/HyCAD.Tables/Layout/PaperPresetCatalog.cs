using System;

namespace HyCAD.Tables.Layout;

/// <summary>
/// 纸型 → 默认可用宽（横向、ExcludingMargins，018 §2.2）。
/// </summary>
public static class PaperPresetCatalog
{
    public const double DefaultMarginMm = 10.0;

    /// <summary>Custom 未指定宽时的占位默认（mm）。</summary>
    public const double CustomFallbackWidthMm = 400.0;

    /// <summary>Custom 未指定高时的占位默认（mm）。</summary>
    public const double CustomFallbackHeightMm = 280.0;

    public static double ResolveTargetWidthMm(
        PaperPreset preset,
        double marginMm = DefaultMarginMm,
        PaperOrientation orientation = PaperOrientation.Landscape)
    {
        if (marginMm < 0)
            throw new ArgumentOutOfRangeException(nameof(marginMm));

        var horizontalMarginTotal = marginMm * 2.0;
        return preset switch
        {
            PaperPreset.A4 => (orientation == PaperOrientation.Landscape ? 297.0 : 210.0) - horizontalMarginTotal,
            PaperPreset.A3 => (orientation == PaperOrientation.Landscape ? 420.0 : 297.0) - horizontalMarginTotal,
            PaperPreset.A2 => (orientation == PaperOrientation.Landscape ? 594.0 : 420.0) - horizontalMarginTotal,
            PaperPreset.Custom => CustomFallbackWidthMm,
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
        };
    }

    public static double DefaultTargetWidthMm(PaperPreset preset) =>
        ResolveTargetWidthMm(preset, DefaultMarginMm, PaperOrientation.Landscape);

    /// <summary>纸型 → 默认可用高（横向/纵向、扣除左右边距后的垂直可用区，mm）。</summary>
    public static double ResolveTargetHeightMm(
        PaperPreset preset,
        double marginMm = DefaultMarginMm,
        PaperOrientation orientation = PaperOrientation.Landscape)
    {
        if (marginMm < 0)
            throw new ArgumentOutOfRangeException(nameof(marginMm));

        var verticalMarginTotal = marginMm * 2.0;
        return preset switch
        {
            PaperPreset.A4 => (orientation == PaperOrientation.Landscape ? 210.0 : 297.0) - verticalMarginTotal,
            PaperPreset.A3 => (orientation == PaperOrientation.Landscape ? 297.0 : 420.0) - verticalMarginTotal,
            PaperPreset.A2 => (orientation == PaperOrientation.Landscape ? 420.0 : 594.0) - verticalMarginTotal,
            PaperPreset.Custom => CustomFallbackHeightMm,
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
        };
    }

    public static string GetDisplayName(PaperPreset preset) => preset switch
    {
        PaperPreset.A4 => "A4",
        PaperPreset.A3 => "A3",
        PaperPreset.A2 => "A2",
        PaperPreset.Custom => "自定义",
        _ => preset.ToString(),
    };
}
