using HYFEA.Core.Assembly;
using HYFEA.Core.Diagnostics;
using HYFEA.Core.LinearAlgebra;

namespace HYFEA.Core.Solvers;

/// <summary>
/// CSparse.NET 稀疏求解器 (P2.v0.1 Track A)。
/// P2.v0.2 起被 <see cref="CSparseSystemSolver"/> 取代;本类委托到新实现,仅为兼容保留。
/// </summary>
[Obsolete("Use CSparseSystemSolver (ILinearSystemSolver) instead. Kept for P2.v0.1 compatibility.")]
public sealed class CSparseLinearSolver : ILinearSolver
{
    private readonly CSparseSystemSolver _inner = new();

    /// <summary>兼容 ILinearSolver 接口;实际使用请调用 SolveSparse。</summary>
    public LinearSolveResult Solve(DenseMatrix K, DenseVector F) => _inner.SolveDense(K, F);

    /// <summary>从稀疏三元组求解。</summary>
    public LinearSolveResult SolveSparse(List<SparseTriplet> triplets, int n, DenseVector F) =>
        _inner.SolveSparse(triplets, n, F);
}
