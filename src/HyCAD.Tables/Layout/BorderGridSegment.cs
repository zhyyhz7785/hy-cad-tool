namespace HyCAD.Tables.Layout;

/// <summary>网格线段方向。</summary>
public enum GridLineOrientation
{
    Horizontal,
    Vertical,
}

/// <summary>表格网格上的一段直线边框（模型空间 mm）。</summary>
public readonly record struct BorderGridSegment(
    GridLineOrientation Orientation,
    double FixedCoord,
    double Start,
    double End,
    double WidthMm);
