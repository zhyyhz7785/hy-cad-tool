namespace HyCAD.Tables.Structure;

/// <summary>
/// 单元格四边边框（Border Set）。
/// </summary>
/// <param name="Top">上边线宽（mm）。</param>
/// <param name="Right">右边线宽（mm）。</param>
/// <param name="Bottom">下边线宽（mm）。</param>
/// <param name="Left">左边线宽（mm）。</param>
public sealed record BorderSet(
    double Top = 0.0,
    double Right = 0.0,
    double Bottom = 0.0,
    double Left = 0.0)
{
    /// <summary>无边框。</summary>
    public static BorderSet None { get; } = new();

    /// <summary>四边等宽边框。</summary>
    public static BorderSet Uniform(double width) => new(width, width, width, width);
}
