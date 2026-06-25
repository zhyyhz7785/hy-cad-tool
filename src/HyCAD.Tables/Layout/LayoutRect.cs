namespace HyCAD.Tables.Layout;

/// <summary>
/// 布局外接矩形（GrowDirection.Down：Top &gt; Bottom，Height 为正）。
/// </summary>
/// <param name="Left">左边缘 X（mm）。</param>
/// <param name="Top">顶边 Y（mm）。</param>
/// <param name="Right">右边缘 X（mm）。</param>
/// <param name="Bottom">底边 Y（mm）。</param>
public readonly record struct LayoutRect(double Left, double Top, double Right, double Bottom)
{
    /// <summary>宽度（mm）。</summary>
    public double Width => Right - Left;

    /// <summary>高度（mm，Down 方向 Top &gt; Bottom 时为正）。</summary>
    public double Height => Top - Bottom;

    /// <summary>矩形中心点。</summary>
    public LayoutPoint Center => new((Left + Right) / 2, (Top + Bottom) / 2);
}
