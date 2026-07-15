using HYFEA.Core.Diagnostics;
using HYFEA.Core.LinearAlgebra;

namespace HYFEA.Core.Solvers;

/// <summary>
/// 稠密 Gauss 消元求解器 (Track B baseline) — 适配 ILinearSystemSolver (P2.v0.2)。
/// </summary>
public sealed class DenseSystemSolver : ILinearSystemSolver
{
    private const double PivotTol = 1e-18;

    public string Name => "Dense Gauss Elimination (Track B)";

    public bool SupportsSparse => false;

    /// <summary>
    /// 求解稠密线性系统 (原生接口)。
    /// </summary>
    public LinearSolveResult SolveDense(DenseMatrix K, DenseVector F)
    {
        if (K.Rows != K.Cols)
            return new LinearSolveResult(false, null, new FemError(FemErrorKind.SingularMatrix, "Matrix must be square."), null);
        int n = K.Rows;
        if (F.Length != n)
            return new LinearSolveResult(false, null, new FemError(FemErrorKind.SingularMatrix, "RHS length mismatch."), null);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        double rhsNorm = F.Norm2();

        var A = K.CloneMatrix();
        var b = F.CloneVector();

        // Gaussian elimination with partial pivoting
        for (int col = 0; col < n; col++)
        {
            int pivRow = col;
            double maxAbs = Math.Abs(A[col, col]);
            for (int r = col + 1; r < n; r++)
            {
                double v = Math.Abs(A[r, col]);
                if (v > maxAbs)
                {
                    maxAbs = v;
                    pivRow = r;
                }
            }

            if (maxAbs < PivotTol)
            {
                sw.Stop();
                return new LinearSolveResult(
                    false,
                    null,
                    new FemError(FemErrorKind.SingularMatrix, $"Zero pivot at column {col}."),
                    new SolveDiagnostics(n, rhsNorm, double.NaN, sw.Elapsed));
            }

            if (pivRow != col)
            {
                for (int c = col; c < n; c++)
                    (A[col, c], A[pivRow, c]) = (A[pivRow, c], A[col, c]);
                (b[col], b[pivRow]) = (b[pivRow], b[col]);
            }

            double pivot = A[col, col];
            for (int r = col + 1; r < n; r++)
            {
                double factor = A[r, col] / pivot;
                A[r, col] = 0;
                for (int c = col + 1; c < n; c++)
                    A[r, c] -= factor * A[col, c];
                b[r] -= factor * b[col];
            }
        }

        // Back substitution
        for (int i = n - 1; i >= 0; i--)
        {
            double sum = b[i];
            for (int j = i + 1; j < n; j++)
                sum -= A[i, j] * b[j];
            b[i] = sum / A[i, i];
        }

        sw.Stop();

        // Compute residual
        double residual = ComputeResidual(K, b, F);

        return new LinearSolveResult(
            true,
            b,
            null,
            new SolveDiagnostics(n, rhsNorm, residual, sw.Elapsed));
    }

    /// <summary>
    /// 求解稀疏系统 (内部转换为稠密格式,不推荐)。
    /// </summary>
    public LinearSolveResult SolveSparse(IReadOnlyList<SparseTriplet> triplets, int n, DenseVector F)
    {
        var K = ConvertTripletsToDense(triplets, n);
        return SolveDense(K, F);
    }

    private static double ComputeResidual(DenseMatrix K, DenseVector x, DenseVector f)
    {
        int n = K.Rows;
        double s = 0;
        for (int i = 0; i < n; i++)
        {
            double ax = 0;
            for (int j = 0; j < n; j++)
                ax += K[i, j] * x[j];
            double r = ax - f[i];
            s += r * r;
        }
        return Math.Sqrt(s);
    }

    private static DenseMatrix ConvertTripletsToDense(IReadOnlyList<SparseTriplet> triplets, int n)
    {
        var K = new DenseMatrix(n, n);
        foreach (var (row, col, value) in triplets)
        {
            K.Add(row, col, value);
        }
        return K;
    }
}
