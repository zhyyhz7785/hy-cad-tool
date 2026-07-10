using HYFEA.Core.Dofs;
using HyCAD.Geometry;
using HYFEA.Core.LinearAlgebra;
using HYFEA.Core.Loads;
using HYFEA.Core.Materials;
using HYFEA.Core.Model;
using HYFEA.Core.Sections;

namespace HYFEA.Core.Elements;

/// <summary>Truss2D contribution: UX+UY, tension/compression only.</summary>
internal sealed class Truss2DContribution : IElementContribution
{
    public Type ElementDefinitionType => typeof(Truss2DElementDef);

    public IReadOnlyList<DofType> ActiveDofsForNode(FemElementDefinition element, NodeId node)
    {
        var t = (Truss2DElementDef)element;
        if (node == t.NodeA || node == t.NodeB)
            return new[] { DofType.UX, DofType.UY };
        return Array.Empty<DofType>();
    }

    public void ApplyStiffness(FemProblem problem, FemElementDefinition element, DofLayout layout, DenseMatrix K)
    {
        var e = (Truss2DElementDef)element;
        GetBarData(problem, e, out double L, out double c, out double s, out double k);
        double[] b = { -c, -s, c, s };
        int[] g =
        {
            layout.GetGlobalIndex(e.NodeA, DofType.UX),
            layout.GetGlobalIndex(e.NodeA, DofType.UY),
            layout.GetGlobalIndex(e.NodeB, DofType.UX),
            layout.GetGlobalIndex(e.NodeB, DofType.UY),
        };
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
                K.Add(g[i], g[j], k * b[i] * b[j]);
        }
    }

    public void ApplyEquivalentLoads(
        FemProblem problem,
        FemElementDefinition element,
        IReadOnlyList<IElementLoad> elementLoads,
        DofLayout layout,
        DenseVector F)
    {
        // Truss2D has no element loads (no distributed load on truss bar)
    }

    public object Recover(FemProblem problem, FemElementDefinition element, DofLayout layout, DenseVector uFull)
    {
        var e = (Truss2DElementDef)element;
        GetBarData(problem, e, out double L, out double c, out double s, out double k);
        int gax = layout.GetGlobalIndex(e.NodeA, DofType.UX);
        int gay = layout.GetGlobalIndex(e.NodeA, DofType.UY);
        int gbx = layout.GetGlobalIndex(e.NodeB, DofType.UX);
        int gby = layout.GetGlobalIndex(e.NodeB, DofType.UY);
        double uax = uFull[gax];
        double uay = uFull[gay];
        double ubx = uFull[gbx];
        double uby = uFull[gby];
        double delta = c * (ubx - uax) + s * (uby - uay);
        return k * delta; // tension-positive axial force
    }

    private static void GetBarData(
        FemProblem problem,
        Truss2DElementDef e,
        out double L,
        out double c,
        out double s,
        out double k)
    {
        var na = GetNode(problem, e.NodeA);
        var nb = GetNode(problem, e.NodeB);
        var v = na.Position.VectorTo(nb.Position);
        L = v.Length;
        if (L < 1e-30)
            throw new InvalidOperationException($"Truss element {e.Id.Value}: zero length.");
        var vn = v.Normalize();
        c = vn.X;
        s = vn.Y;
        var mat = problem.Materials[e.MaterialId];
        var sec = problem.Sections[e.SectionId];
        if (mat is not LinearElasticMaterial le)
            throw new InvalidOperationException("Truss2D requires LinearElasticMaterial.");
        if (sec is not AxialSection ax)
            throw new InvalidOperationException("Truss2D requires AxialSection.");
        k = le.YoungsModulus * ax.Area / L;
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
