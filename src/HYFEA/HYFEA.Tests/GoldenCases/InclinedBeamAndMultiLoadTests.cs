using FluentAssertions;
using HYFEA.Core.Analysis;
using HYFEA.Core.Boundary;
using HYFEA.Core.Dofs;
using HYFEA.Core.Loads;
using HYFEA.Core.Materials;
using HYFEA.Core.Model;
using HYFEA.Core.Sections;
using Xunit;

namespace HYFEA.Tests.GoldenCases;

/// <summary>
/// 审查 A5 高5 / 中6：斜梁全局竖向均布荷载的轴向分量，以及多条均布荷载的内力恢复。
/// </summary>
public sealed class InclinedBeamAndMultiLoadTests
{
    private const double L = 10000.0;
    private const double E = 2.06e5;
    private const double A = 8e4;
    private const double Iz = 1.067e9;

    private static readonly MaterialId Mid = new(1);
    private static readonly SectionId Sid = new(1);

    private static FemProblemBuilder BaseBuilder() =>
        new FemProblemBuilder()
            .WithUnits(UnitSystem.MmN)
            .AddMaterial(new LinearElasticMaterial(Mid, E, 0.3))
            .AddSection(new BeamSection(Sid, A, Iz));

    [Fact]
    public void Inclined_45deg_cantilever_global_udl_has_full_vertical_reaction()
    {
        const double q = 10.0;
        double c = Math.Sqrt(2.0) / 2.0;
        var n0 = new NodeId(0);
        var n1 = new NodeId(1);
        var e1 = new ElementId(1);

        var problem = BaseBuilder()
            .AddNode(n0, 0, 0)
            .AddNode(n1, L * c, L * c)
            .AddEulerBeam2D(e1, n0, n1, Mid, Sid)
            .AddSupport(new FixedSupport(n0, DofType.UX))
            .AddSupport(new FixedSupport(n0, DofType.UY))
            .AddSupport(new FixedSupport(n0, DofType.RZ))
            .AddElementLoad(new UniformBeamLoad(e1, BeamLoadDirection.GlobalNegY, q))
            .Build();

        var res = new LinearStaticAnalysis().Run(problem);
        res.Success.Should().BeTrue();

        double ry = res.Reactions!.Get(n0, DofType.UY);
        double rx = res.Reactions.Get(n0, DofType.UX);
        ry.Should().BeApproximately(q * L, q * L * 1e-6);
        rx.Should().BeApproximately(0, 1e-6);

        var bend = res.BeamEndForces!.Get(e1);
        bend.N.Should().NotBeApproximately(0, 1e-6);
    }

    [Fact]
    public void Two_half_udls_recover_the_same_end_forces_as_one_full_udl()
    {
        const double q = 10.0;
        var n0 = new NodeId(0);
        var n1 = new NodeId(1);
        var e1 = new ElementId(1);

        var single = BaseBuilder()
            .AddNode(n0, 0, 0)
            .AddNode(n1, L, 0)
            .AddEulerBeam2D(e1, n0, n1, Mid, Sid)
            .AddSupport(new FixedSupport(n0, DofType.UX))
            .AddSupport(new FixedSupport(n0, DofType.UY))
            .AddSupport(new FixedSupport(n0, DofType.RZ))
            .AddElementLoad(new UniformBeamLoad(e1, BeamLoadDirection.GlobalNegY, q))
            .Build();

        var split = BaseBuilder()
            .AddNode(n0, 0, 0)
            .AddNode(n1, L, 0)
            .AddEulerBeam2D(e1, n0, n1, Mid, Sid)
            .AddSupport(new FixedSupport(n0, DofType.UX))
            .AddSupport(new FixedSupport(n0, DofType.UY))
            .AddSupport(new FixedSupport(n0, DofType.RZ))
            .AddElementLoad(new UniformBeamLoad(e1, BeamLoadDirection.GlobalNegY, q / 2.0))
            .AddElementLoad(new UniformBeamLoad(e1, BeamLoadDirection.GlobalNegY, q / 2.0))
            .Build();

        var analysis = new LinearStaticAnalysis();
        var a = analysis.Run(single);
        var b = analysis.Run(split);
        a.Success.Should().BeTrue();
        b.Success.Should().BeTrue();

        var ea = a.BeamEndForces!.Get(e1);
        var eb = b.BeamEndForces!.Get(e1);
        eb.VA.Should().BeApproximately(ea.VA, 1e-6);
        eb.MA.Should().BeApproximately(ea.MA, Math.Abs(ea.MA) * 1e-6 + 1e-6);
        eb.VB.Should().BeApproximately(ea.VB, 1e-6);
        eb.MB.Should().BeApproximately(ea.MB, 1e-6);
        eb.N.Should().BeApproximately(ea.N, 1e-6);
    }
}
