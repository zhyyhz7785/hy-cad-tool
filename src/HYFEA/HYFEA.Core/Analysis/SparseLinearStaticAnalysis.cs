using HYFEA.Core.Assembly;
using HYFEA.Core.Diagnostics;
using HYFEA.Core.Dofs;
using HYFEA.Core.Elements;
using HYFEA.Core.LinearAlgebra;
using HYFEA.Core.Model;
using HYFEA.Core.Results;
using HYFEA.Core.Solvers;

namespace HYFEA.Core.Analysis;

/// <summary>
/// 稀疏线性静力分析 (P2.v0.1 CSparse.NET Track A;P2.v0.2 支持求解器注册表)。
/// 使用稀疏三元组装配 + ILinearSystemSolver 稀疏求解。
/// </summary>
public sealed class SparseLinearStaticAnalysis
{
    private readonly ILinearSystemSolver _solver;

    /// <summary>
    /// 创建稀疏分析 (默认使用 CSparse.NET)。
    /// </summary>
    /// <param name="solver">指定求解器;null 则用 CSparseSystemSolver</param>
    public SparseLinearStaticAnalysis(ILinearSystemSolver? solver = null)
    {
        _solver = solver ?? new CSparseSystemSolver();
        if (!_solver.SupportsSparse)
            throw new ArgumentException(
                $"Solver '{_solver.Name}' does not natively support sparse; use LinearStaticAnalysis instead.",
                nameof(solver));
    }

    public FemResult Run(FemProblem problem)
    {
        var layout = DofLayout.Build(problem);
        if (layout.FreeDofCount <= 0)
        {
            return new FemResult(
                false,
                new FemError(FemErrorKind.InsufficientConstraints, "No free DOFs to solve."),
                null,
                null,
                null,
                null,
                null,
                layout,
                null);
        }

        // 使用稀疏装配器
        var sys = SparseAssembler.Assemble(problem, layout);
        var red = SparseAssembler.Reduce(sys);

        // 稀疏求解
        var sol = _solver.SolveSparse(red.StiffnessTriplets, red.Size, red.Force);
        if (!sol.Success || sol.Solution is null)
        {
            return new FemResult(
                false,
                sol.Error,
                null,
                null,
                null,
                null,
                null,
                layout,
                sol.Diagnostics);
        }

        var uFull = Expand(layout, sol.Solution);
        var disp = ToDisplacementField(layout, uFull);
        var rVec = ComputeReactions(sys.StiffnessTriplets, sys.Layout.TotalDofCount, sys.Force, uFull);
        var reacField = ToReactionField(layout, rVec);
        var axial = ComputeAxialForces(problem, layout, uFull);
        var beams = ComputeBeamEndForces(problem, layout, uFull);

        return new FemResult(
            true,
            null,
            uFull,
            disp,
            reacField,
            axial,
            beams,
            layout,
            sol.Diagnostics);
    }

    private static DenseVector Expand(DofLayout layout, DenseVector uFree)
    {
        var u = new DenseVector(layout.TotalDofCount);
        for (int i = 0; i < layout.FreeDofCount; i++)
        {
            int g = layout.FreeToGlobal[i];
            u[g] = uFree[i];
        }
        return u;
    }

    private static NodalValueField ToDisplacementField(DofLayout layout, DenseVector uFull)
    {
        var f = new NodalValueField();
        for (int g = 0; g < layout.TotalDofCount; g++)
        {
            var (node, dof) = layout.GetPair(g);
            f.Set(node, dof, uFull[g]);
        }
        return f;
    }

    /// <summary>
    /// 计算反力 r = K*u - F (稀疏矩阵版本)。
    /// </summary>
    private static DenseVector ComputeReactions(List<SparseTriplet> kTriplets, int n, DenseVector f, DenseVector u)
    {
        var Ku = new DenseVector(n);
        foreach (var (row, col, value) in kTriplets)
        {
            Ku[row] += value * u[col];
        }

        var r = new DenseVector(n);
        for (int i = 0; i < n; i++)
        {
            r[i] = Ku[i] - f[i];
        }
        return r;
    }

    private static NodalValueField ToReactionField(DofLayout layout, DenseVector r)
    {
        var f = new NodalValueField();
        for (int g = 0; g < layout.TotalDofCount; g++)
        {
            var pair = layout.GetPair(g);
            if (!layout.ConstrainedPairs.Contains(pair))
                continue;
            f.Set(pair.node, pair.dof, r[g]);
        }
        return f;
    }

    private static ElementAxialForceMap ComputeAxialForces(FemProblem problem, DofLayout layout, DenseVector uFull)
    {
        var map = new ElementAxialForceMap();
        var registry = new ElementContributionRegistry();

        foreach (var el in problem.Elements)
        {
            var contrib = registry.Resolve(el);
            var result = contrib.Recover(problem, el, layout, uFull);

            // Extract axial force from result
            double axial = result switch
            {
                double d => d,  // Spring1D, Truss2D return double
                BeamEndValues b => b.N,  // EulerBeam2D returns BeamEndValues
                _ => 0.0
            };

            map.Set(el.Id, axial);
        }

        return map;
    }

    private static ElementBeamEndForceMap ComputeBeamEndForces(FemProblem problem, DofLayout layout, DenseVector uFull)
    {
        var map = new ElementBeamEndForceMap();
        var registry = new ElementContributionRegistry();

        foreach (var el in problem.Elements)
        {
            if (el is not EulerBeam2DElementDef)
                continue;

            var contrib = registry.Resolve(el);
            var result = contrib.Recover(problem, el, layout, uFull);

            if (result is BeamEndValues beamEnd)
            {
                map.Set(el.Id, beamEnd);
            }
        }

        return map;
    }
}
