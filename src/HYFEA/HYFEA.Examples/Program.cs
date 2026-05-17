using HYFEA.Core.Analysis;
using HYFEA.Core.Boundary;
using HYFEA.Core.Dofs;
using HYFEA.Core.Loads;
using HYFEA.Core.Materials;
using HYFEA.Core.Model;
using HYFEA.Core.Sections;

namespace HYFEA.Examples;

internal static class Program
{
    private static void Main()
    {
        DemoTrussTriangle();
        Console.WriteLine();
        DemoSymmetricBothFixed();
    }

    /// <summary>
    /// Statically determinate plane truss: base + two rafters. (0,0) pin, (4,0) roller (UY), apex (2,3), vertical load.
    /// </summary>
    private static void DemoTrussTriangle()
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

        var res = new LinearStaticAnalysis().Run(problem);
        if (!res.Success)
        {
            Console.WriteLine("Failed: " + res.Error?.Message);
            return;
        }

        Console.WriteLine("Triangle truss (G03-style with base chord):");
        foreach (var id in new[] { n0, n1, n2 })
        {
            Console.WriteLine(
                $"  Node {id.Value}: ux={res.Displacements!.Get(id, DofType.UX):G17} uy={res.Displacements.Get(id, DofType.UY):G17}");
        }
        Console.WriteLine("Reactions (constrained):");
        foreach (var pair in res.Reactions!.AsReadOnly())
            Console.WriteLine($"  Node {pair.Key.Node.Value} {pair.Key.Dof}: {pair.Value:G17}");
        Console.WriteLine("Axial forces (tension +):");
        foreach (var kv in res.AxialForces!.AsReadOnly())
            Console.WriteLine($"  Element {kv.Key.Value}: N={kv.Value:G17}");
    }

    private static void DemoSymmetricBothFixed()
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

        var res = new LinearStaticAnalysis().Run(problem);
        if (!res.Success)
        {
            Console.WriteLine("G04 Failed: " + res.Error?.Message);
            return;
        }
        Console.WriteLine("G04 Both bases fixed:");
        foreach (var id in new[] { n0, n1, n2 })
        {
            Console.WriteLine(
                $"  Node {id.Value}: ux={res.Displacements!.Get(id, DofType.UX):G17} uy={res.Displacements.Get(id, DofType.UY):G17}");
        }
        foreach (var pair in res.Reactions!.AsReadOnly())
            Console.WriteLine($"  R Node {pair.Key.Node.Value} {pair.Key.Dof}: {pair.Value:G17}");
        foreach (var kv in res.AxialForces!.AsReadOnly())
            Console.WriteLine($"  N Element {kv.Key.Value}: {kv.Value:G17}");
    }
}
