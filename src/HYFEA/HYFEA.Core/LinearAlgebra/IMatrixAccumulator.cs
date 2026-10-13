namespace HYFEA.Core.LinearAlgebra;

/// <summary>
/// 刚度装配目标：稠密矩阵或稀疏三元组都可以接收 <see cref="Add"/>。
/// </summary>
public interface IMatrixAccumulator
{
    void Add(int row, int col, double value);
}
