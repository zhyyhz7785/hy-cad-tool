using CSparse.Double;
using CSparse.Storage;
using HYFEA.Core.LinearAlgebra;

namespace HYFEA.Core.Solvers;

/// <summary>
/// CSparse.NET 稀疏求解器适配 (P2.v0.1 Track A)。
/// 使用 Cholesky 分解求解对称正定系统 Kx = F。
/// </summary>
public static class CSparseSolver
{
    /// <summary>
    /// 求解稀疏线性系统 Ax = b。
    /// </summary>
    /// <param name="triplets">系数矩阵三元组 (row, col, value)</param>
    /// <param name="n">系统维度</param>
    /// <param name="rhs">右端向量</param>
    /// <returns>解向量 x</returns>
    public static DenseVector Solve(
        IReadOnlyList<SparseTriplet> triplets,
        int n,
        DenseVector rhs)
    {
        if (triplets.Count == 0)
            throw new ArgumentException("Empty triplet list", nameof(triplets));
        if (n <= 0)
            throw new ArgumentException("Invalid dimension", nameof(n));
        if (rhs.Length != n)
            throw new ArgumentException("RHS size mismatch", nameof(rhs));

        // 1. 构建 COO (Coordinate) 稀疏矩阵
        var builder = new CoordinateStorage<double>(n, n, triplets.Count);
        foreach (var (row, col, value) in triplets)
        {
            if (row < 0 || row >= n || col < 0 || col >= n)
                throw new ArgumentException($"Invalid triplet ({row},{col}) for n={n}");
            builder.At(row, col, value);  // CSparse 自动累加重复项
        }

        // 2. 转换为 CSC (Compressed Sparse Column) 格式
        var A = CompressedColumnStorage<double>.OfIndexed(builder);

        // 3. 构造 RHS 向量数组
        var b = new double[n];
        for (int i = 0; i < n; i++)
            b[i] = rhs[i];

        // 4. Cholesky 分解求解 (对称正定系统)
        double[]? x;
        try
        {
            var chol = CSparse.Double.Factorization.SparseCholesky.Create(A, CSparse.ColumnOrdering.MinimumDegreeAtPlusA);
            x = new double[n];
            chol.Solve(b, x);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "CSparse Cholesky failed. Matrix may not be symmetric positive definite.", ex);
        }

        if (x == null || x.Length != n)
            throw new InvalidOperationException("CSparse returned invalid solution");

        // 5. 转回 DenseVector
        var result = new DenseVector(n);
        for (int i = 0; i < n; i++)
            result[i] = x[i];

        return result;
    }
}
