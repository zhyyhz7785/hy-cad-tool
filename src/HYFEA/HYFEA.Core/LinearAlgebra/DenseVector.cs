namespace HYFEA.Core.LinearAlgebra;

public sealed class DenseVector
{
    public DenseVector(int n)
    {
        Data = new double[n];
    }

    public DenseVector(double[] data)
    {
        Data = data;
    }

    public double[] Data { get; }

    public int Length => Data.Length;

    public double this[int i]
    {
        get => Data[i];
        set => Data[i] = value;
    }

    public static DenseVector Zeros(int n) => new(n);

    public DenseVector CloneVector()
    {
        var c = new DenseVector(Length);
        Array.Copy(Data, c.Data, Length);
        return c;
    }

    public double Norm2()
    {
        double s = 0;
        for (int i = 0; i < Length; i++)
            s += Data[i] * Data[i];
        return Math.Sqrt(s);
    }
}
