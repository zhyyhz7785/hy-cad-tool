using FluentAssertions;
using HYFEA.Core.Analysis;
using HYFEA.Core.Boundary;
using HYFEA.Core.Dofs;
using HYFEA.Core.Loads;
using HYFEA.Core.Materials;
using HYFEA.Core.Model;
using HYFEA.Core.Sections;
using Xunit;

namespace HYFEA.Tests.Unit;

/// <summary>
/// P1.v0.3: Unit system consistency tests — SI ↔ MmN (see 02 §6, 03 §2.2).
/// Verifies that the same physical problem yields equivalent results when modeled in different unit systems.
/// </summary>
public sealed class UnitSystemConsistencyTests
{
    private static readonly MaterialId Mid = new(1);
    private static readonly SectionId Sid = new(1);

    /// <summary>
    /// Cantilever beam with uniform load: SI (m, N, Pa) vs MmN (mm, N, MPa).
    /// Physical setup: L=10m, E=206 GPa, A=0.008 m², Iz=8.889e-4 m⁴, q=10 N/m (global -Y).
    /// </summary>
    [Fact]
    public void Cantilever_uniform_load_SI_vs_MmN_consistent_after_conversion()
    {
        // === MmN system ===
        // Geometry: mm
        // Material: E in MPa (2.06e5 MPa = 206 GPa)
        // Section: A in mm², Iz in mm⁴
        // Load: q in N/mm (0.01 N/mm = 10 N/m)
        var n0 = new NodeId(0);
        var n1 = new NodeId(1);
        var e1 = new ElementId(1);

        var probMm = new FemProblemBuilder()
            .WithUnits(UnitSystem.MmN)
            .AddMaterial(new LinearElasticMaterial(Mid, 2.06e5, 0.3))  // MPa
            .AddSection(new BeamSection(Sid, 8e4, 8.889e8))            // mm², mm⁴
            .AddNode(n0, 0, 0)
            .AddNode(n1, 10000, 0)  // 10000 mm = 10 m
            .AddEulerBeam2D(e1, n0, n1, Mid, Sid)
            .AddSupport(new FixedSupport(n0, DofType.UX))
            .AddSupport(new FixedSupport(n0, DofType.UY))
            .AddSupport(new FixedSupport(n0, DofType.RZ))
            .AddElementLoad(new UniformBeamLoad(e1, BeamLoadDirection.GlobalNegY, 0.01))  // N/mm
            .Build();

        // === SI system ===
        // Geometry: m
        // Material: E in Pa (2.06e11 Pa = 206 GPa)
        // Section: A in m², Iz in m⁴
        // Load: q in N/m
        var probSi = new FemProblemBuilder()
            .WithUnits(UnitSystem.SI)
            .AddMaterial(new LinearElasticMaterial(Mid, 2.06e11, 0.3))  // Pa
            .AddSection(new BeamSection(Sid, 0.008, 8.889e-4))          // m², m⁴
            .AddNode(n0, 0, 0)
            .AddNode(n1, 10, 0)  // 10 m
            .AddEulerBeam2D(e1, n0, n1, Mid, Sid)
            .AddSupport(new FixedSupport(n0, DofType.UX))
            .AddSupport(new FixedSupport(n0, DofType.UY))
            .AddSupport(new FixedSupport(n0, DofType.RZ))
            .AddElementLoad(new UniformBeamLoad(e1, BeamLoadDirection.GlobalNegY, 10.0))  // N/m
            .Build();

        // Solve both
        var solMm = new LinearStaticAnalysis().Run(probMm);
        var solSi = new LinearStaticAnalysis().Run(probSi);

        solMm.Success.Should().BeTrue("MmN solve should succeed");
        solSi.Success.Should().BeTrue("SI solve should succeed");

        // === Compare displacements (convert SI to mm for comparison) ===
        double uyMm = solMm.Displacements!.Get(n1, DofType.UY);        // mm
        double uySi = solSi.Displacements!.Get(n1, DofType.UY);        // m
        double uySiInMm = uySi * 1000;  // m → mm

        Math.Abs(uyMm - uySiInMm).Should().BeLessThan(Math.Abs(uyMm) * 1e-9,
            "tip displacement uy should match after unit conversion (relative error < 1e-9)");

        // === Compare rotations (dimensionless, should match directly) ===
        double rzMm = solMm.Displacements.Get(n1, DofType.RZ);         // radians
        double rzSi = solSi.Displacements.Get(n1, DofType.RZ);         // radians

        Math.Abs(rzMm - rzSi).Should().BeLessThan(Math.Abs(rzMm) * 1e-9,
            "tip rotation rz should match directly (dimensionless, relative error < 1e-9)");

        // === Compare moments (convert SI to N·mm for comparison) ===
        var beamMm = solMm.BeamEndForces!.Get(e1);
        var beamSi = solSi.BeamEndForces!.Get(e1);

        double maMm = beamMm.MA;            // N·mm
        double maSi = beamSi.MA;            // N·m
        double maSiInMm = maSi * 1000;      // N·m → N·mm

        Math.Abs(maMm - maSiInMm).Should().BeLessThan(Math.Abs(maMm) * 1e-9,
            "fixed-end moment MA should match after unit conversion (relative error < 1e-9)");

        // === Compare axial forces (dimensionless in N, should match directly) ===
        double nMm = beamMm.N;  // N
        double nSi = beamSi.N;  // N

        Math.Abs(nMm - nSi).Should().BeLessThan(1e-6,
            "axial force N should be ~0 and match (absolute error < 1e-6 N)");

        // === Compare shear forces (both in N, should match directly) ===
        double vaMm = beamMm.VA;  // N
        double vaSi = beamSi.VA;  // N

        Math.Abs(vaMm - vaSi).Should().BeLessThan(Math.Abs(vaMm) * 1e-9,
            "shear force VA should match directly (relative error < 1e-9)");
    }

