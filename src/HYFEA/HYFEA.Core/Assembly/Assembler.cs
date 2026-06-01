using HYFEA.Core.Dofs;
using HYFEA.Core.Elements;
using HYFEA.Core.LinearAlgebra;
using HYFEA.Core.Loads;
using HYFEA.Core.Model;

namespace HYFEA.Core.Assembly;

public static class Assembler
{
    public static AssembledLinearSystem Assemble(FemProblem problem, DofLayout layout)
    {
        int n = layout.TotalDofCount;
        var K = new DenseMatrix(n, n);
        var F = new DenseVector(n);

        foreach (var load in problem.LoadCase.Loads)
        {
            int g = layout.GetGlobalIndex(load.NodeId, load.Dof);
            F[g] += load.Value;
        }

        foreach (var el in problem.Elements)
        {
            switch (el)
            {
                case Spring1DElementDef s:
                    Spring1DContribution.Contribute(problem, s, layout, K);
                    break;
                case Truss2DElementDef t:
                    Truss2DContribution.Contribute(problem, t, layout, K);
                    break;
                case EulerBeam2DElementDef b:
                    EulerBeam2DContribution.Contribute(problem, b, layout, K);
                    break;
                default:
                    throw new NotSupportedException($"Element {el.GetType().Name} not supported.");
            }
        }

        foreach (var eload in problem.LoadCase.ElementLoads)
        {
            if (eload is not UniformBeamLoad ul)
                continue;
            var found = FindElement(problem, ul.TargetElement) as EulerBeam2DElementDef;
            if (found is null)
                throw new InvalidOperationException($"Uniform load targets missing or non-beam element {ul.TargetElement.Value}.");
            EulerBeam2DContribution.ApplyEquivalentNodalForces(problem, found, ul, layout, F);
        }

        return new AssembledLinearSystem(K, F, layout);
    }

    private static FemElementDefinition? FindElement(FemProblem problem, ElementId id)
    {
        foreach (var e in problem.Elements)
        {
            if (e.Id == id)
                return e;
        }
        return null;
    }

    public static ReducedLinearSystem Reduce(AssembledLinearSystem system)
    {
        var layout = system.Layout;
        int nf = layout.FreeDofCount;
        var Kff = new DenseMatrix(nf, nf);
        var Ff = new DenseVector(nf);
        for (int i = 0; i < nf; i++)
        {
            int gi = layout.FreeToGlobal[i];
            Ff[i] = system.Force[gi];
            for (int j = 0; j < nf; j++)
            {
                int gj = layout.FreeToGlobal[j];
                Kff[i, j] = system.Stiffness[gi, gj];
            }
        }
        return new ReducedLinearSystem(Kff, Ff, system);
    }
}
