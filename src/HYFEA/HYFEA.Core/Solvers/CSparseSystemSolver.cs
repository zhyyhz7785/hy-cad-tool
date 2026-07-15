using HYFEA.Core.Assembly;
using HYFEA.Core.Diagnostics;
using HYFEA.Core.LinearAlgebra;

namespace HYFEA.Core.Solvers;

/// <summary>
/// CSparse.NET 稀疏求解器 (P2.v0.1 Track A) — 适配 ILinearSystemSolver (P2.v0.2)。
/// 使用稀疏三元组装配 + Cholesky 直接求解器。
/// </summary>
public sealed class CSparseSystemSolver : ILinearSystemSolver
{
    public string Name => "CSparse.NET Cholesky";

    public bool SupportsSparse => true;

    /// <summary>
    /// 从稀疏三元组求解 (推荐入口)。
    /// </summary>
    public LinearSolveResult SolveSparse(IReadOnlyList<SparseTriplet> triplets, int n, DenseVector F)
    {
        if (triplets.Count == 0)
        {
            return new LinearSolveResult(
                false,
                null,
                new FemError(FemErrorKind.SingularMatrix, "Empty stiffness matrix"),
                null);
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        double rhsNorm = F.Norm2();

        DenseVector? solution;
        try
        {
            solution = CSparseSolver.Solve(triplets, n, F);
        }
        catch (InvalidOperationException ex)
        {
            sw.Stop();
            return new LinearSolveResult(
                false,
                null,
                new FemError(FemErrorKind.SingularMatrix, ex.Message),
                new SolveDiagnostics(n, rhsNorm, double.NaN, sw.Elapsed));
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new LinearSolveResult(
                false,
                null,
                new FemError(FemErrorKind.SingularMatrix, $"CSparse failed: {ex.Message}"),
                new SolveDiagnostics(n, rhsNorm, double.NaN, sw.Elapsed));
        }

        sw.Stop();

        // 残差计算 (使用三元组格式)
        double residual = ComputeResidualNorm(triplets, n, solution, F);

        return new LinearSolveResult(
            true,
            solution,
            null,
            new SolveDiagnostics(n, rhsNorm, residual, sw.Elapsed));
    }

    /// <summary>
    /// 求解稠密线性系统 (兼容接口,内部转换为稀疏格式)。
    /// 注意: 不推荐用于大规模问题,应使用 SolveSparse。
    /// </summary>
    public LinearSolveResult SolveDense(DenseMatrix K, DenseVector F)
    {
        var triplets = ConvertDenseToTriplets(K);
        return SolveSparse(triplets, K.Rows, F);
    }

    private static double ComputeResidualNorm(IReadOnlyList<SparseTriplet> triplets, int n, DenseVector x, DenseVector f)
    {
        var Ax = new DenseVector(n);
        foreach (var (row, col, value) in triplets)
        {
            Ax[row] += value * x[col];
        }

        double s = 0;
        for (int i = 0; i < n; i++)
        {
            double r = Ax[i] - f[i];
            s += r * r;
        }
        return Math.Sqrt(s);
    }

    private static List<SparseTriplet> ConvertDenseToTriplets(DenseMatrix K)
    {
        var triplets = new List<SparseTriplet>();
        for (int i = 0; i < K.Rows; i++)
        {
            for (int j = 0; j < K.Cols; j++)
            {
                double val = K[i, j];
                if (Math.Abs(val) > 1e-20)
                    triplets.Add(new SparseTriplet(i, j, val));
            }
        }
        return triplets;
    }
}