    /// <summary>
    /// Simply-supported beam with mid-point load: SI vs MmN consistency check.
    /// Physical setup: L=8m, E=200 GPa, A=0.01 m², Iz=1e-3 m⁴, P=5000 N at mid-point.
    /// </summary>
    [Fact]
    public void SimplySupportedBeam_midpoint_load_SI_vs_MmN_consistent()
    {
        var n0 = new NodeId(0);
        var n1 = new NodeId(1);
        var n2 = new NodeId(2);
        var e1 = new ElementId(1);
        var e2 = new ElementId(2);

        const double P = 5000.0;  // N (same in both systems)

        // === MmN: L=8000mm, E=200000 MPa, A=1e4 mm², Iz=1e9 mm⁴ ===
        var probMm = new FemProblemBuilder()
            .WithUnits(UnitSystem.MmN)
            .AddMaterial(new LinearElasticMaterial(Mid, 2e5, 0.3))
            .AddSection(new BeamSection(Sid, 1e4, 1e9))
            .AddNode(n0, 0, 0)
            .AddNode(n1, 4000, 0)  // mid-point
            .AddNode(n2, 8000, 0)
            .AddEulerBeam2D(e1, n0, n1, Mid, Sid)
            .AddEulerBeam2D(e2, n1, n2, Mid, Sid)
            .AddSupport(new FixedSupport(n0, DofType.UX))
            .AddSupport(new FixedSupport(n0, DofType.UY))
            .AddSupport(new FixedSupport(n2, DofType.UY))
            .AddLoad(new NodalLoad(n1, DofType.UY, -P))
            .Build();

        // === SI: L=8m, E=2e11 Pa, A=0.01 m², Iz=1e-3 m⁴ ===
        var probSi = new FemProblemBuilder()
            .WithUnits(UnitSystem.SI)
            .AddMaterial(new LinearElasticMaterial(Mid, 2e11, 0.3))
            .AddSection(new BeamSection(Sid, 0.01, 1e-3))
            .AddNode(n0, 0, 0)
            .AddNode(n1, 4, 0)
            .AddNode(n2, 8, 0)
            .AddEulerBeam2D(e1, n0, n1, Mid, Sid)
            .AddEulerBeam2D(e2, n1, n2, Mid, Sid)
            .AddSupport(new FixedSupport(n0, DofType.UX))
            .AddSupport(new FixedSupport(n0, DofType.UY))
            .AddSupport(new FixedSupport(n2, DofType.UY))
            .AddLoad(new NodalLoad(n1, DofType.UY, -P))
            .Build();

        var solMm = new LinearStaticAnalysis().Run(probMm);
        var solSi = new LinearStaticAnalysis().Run(probSi);

        solMm.Success.Should().BeTrue();
        solSi.Success.Should().BeTrue();

        // Mid-point displacement
        double uyMidMm = solMm.Displacements!.Get(n1, DofType.UY);
        double uyMidSi = solSi.Displacements!.Get(n1, DofType.UY) * 1000;

        Math.Abs(uyMidMm - uyMidSi).Should().BeLessThan(Math.Abs(uyMidMm) * 1e-9,
            "mid-point displacement should match after conversion");

        // Support reactions
        double ry0Mm = solMm.Reactions!.Get(n0, DofType.UY);
        double ry0Si = solSi.Reactions!.Get(n0, DofType.UY);

        Math.Abs(ry0Mm - ry0Si).Should().BeLessThan(Math.Abs(ry0Mm) * 1e-9,
            "support reaction should match (both in N)");
    }
}
