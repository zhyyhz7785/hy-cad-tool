using HYFEA.Core.Dofs;
using HYFEA.Core.LinearAlgebra;
using HYFEA.Core.Loads;
using HYFEA.Core.Materials;
using HYFEA.Core.Model;
using HYFEA.Core.Sections;

namespace HYFEA.Core.Elements;

/// <summary>Euler–Bernoulli plane beam (6 dof: ux, uy, rz per node).</summary>
/// <remarks>
/// Sign / recovery: end moments from <b>residual</b> <c>K_l u_l - f_equiv</c> rotation components (dual to rz); see source comments.
/// </remarks>
internal static class EulerBeam2DContribution
{
    public static void Contribute(FemProblem problem, EulerBeam2DElementDef e, DofLayout layout, DenseMatrix K)
    {
        GetBeamData(problem, e, out double L, out double c, out double s, out double E, out double A, out double I);

        Span<double> kl = stackalloc double[36];
        BuildLocalStiffness(E, A, I, L, kl);

        Span<double> t = stackalloc double[36];
        BuildT6(c, s, t);

        Span<double> kg = stackalloc double[36];
        Mat6TripleProduct(t, kl, kg);

        int[] g =
        {
            layout.GetGlobalIndex(e.NodeA, DofType.UX),
            layout.GetGlobalIndex(e.NodeA, DofType.UY),
            layout.GetGlobalIndex(e.NodeA, DofType.RZ),
            layout.GetGlobalIndex(e.NodeB, DofType.UX),
            layout.GetGlobalIndex(e.NodeB, DofType.UY),
            layout.GetGlobalIndex(e.NodeB, DofType.RZ),
        };

        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
                K.Add(g[i], g[j], kg[i * 6 + j]);
        }
    }

    /// <summary>Adds equivalent nodal forces from distributed loads onto <paramref name="F"/>.</summary>
    public static void ApplyEquivalentNodalForces(
        FemProblem problem,
        EulerBeam2DElementDef e,
        UniformBeamLoad load,
        DofLayout layout,
        DenseVector F)
    {
        GetBeamData(problem, e, out double L, out double c, out double s, out _, out _, out _);
        double q = ResolveTransverseQ(load.Q, load.Direction, c, s);
        // Local transverse consistent load (+local y), see 02 MVP §4.2
        Span<double> fl = stackalloc double[6];
        fl[0] = 0;
        fl[1] = q * L / 2.0;
        fl[2] = q * L * L / 12.0;
        fl[3] = 0;
        fl[4] = q * L / 2.0;
        fl[5] = -q * L * L / 12.0;

        Span<double> t = stackalloc double[36];
        BuildT6(c, s, t);
        // F_g = T^T * f_l
        for (int i = 0; i < 6; i++)
        {
            double sum = 0;
            for (int j = 0; j < 6; j++)
                sum += t[j * 6 + i] * fl[j]; // T^T[i,j] = T[j,i]
            int gi = i switch
            {
                0 => layout.GetGlobalIndex(e.NodeA, DofType.UX),
                1 => layout.GetGlobalIndex(e.NodeA, DofType.UY),
                2 => layout.GetGlobalIndex(e.NodeA, DofType.RZ),
                3 => layout.GetGlobalIndex(e.NodeB, DofType.UX),
                4 => layout.GetGlobalIndex(e.NodeB, DofType.UY),
                _ => layout.GetGlobalIndex(e.NodeB, DofType.RZ),
            };
            F[gi] += sum;
        }
    }

    /// <summary>Tension-positive axial force from global displacements.</summary>
    public static double AxialForceFromDisplacement(
        FemProblem problem,
        EulerBeam2DElementDef e,
        DofLayout layout,
        DenseVector uFull)
    {
        GetBeamData(problem, e, out double L, out double c, out double s, out double E, out double A, out _);
        Span<double> ug = stackalloc double[6];
        GatherGlobal(e, layout, uFull, ug);
        Span<double> t = stackalloc double[36];
        BuildT6(c, s, t);
        Span<double> uLoc = stackalloc double[6];
        Mat6Vec(t, ug, uLoc);
        return E * A / L * (uLoc[3] - uLoc[0]);
    }

    /// <summary>Axial force N (tension +); end moments MA, MB (N·length); end shears VA, VB.</summary>
    public static void RecoverEndForces(
        FemProblem problem,
        EulerBeam2DElementDef e,
        DofLayout layout,
        DenseVector uFull,
        UniformBeamLoad? uniform,
        out double N,
        out double MA,
        out double VA,
        out double MB,
        out double VB)
    {
        GetBeamData(problem, e, out double L, out double c, out double s, out double E, out double A, out double Iz);

        Span<double> ul = stackalloc double[6];
        GatherGlobal(e, layout, uFull, ul);

        Span<double> t = stackalloc double[36];
        BuildT6(c, s, t);
        // u_local = T * u_global
        Span<double> uLoc = stackalloc double[6];
        Mat6Vec(t, ul, uLoc);

        double ua = uLoc[0];
        double ub = uLoc[3];
        N = E * A / L * (ub - ua);

        double w1 = uLoc[1];
        double th1 = uLoc[2];
        double w2 = uLoc[4];
        double th2 = uLoc[5];

        Span<double> kl = stackalloc double[36];
        BuildLocalStiffness(E, A, Iz, L, kl);

        Span<double> fl = stackalloc double[6];
        for (int i = 0; i < 6; i++) fl[i] = 0;
        if (uniform is not null)
        {
            double q = ResolveTransverseQ(uniform.Q, uniform.Direction, c, s);
            fl[1] = q * L / 2.0;
            fl[2] = q * L * L / 12.0;
            fl[4] = q * L / 2.0;
            fl[5] = -q * L * L / 12.0;
        }

        Span<double> ku = stackalloc double[6];
        Mat6Vec(kl, uLoc, ku);

        // Resisting nodal vectors: r = K_l u_l - f_l (bending–shear dual for FE beam; moment at node ~ dual to θ dof)
        double ryA = ku[1] - fl[1];
        double mA = ku[2] - fl[2];
        double ryB = ku[4] - fl[4];
        double mB = ku[5] - fl[5];

        // Map FE nodal equilibrium to engineering V, M (vertical reaction at A + upward +): VA = ryA, VB = ryB
        VA = ryA;
        VB = ryB;
        MA = mA;
        MB = mB;
    }

    private static double ResolveTransverseQ(double Q, BeamLoadDirection direction, double c, double s)
    {
        switch (direction)
        {
            case BeamLoadDirection.LocalPerpendicular:
                return Q;
            case BeamLoadDirection.GlobalNegY:
                // Global distributed (0, -Q) per length; local +y unit in GCS = (-s, c)
                return -Q * c;
            default:
                throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
        }
    }

    private static void GetBeamData(
        FemProblem problem,
        EulerBeam2DElementDef e,
        out double L,
        out double c,
        out double s,
        out double E,
        out double A,
        out double I)
    {
        var na = GetNode(problem, e.NodeA);
        var nb = GetNode(problem, e.NodeB);
        var v = na.Position.VectorTo(nb.Position);
        L = v.Length;
        if (L < 1e-30)
            throw new InvalidOperationException($"Euler beam element {e.Id.Value}: zero length.");
        var vn = v.Normalize();
        c = vn.X;
        s = vn.Y;

        var mat = problem.Materials[e.MaterialId];
        var sec = problem.Sections[e.SectionId];
        if (mat is not LinearElasticMaterial le)
            throw new InvalidOperationException("EulerBeam2D requires LinearElasticMaterial.");
        if (sec is not BeamSection bm)
            throw new InvalidOperationException("EulerBeam2D requires BeamSection.");
        E = le.YoungsModulus;
        A = bm.Area;
        I = bm.MomentOfInertiaZ;
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

    private static void GatherGlobal(EulerBeam2DElementDef e, DofLayout layout, DenseVector uFull, Span<double> u6)
    {
        u6[0] = uFull[layout.GetGlobalIndex(e.NodeA, DofType.UX)];
        u6[1] = uFull[layout.GetGlobalIndex(e.NodeA, DofType.UY)];
        u6[2] = uFull[layout.GetGlobalIndex(e.NodeA, DofType.RZ)];
        u6[3] = uFull[layout.GetGlobalIndex(e.NodeB, DofType.UX)];
        u6[4] = uFull[layout.GetGlobalIndex(e.NodeB, DofType.UY)];
        u6[5] = uFull[layout.GetGlobalIndex(e.NodeB, DofType.RZ)];
    }

    /// <summary>6×6 stiffness in local (ux uy rz)_A (ux uy rz)_B order.</summary>
    private static void BuildLocalStiffness(double E, double A, double Iz, double L, Span<double> k)
    {
        for (int i = 0; i < 36; i++) k[i] = 0;
        double kAx = E * A / L;
        // Axial dofs 0 and 3
        k[0 * 6 + 0] += kAx;
        k[0 * 6 + 3] += -kAx;
        k[3 * 6 + 0] += -kAx;
        k[3 * 6 + 3] += kAx;

        double EI = E * Iz;
        double a = EI / (L * L * L);
        // Hermite bending for dofs 1,2,4,5
        double L2 = L * L;
        // From [12 6L -12 6L; 6L 4L² -6L 2L²; ...] * EI/L³
        double[,] hb =
        {
            { 12, 6 * L, -12, 6 * L },
            { 6 * L, 4 * L2, -6 * L, 2 * L2 },
            { -12, -6 * L, 12, -6 * L },
            { 6 * L, 2 * L2, -6 * L, 4 * L2 },
        };
        int[] bi = { 1, 2, 4, 5 };
        for (int ii = 0; ii < 4; ii++)
        {
            for (int jj = 0; jj < 4; jj++)
            {
                int gi = bi[ii];
                int gj = bi[jj];
                k[gi * 6 + gj] += a * hb[ii, jj];
            }
        }
    }

    /// <summary>Per-node T: u_loc = T3 * u_g; stacks to 6×6.</summary>
    private static void BuildT6(double c, double s, Span<double> t)
    {
        for (int i = 0; i < 36; i++) t[i] = 0;
        SetRotationBlock3(t, 0, 0, c, s);
        SetRotationBlock3(t, 3, 3, c, s);
    }

    private static void SetRotationBlock3(Span<double> t, int offR, int offC, double cc, double ss)
    {
        t[(offR + 0) * 6 + offC + 0] = cc;
        t[(offR + 0) * 6 + offC + 1] = ss;
        t[(offR + 1) * 6 + offC + 0] = -ss;
        t[(offR + 1) * 6 + offC + 1] = cc;
        t[(offR + 2) * 6 + offC + 2] = 1.0;
    }

    private static void Mat6Vec(ReadOnlySpan<double> m, ReadOnlySpan<double> v, Span<double> o)
    {
        for (int i = 0; i < 6; i++)
        {
            double s0 = 0;
            for (int j = 0; j < 6; j++)
                s0 += m[i * 6 + j] * v[j];
            o[i] = s0;
        }
    }

    /// <summary>kg = Tᵀ * kl * T (column-major flat row-major same indexing)</summary>
    private static void Mat6TripleProduct(ReadOnlySpan<double> t, ReadOnlySpan<double> kl, Span<double> kg)
    {
        Span<double> tmp = stackalloc double[36];
        // tmp = kl * T
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                double s0 = 0;
                for (int k = 0; k < 6; k++)
                    s0 += kl[i * 6 + k] * t[k * 6 + j];
                tmp[i * 6 + j] = s0;
            }
        }
        // kg = Tᵀ * tmp
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                double s0 = 0;
                for (int k = 0; k < 6; k++)
                    s0 += t[k * 6 + i] * tmp[k * 6 + j];
                kg[i * 6 + j] = s0;
            }
        }
    }
}
