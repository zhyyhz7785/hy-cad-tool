using FluentAssertions;
using HYFEA.Core.Analysis;
using HYFEA.Core.Boundary;
using HYFEA.Core.Dofs;
using HYFEA.Core.Loads;
using HYFEA.Core.Materials;
using HYFEA.Core.Model;
using HYFEA.Core.Sections;
using HYFEA.Core.Validation;
using Xunit;

namespace HYFEA.Tests.GoldenCases;

public sealed class G03_G04_TrussTests
{
    private static readonly Tolerance Tol = new(
        DisplacementRelative: 1e-9,
        ReactionAbsolute: 1e-8,
        AxialRelative: 1e-8);

    /// <summary>Determinate triangle: base chord + two rafters; left pin, right roller (UY).</summary>
    [Fact]
    public void G03_triangle_truss_base_and_rafters()
    {
        var n0 = new NodeId(0);
        var n1 = new NodeId(1);
        var n2 = new NodeId(2);
        var problem = new FemProblemBuilder()
            .AddNode(n0, 0, 0)
            .AddNode(n1, 4, 0)
            .AddNode(n2, 2, 3)
            .AddMaterial(new LinearElasticMaterial(new MaterialId(1), 1e6, 0.3))
            .AddSection(new AxialSection(new SectionId(1), 1))
            .AddTruss2D(new ElementId(1), n0, n1, new MaterialId(1), new SectionId(1))
            .AddTruss2D(new ElementId(2), n0, n2, new MaterialId(1), new SectionId(1))
            .AddTruss2D(new ElementId(3), n1, n2, new MaterialId(1), new SectionId(1))
            .AddSupport(new FixedSupport(n0, DofType.UX))
            .AddSupport(new FixedSupport(n0, DofType.UY))
            .AddSupport(new FixedSupport(n1, DofType.UY))
            .AddLoad(new NodalLoad(n2, DofType.UY, -1000))
            .Build();

        var expected = new ExpectedResults(
            ExpectedDisplacements:
            [
                (n1, DofType.UX, 0.0013333333333333339),
                (n2, DofType.UX, 0.00066666666666666686),
                (n2, DofType.UY, -0.0030484536989462143),
            ],
            ExpectedReactions:
            [
                (n0, DofType.UY, 500),
                (n1, DofType.UY, 500),
            ],
            ExpectedAxials:
            [
                (new ElementId(1), 333.33333333333348),
                (new ElementId(2), -600.92521257733154),
                (new ElementId(3), -600.92521257733154),
            ]);

        var report = GoldenCaseRunner.Run(new GoldenCase("G03", problem, expected, Tol));
        report.AllPassed.Should().BeTrue();
    }

    [Fact]
    public void G04_symmetric_both_bases_fixed()
    {
        var n0 = new NodeId(0);
        var n1 = new NodeId(1);
        var n2 = new NodeId(2);
        var problem = new FemProblemBuilder()
            .AddNode(n0, 0, 0)
            .AddNode(n1, 4, 0)
            .AddNode(n2, 2, 3)
            .AddMaterial(new LinearElasticMaterial(new MaterialId(1), 1e6, 0.3))
            .AddSection(new AxialSection(new SectionId(1), 1))
            .AddTruss2D(new ElementId(1), n0, n1, new MaterialId(1), new SectionId(1))
            .AddTruss2D(new ElementId(2), n0, n2, new MaterialId(1), new SectionId(1))
            .AddTruss2D(new ElementId(3), n1, n2, new MaterialId(1), new SectionId(1))
            .AddSupport(new FixedSupport(n0, DofType.UX))
            .AddSupport(new FixedSupport(n0, DofType.UY))
            .AddSupport(new FixedSupport(n1, DofType.UX))
            .AddSupport(new FixedSupport(n1, DofType.UY))
            .AddLoad(new NodalLoad(n2, DofType.UY, -1000))
            .Build();

        var analysis = new LinearStaticAnalysis();
        var res = analysis.Run(problem);
        res.Success.Should().BeTrue();
        res.Displacements!.Get(n2, DofType.UX).Should().BeApproximately(0, 1e-12);
        res.Displacements.Get(n2, DofType.UY).Should().BeApproximately(-0.0026040092545017695, 1e-12);

        res.Reactions!.Get(n0, DofType.UY).Should().BeApproximately(500, 1e-8);
        res.Reactions.Get(n1, DofType.UY).Should().BeApproximately(500, 1e-8);
        res.Reactions.Get(n0, DofType.UX).Should().BeApproximately(333.33333333333337, 1e-8);
        res.Reactions.Get(n1, DofType.UX).Should().BeApproximately(-333.33333333333337, 1e-8);

        res.AxialForces!.Get(new ElementId(1)).Should().BeApproximately(0, 1e-8);
        res.AxialForces.Get(new ElementId(2)).Should().BeApproximately(-600.92521257733142, 1e-6);
        res.AxialForces.Get(new ElementId(3)).Should().BeApproximately(-600.92521257733142, 1e-6);
    }
}
