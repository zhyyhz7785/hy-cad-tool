using HYFEA.Core.Boundary;
using HYFEA.Core.Dofs;
using HYFEA.Core.Loads;
using HYFEA.Core.Materials;
using HYFEA.Core.Model;
using HYFEA.Core.Sections;
using Xunit;

namespace HYFEA.Tests.Unit;

public sealed class DofLayoutTests
{
    [Fact]
    public void Spring_chain_constrained_dofs_not_in_free_set()
    {
        var p = new FemProblemBuilder()
            .AddNode(new NodeId(0), 0, 0)
            .AddNode(new NodeId(1), 1, 0)
            .AddMaterial(new LinearElasticMaterial(new MaterialId(1), 1000, 0.3))
            .AddSection(new AxialSection(new SectionId(1), 1))
            .AddSpring1D(new ElementId(1), new NodeId(0), new NodeId(1), new MaterialId(1), new SectionId(1))
            .AddSupport(new FixedSupport(new NodeId(0), DofType.UX))
            .AddLoad(new NodalLoad(new NodeId(1), DofType.UX, 1))
            .Build();

        var layout = DofLayout.Build(p);
        Assert.Equal(2, layout.TotalDofCount);
        Assert.Equal(1, layout.FreeDofCount);
        Assert.Contains((new NodeId(0), DofType.UX), layout.ConstrainedPairs);
        Assert.Equal(-1, layout.GlobalToFree[layout.GetGlobalIndex(new NodeId(0), DofType.UX)]);
        Assert.NotEqual(-1, layout.GlobalToFree[layout.GetGlobalIndex(new NodeId(1), DofType.UX)]);
    }
}
