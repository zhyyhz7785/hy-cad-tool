using HYFEA.Core.Assembly;
using HYFEA.Core.Diagnostics;
using HYFEA.Core.Dofs;
using HYFEA.Core.Elements;
using HYFEA.Core.LinearAlgebra;
using HYFEA.Core.Model;
using HYFEA.Core.Results;
using HYFEA.Core.Solvers;

namespace HYFEA.Core.Analysis;

public sealed class LinearStaticAnalysis
{
    private readonly ILinearSolver _solver;

    public LinearStaticAnalysis(ILinearSolver? solver = null)
    {
        _solver = solver ?? new DenseLinearSolver();
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
                layout,
                null);
        }

        var sys = Assembler.Assemble(problem, layout);
        var red = Assembler.Reduce(sys);
        var sol = _solver.Solve(red.Stiffness, red.Force);
        if (!sol.Success || sol.Solution is null)
        {
            return new FemResult(
                false,
                sol.Error,
                null,
                null,
                null,
                null,
                layout,
                sol.Diagnostics);
        }

        var uFull = Expand(layout, sol.Solution);
        var disp = ToDisplacementField(layout, uFull);
        var rVec = ComputeReactions(sys.Stiffness, sys.Force, uFull);
        var reacField = ToReactionField(layout, rVec);
        var axial = ComputeAxialForces(problem, layout, uFull);

        return new FemResult(
            true,
            null,
            uFull,
            disp,
            reacField,
            axial,
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

    private static DenseVector ComputeReactions(DenseMatrix k, DenseVector f, DenseVector u)
    {
        int n = k.Rows;
        var r = new DenseVector(n);
        for (int i = 0; i < n; i++)
        {
            double ax = 0;
            for (int j = 0; j < n; j++)
                ax += k[i, j] * u[j];
            r[i] = ax - f[i];
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
        foreach (var el in problem.Elements)
        {
            switch (el)
            {
                case Spring1DElementDef s:
                    map.Set(s.Id, Spring1DContribution.AxialForceUxOnly(problem, s, layout, uFull));
                    break;
                case Truss2DElementDef t:
                    map.Set(t.Id, Truss2DContribution.AxialForce(problem, t, layout, uFull));
                    break;
            }
        }
        return map;
    }
}
