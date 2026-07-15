using HYFEA.Core.LinearAlgebra;

namespace HYFEA.Core.Solvers;

/// <summary>
/// 线性系统求解器接口 (P2.v0.2)。
/// 支持 Track A (CSparse) 和 Track B (自研) 的双轨可替换机制。
/// </summary>
public interface ILinearSystemSolver
{
    /// <summary>
    /// 求解器名称(用于日志/性能报告)。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 求解稠密线性系统 K·u = F。
    /// </summary>
    /// <param name="K">刚度矩阵(稠密表示)</param>
    /// <param name="F">荷载向量</param>
    /// <returns>求解结果(包含位移向量、诊断信息)</returns>
    LinearSolveResult SolveDense(DenseMatrix K, DenseVector F);

    /// <summary>
    /// 求解稀疏线性系统(三元组格式)。
    /// </summary>
    /// <param name="triplets">稀疏三元组列表 (row, col, value)</param>
    /// <param name="n">系统维度</param>
    /// <param name="F">荷载向量</param>
    /// <returns>求解结果</returns>
    LinearSolveResult SolveSparse(IReadOnlyList<SparseTriplet> triplets, int n, DenseVector F);

    /// <summary>
    /// 是否原生支持稀疏求解(true: CSparse / false: Dense fallback)。
    /// </summary>
    bool SupportsSparse { get; }
}
