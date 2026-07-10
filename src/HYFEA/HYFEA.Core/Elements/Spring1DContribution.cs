using HYFEA.Core.Dofs;
using HYFEA.Core.LinearAlgebra;
using HYFEA.Core.Loads;
using HYFEA.Core.Materials;
using HYFEA.Core.Model;
using HYFEA.Core.Sections;

namespace HYFEA.Core.Elements;

/// <summary>Spring1D contribution: UX-only, axial stiffness k = EA/L.</summary>
internal sealed class Spring1DContribution : IElementContribution
{
    public Type ElementDefinitionType => typeof(Spring1DElementDef);

    public IReadOnlyList<DofType> ActiveDofsForNode(FemElementDefinition element, NodeId node)
    {
        var s = (Spring1DElementDef)element;
        if (node == s.NodeA || node == s.NodeB)
            return new[] { DofType.UX };
        return Array.Empty<DofType>();
    }

    public void ApplyStiffness(FemProblem problem, FemElementDefinition element, DofLayout layout, DenseMatrix K)
    {
        var e = (Spring1DElementDef)element;
        var na = GetNode(problem, e.NodeA);
        var nb = GetNode(problem, e.NodeB);
        double L = na.Position.DistanceTo(nb.Position);
        if (L < 1e-30)
            throw new InvalidOperationException($"Spring element {e.Id.Value}: zero length.");

        double k = GetAxialStiffness(problem, e.MaterialId, e.SectionId, L);
        int g0 = layout.GetGlobalIndex(e.NodeA, DofType.UX);
        int g1 = layout.GetGlobalIndex(e.NodeB, DofType.UX);

        K.Add(g0, g0, k);
        K.Add(g0, g1, -k);
        K.Add(g1, g0, -k);
        K.Add(g1, g1, k);
    }

    public void ApplyEquivalentLoads(
        FemProblem problem,
        FemElementDefinition element,
        IReadOnlyList<IElementLoad> elementLoads,
        DofLayout layout,
        DenseVector F)
    {
        // Spring1D has no element loads (no distributed load on 1D spring)
    }

    public object Recover(FemProblem problem, FemElementDefinition element, DofLayout layout, DenseVector uFull)
    {
        var e = (Spring1DElementDef)element;
        int ga = layout.GetGlobalIndex(e.NodeA, DofType.UX);
        int gb = layout.GetGlobalIndex(e.NodeB, DofType.UX);
        var na = GetNode(problem, e.NodeA);
        var nb = GetNode(problem, e.NodeB);
        double L = na.Position.DistanceTo(nb.Position);
        double k = GetAxialStiffness(problem, e.MaterialId, e.SectionId, L);
        return k * (uFull[gb] - uFull[ga]); // tension-positive axial force
    }

    private static double GetAxialStiffness(FemProblem problem, MaterialId mid, SectionId sid, double L)
    {
        var mat = problem.Materials[mid];
        var sec = problem.Sections[sid];
        if (mat is not LinearElasticMaterial le)
            throw new InvalidOperationException("Spring1D requires LinearElasticMaterial.");
        if (sec is not AxialSection ax)
            throw new InvalidOperationException("Spring1D requires AxialSection.");
        return le.YoungsModulus * ax.Area / L;
    }

    private static FemNode GetNode(FemProblem p, NodeId id)
    {
        foreach (var n in p.Nodes)
        {
            if (n.Id == id)
                return n;
        }
        throw new ArgumentException($"Node {id.Value} not found.");
    }
}

