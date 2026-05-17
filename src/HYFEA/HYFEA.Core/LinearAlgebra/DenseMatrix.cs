namespace HYFEA.Core.LinearAlgebra;

public sealed class DenseMatrix
{
    public DenseMatrix(int nRows, int nCols)
    {
        Rows = nRows;
        Cols = nCols;
        Data = new double[nRows * nCols];
    }

    public int Rows { get; }
    public int Cols { get; }

    public double[] Data { get; }

    public double this[int row, int col]
    {
        get => Data[row * Cols + col];
        set => Data[row * Cols + col] = value;
    }

    public void Add(int row, int col, double value) => Data[row * Cols + col] += value;

    public DenseMatrix CloneMatrix()
    {
        var c = new DenseMatrix(Rows, Cols);
        Array.Copy(Data, c.Data, Data.Length);
        return c;
    }
}
