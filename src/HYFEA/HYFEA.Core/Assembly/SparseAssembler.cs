using HYFEA.Core.Dofs;
using HYFEA.Core.Elements;
using HYFEA.Core.LinearAlgebra;
using HYFEA.Core.Model;
using HYFEA.Core.Solvers;

namespace HYFEA.Core.Assembly;

/// <summary>
/// 稀疏矩阵装配器 (P2.v0.1 CSparse.NET Track A)。
/// 输出三元组格式,避免稠密矩阵内存开销。
/// </summary>
public static class SparseAssembler
{
    /// <summary>
    /// 装配全局刚度矩阵(三元组)和荷载向量。
    /// </summary>
    public static SparseAssembledLinearSystem Assemble(FemProblem problem, DofLayout layout)
    {
        int n = layout.TotalDofCount;
        var triplets = new List<SparseTriplet>(capacity: EstimateTripletCount(problem, layout));
        var F = new DenseVector(n);

        // 集中荷载
        foreach (var load in problem.LoadCase.Loads)
        {
            int g = layout.GetGlobalIndex(load.NodeId, load.Dof);
            F[g] += load.Value;
        }

        // 直接累加到三元组。不分配 n×n 稠密阵，也不用绝对阈值丢掉很小的刚度项。
        var registry = new ElementContributionRegistry();
        var sink = new TripletAccumulator();
        foreach (var el in problem.Elements)
        {
            var contrib = registry.Resolve(el);
            contrib.ApplyStiffness(problem, el, layout, sink);
        }

        triplets.AddRange(sink.ToTriplets());

        // 单元荷载 (等效节点荷载)
        foreach (var el in problem.Elements)
        {
            var contrib = registry.Resolve(el);
            var loads = problem.LoadCase.ElementLoads.Where(ld => ld.TargetElement == el.Id).ToList();
            contrib.ApplyEquivalentLoads(problem, el, loads, layout, F);
        }

        return new SparseAssembledLinearSystem(triplets, F, layout);
    }

    /// <summary>
    /// 施加边界条件,约简为自由自由度系统 Kff * uf = Ff。
    /// </summary>
    public static SparseReducedLinearSystem Reduce(SparseAssembledLinearSystem system)
    {
        var layout = system.Layout;
        int nf = layout.FreeDofCount;

        // 过滤三元组:仅保留自由-自由对
        var triplets_ff = new List<SparseTriplet>(capacity: system.StiffnessTriplets.Count);
        foreach (var (row, col, value) in system.StiffnessTriplets)
        {
            int fi = layout.GlobalToFree[row];
            int fj = layout.GlobalToFree[col];
            if (fi >= 0 && fj >= 0)  // 两者均为自由自由度
                triplets_ff.Add(new SparseTriplet(fi, fj, value));
        }

        // 约简荷载向量
        var Ff = new DenseVector(nf);
        for (int i = 0; i < nf; i++)
        {
            int gi = layout.FreeToGlobal[i];
            Ff[i] = system.Force[gi];
        }

        return new SparseReducedLinearSystem(triplets_ff, Ff, system);
    }

    /// <summary>
    /// 估算三元组数量:梁单元 2 节点 × 3 DOF/节点 = 6 DOF,刚度矩阵 6×6=36 项/单元。
    /// </summary>
    private static int EstimateTripletCount(FemProblem problem, DofLayout layout)
    {
        // 保守估计:每单元 50 项(含对称项)
        return problem.Elements.Count * 50;
    }

    /// <summary>按 (row,col) 求和，保留单元写入的全部非零项。</summary>
    private sealed class TripletAccumulator : IMatrixAccumulator
    {
        private readonly Dictionary<(int Row, int Col), double> _entries = new();

        public void Add(int row, int col, double value)
        {
            if (value == 0.0)
                return;

            var key = (row, col);
            _entries.TryGetValue(key, out double current);
            double sum = current + value;
            if (sum == 0.0)
                _entries.Remove(key);
            else
                _entries[key] = sum;
        }

        public List<SparseTriplet> ToTriplets()
        {
            var list = new List<SparseTriplet>(_entries.Count);
            foreach (var entry in _entries)
                list.Add(new SparseTriplet(entry.Key.Row, entry.Key.Col, entry.Value));
            return list;
        }
    }
}
