namespace HyCAD.Tables;

/// <summary>
/// 表格 mm 口径单一真相源（与 Web <c>mm-display.ts</c> 同名同值）。
/// 业务长度一律 mm；px/pt 仅在 Adapter 边界换算。详见 doc/new/12。
/// </summary>
public static class TableMmDefaults
{
    /// <summary>CSS DIP 像素密度（96px = 1in = 25.4mm）。</summary>
    public const double DisplayPxPerMm = 96.0 / 25.4;

    /// <summary>纸面 mm → Univer 字号 pt。</summary>
    public const double MmToPoint = 72.0 / 25.4;

    /// <summary>Univer 字号 pt → 纸面 mm。</summary>
    public const double PointToMm = 25.4 / 72.0;

    /// <summary>按纸面推导行列数的种子行高（mm）。</summary>
    public const double SeedRowHeightMm = 6.4;

    /// <summary>按纸面推导行列数的种子列宽（mm）。</summary>
    public const double SeedColWidthMm = 23.3;

    /// <summary>缺省/回退行高（mm）。</summary>
    public const double FallbackRowHeightMm = 10.0;

    /// <summary>缺省/回退列宽（mm）。</summary>
    public const double FallbackColWidthMm = 25.0;

    /// <summary>缺省字高（mm）。</summary>
    public const double TextHeightMm = 3.5;

    /// <summary>边框预设线宽（mm）。</summary>
    public const double BorderWidthMm = 0.35;

    /// <summary>默认页边距（mm）。</summary>
    public const double MarginMm = 30.0;

    /// <summary>最小有效尺寸（mm）。</summary>
    public const double MinDimensionMm = 0.01;

    /// <summary>CAD 标准字高预设（mm）。</summary>
    public static readonly double[] CadTextHeightsMm = { 2.5, 3.5, 5, 7, 10, 14, 20 };
}
