namespace HYFEA.Core.Solvers;

/// <summary>
/// 稀疏矩阵三元组 (row, col, value) — P2.v0.1 CSparse.NET 装配格式。
/// </summary>
public readonly struct SparseTriplet
{
    public int Row { get; }
    public int Col { get; }
    public double Value { get; }

    public SparseTriplet(int row, int col, double value)
    {
        Row = row;
        Col = col;
        Value = value;
    }

    public void Deconstruct(out int row, out int col, out double value)
    {
        row = Row;
        col = Col;
        value = Value;
    }
}
