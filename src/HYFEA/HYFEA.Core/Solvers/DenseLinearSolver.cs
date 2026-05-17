using HYFEA.Core.Diagnostics;
using HYFEA.Core.LinearAlgebra;

namespace HYFEA.Core.Solvers;

public sealed record LinearSolveResult(
    bool Success,
    DenseVector? Solution,
    FemError? Error,
    SolveDiagnostics? Diagnostics);

public interface ILinearSolver
{
    LinearSolveResult Solve(DenseMatrix K, DenseVector F);
}

/// <summary>
/// Dense linear solve with partial pivoting (LU-style Gaussian elimination).
/// Use for small P0 models where <see cref="DenseMatrix"/> fits in memory.
/// </summary>
public sealed class DenseLinearSolver : ILinearSolver
{
    private const double PivotTol = 1e-18;

    public LinearSolveResult Solve(DenseMatrix K, DenseVector F)
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
                return new LinearSolveResult(false, null,
                    new FemError(FemErrorKind.SingularMatrix, $"Singular or ill-conditioned pivot at column {col}.", col),
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
                double m = A[r, col] / pivot;
                if (Math.Abs(m) < PivotTol)
                    continue;
                for (int c = col; c < n; c++)
                    A[r, c] -= m * A[col, c];
                b[r] -= m * b[col];
            }
        }

        var x = new DenseVector(n);
        for (int r = n - 1; r >= 0; r--)
        {
            double sum = b[r];
            for (int c = r + 1; c < n; c++)
                sum -= A[r, c] * x[c];
            double diag = A[r, r];
            if (Math.Abs(diag) < PivotTol)
            {
                sw.Stop();
                return new LinearSolveResult(false, null,
                    new FemError(FemErrorKind.SingularMatrix, $"Near-zero diagonal in back-substitution at row {r}.", r),
                    new SolveDiagnostics(n, rhsNorm, double.NaN, sw.Elapsed));
            }
            x[r] = sum / diag;
        }

        double residual = ComputeResidualNorm(K, x, F);
        sw.Stop();
        return new LinearSolveResult(true, x, null, new SolveDiagnostics(n, rhsNorm, residual, sw.Elapsed));
    }

    private static double ComputeResidualNorm(DenseMatrix K, DenseVector x, DenseVector f)
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
}
