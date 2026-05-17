using FluentAssertions;
using HYFEA.Core.Boundary;
using HYFEA.Core.Dofs;
using HYFEA.Core.Loads;
using HYFEA.Core.Materials;
using HYFEA.Core.Model;
using HYFEA.Core.Sections;
using HYFEA.Core.Validation;
using Xunit;

namespace HYFEA.Tests.GoldenCases;

public sealed class G01_G02_SpringTests
{
    private static readonly Tolerance Tol = new(
        DisplacementRelative: 1e-9,
        ReactionAbsolute: 1e-9,
        AxialRelative: 1e-8);

    [Fact]
    public void G01_single_axial_spring()
    {
        var n0 = new NodeId(0);
        var n1 = new NodeId(1);
        var problem = new FemProblemBuilder()
            .AddNode(n0, 0, 0)
            .AddNode(n1, 1, 0)
            .AddMaterial(new LinearElasticMaterial(new MaterialId(1), 1000, 0.3))
            .AddSection(new AxialSection(new SectionId(1), 1))
            .AddSpring1D(new ElementId(1), n0, n1, new MaterialId(1), new SectionId(1))
            .AddSupport(new FixedSupport(n0, DofType.UX))
            .AddLoad(new NodalLoad(n1, DofType.UX, 10))
            .Build();

        var expected = new ExpectedResults(
            ExpectedDisplacements:
            [
                (n1, DofType.UX, 0.01),
            ],
            ExpectedReactions:
            [
                (n0, DofType.UX, -10),
            ],
            ExpectedAxials: [(new ElementId(1), 10)]);

        var report = GoldenCaseRunner.Run(new GoldenCase("G01", problem, expected, Tol));
        report.AllPassed.Should().BeTrue();
    }

    [Fact]
    public void G02_two_springs_in_series()
    {
        var n0 = new NodeId(0);
        var n1 = new NodeId(1);
        var n2 = new NodeId(2);
        var problem = new FemProblemBuilder()
            .AddNode(n0, 0, 0)
            .AddNode(n1, 1, 0)
            .AddNode(n2, 2, 0)
            .AddMaterial(new LinearElasticMaterial(new MaterialId(1), 1000, 0.3))
            .AddMaterial(new LinearElasticMaterial(new MaterialId(2), 2000, 0.3))
            .AddSection(new AxialSection(new SectionId(1), 1))
            .AddSpring1D(new ElementId(1), n0, n1, new MaterialId(1), new SectionId(1))
            .AddSpring1D(new ElementId(2), n1, n2, new MaterialId(2), new SectionId(1))
            .AddSupport(new FixedSupport(n0, DofType.UX))
            .AddLoad(new NodalLoad(n2, DofType.UX, 10))
            .Build();

        var expected = new ExpectedResults(
            ExpectedDisplacements:
            [
                (n1, DofType.UX, 0.01),
                (n2, DofType.UX, 0.015),
            ],
            ExpectedReactions:
            [
                (n0, DofType.UX, -10),
            ],
            ExpectedAxials:
            [
                (new ElementId(1), 10),
                (new ElementId(2), 10),
            ]);

        var report = GoldenCaseRunner.Run(new GoldenCase("G02", problem, expected, Tol));
        report.AllPassed.Should().BeTrue();
    }
}
