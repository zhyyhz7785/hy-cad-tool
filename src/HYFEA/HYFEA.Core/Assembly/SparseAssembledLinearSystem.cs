using HYFEA.Core.Dofs;
using HYFEA.Core.Solvers;

namespace HYFEA.Core.Assembly;

/// <summary>
/// 稀疏格式的装配线性系统 (P2.v0.1 CSparse.NET)。
/// 存储三元组 (row, col, value) 而非稠密矩阵。
/// </summary>
public sealed class SparseAssembledLinearSystem
{
    public SparseAssembledLinearSystem(
        List<SparseTriplet> stiffnessTriplets,
        LinearAlgebra.DenseVector force,
        DofLayout layout)
    {
        StiffnessTriplets = stiffnessTriplets;
        Force = force;
        Layout = layout;
    }

    /// <summary>刚度矩阵三元组 (全局自由度索引)</summary>
    public List<SparseTriplet> StiffnessTriplets { get; }

    /// <summary>荷载向量 (全局自由度索引)</summary>
    public LinearAlgebra.DenseVector Force { get; }

    /// <summary>自由度布局</summary>
    public DofLayout Layout { get; }
}

/// <summary>
/// 稀疏格式的约简线性系统 (施加边界条件后,仅自由自由度)。
/// </summary>
public sealed class SparseReducedLinearSystem
{
    public SparseReducedLinearSystem(
        List<SparseTriplet> stiffnessTriplets,
        LinearAlgebra.DenseVector force,
        SparseAssembledLinearSystem full)
    {
        StiffnessTriplets = stiffnessTriplets;
        Force = force;
        Full = full;
        Size = force.Length;
    }

    /// <summary>约简刚度矩阵三元组 Kff (自由自由度索引 0~nf-1)</summary>
    public List<SparseTriplet> StiffnessTriplets { get; }

    /// <summary>约简荷载向量 Ff</summary>
    public LinearAlgebra.DenseVector Force { get; }

    /// <summary>完整装配系统(用于回代)</summary>
    public SparseAssembledLinearSystem Full { get; }

    /// <summary>自由自由度数量</summary>
    public int Size { get; }
}
