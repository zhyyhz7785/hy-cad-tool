namespace HyCAD.Tables.Inference;

/// <summary>推断出的网格边界线（调试用）。</summary>
public sealed class GridLineSet
{
    public GridLineSet(IReadOnlyList<double> xBoundaries, IReadOnlyList<double> yBoundaries)
    {
        XBoundaries = xBoundaries ?? throw new ArgumentNullException(nameof(xBoundaries));
        YBoundaries = yBoundaries ?? throw new ArgumentNullException(nameof(yBoundaries));
    }

    public IReadOnlyList<double> XBoundaries { get; }

    public IReadOnlyList<double> YBoundaries { get; }

    public int ColCount => Math.Max(0, XBoundaries.Count - 1);

    public int RowCount => Math.Max(0, YBoundaries.Count - 1);
}
