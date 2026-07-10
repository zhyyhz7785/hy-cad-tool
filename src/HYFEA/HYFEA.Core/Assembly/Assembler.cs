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

        // P1.v0.1b: registry-based polymorphic dispatch
        var registry = new ElementContributionRegistry();

        foreach (var el in problem.Elements)
        {
            var contrib = registry.Resolve(el);
            contrib.ApplyStiffness(problem, el, layout, K);
        }

        foreach (var el in problem.Elements)
        {
            var contrib = registry.Resolve(el);
            var loads = problem.LoadCase.ElementLoads.Where(ld => ld.TargetElement == el.Id).ToList();
            contrib.ApplyEquivalentLoads(problem, el, loads, layout, F);
        }

        return new AssembledLinearSystem(K, F, layout);
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
