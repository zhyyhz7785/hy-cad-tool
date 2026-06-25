using HyCAD.Tables.Layout;

namespace HyCAD.Tables.Inference;

/// <summary>水平/竖直线段（模型空间 mm）。</summary>
public readonly record struct LineSegment2d(
    double X1,
    double Y1,
    double X2,
    double Y2,
    GridLineOrientation Orientation)
{
    public double IntervalStart =>
        Orientation == GridLineOrientation.Horizontal ? Math.Min(X1, X2) : Math.Min(Y1, Y2);

    public double IntervalEnd =>
        Orientation == GridLineOrientation.Horizontal ? Math.Max(X1, X2) : Math.Max(Y1, Y2);

    public double FixedCoord =>
        Orientation == GridLineOrientation.Horizontal ? (Y1 + Y2) / 2 : (X1 + X2) / 2;

    public double Length =>
        Orientation == GridLineOrientation.Horizontal
            ? Math.Abs(X2 - X1)
            : Math.Abs(Y2 - Y1);
}
