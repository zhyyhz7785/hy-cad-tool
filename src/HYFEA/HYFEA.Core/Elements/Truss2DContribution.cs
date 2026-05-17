using HYFEA.Core.Dofs;
using HYFEA.Core.Geometry;
using HYFEA.Core.LinearAlgebra;
using HYFEA.Core.Materials;
using HYFEA.Core.Model;
using HYFEA.Core.Sections;

namespace HYFEA.Core.Elements;

internal static class Truss2DContribution
{
    public static void Contribute(FemProblem problem, Truss2DElementDef e, DofLayout layout, DenseMatrix K)
    {
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

    /// <summary>Tension-positive axial force N.</summary>
    public static double AxialForce(FemProblem problem, Truss2DElementDef e, DofLayout layout, DenseVector uFull)
    {
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
        return k * delta;
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
        var v = Vector2D.From(na.Position, nb.Position);
        L = v.Length;
        if (L < 1e-30)
            throw new InvalidOperationException($"Truss element {e.Id.Value}: zero length.");
        var vn = v.Normalized();
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
