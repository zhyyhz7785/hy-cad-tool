using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HYFEA.Core.Analysis;
using HYFEA.Core.Assembly;
using HYFEA.Core.Boundary;
using HYFEA.Core.Dofs;
using HYFEA.Core.Elements;
using HYFEA.Core.LinearAlgebra;
using HYFEA.Core.Loads;
using HYFEA.Core.Materials;
using HYFEA.Core.Model;
using HYFEA.Core.Sections;
using Xunit;

namespace HYFEA.Tests.Unit;

public sealed class ElementContributionRegistryTests
{
    [Fact]
    public void Empty_registry_is_rejected_by_layout_and_both_analyses()
    {
        var problem = BuildSpring();
        var empty = new ElementContributionRegistry(registerBuiltins: false);

        Assert.Throws<NotSupportedException>(() => DofLayout.Build(problem, empty));
        Assert.Throws<NotSupportedException>(() => new LinearStaticAnalysis(elements: empty).Run(problem));
        Assert.Throws<NotSupportedException>(() => new SparseLinearStaticAnalysis(elements: empty).Run(problem));
    }

    [Fact]
    public void Injected_registry_is_resolved_instead_of_builtins()
    {
        var problem = BuildSpring();
        var registry = new ElementContributionRegistry(registerBuiltins: false);
        registry.Register(new MarkerContribution());

        var dense = Assert.Throws<InvalidOperationException>(() => new LinearStaticAnalysis(elements: registry).Run(problem));
        Assert.Equal("injected-registry", dense.Message);

        var sparse = Assert.Throws<InvalidOperationException>(() => new SparseLinearStaticAnalysis(elements: registry).Run(problem));
        Assert.Equal("injected-registry", sparse.Message);
    }

    [Fact]
    public void FemContext_is_used_by_both_analyses()
    {
        var problem = BuildSpring();
        var registry = new ElementContributionRegistry(registerBuiltins: false);
        registry.Register(new MarkerContribution());
        var ctx = new FemContext(elements: registry);

        var dense = Assert.Throws<InvalidOperationException>(() => new LinearStaticAnalysis(ctx).Run(problem));
        Assert.Equal("injected-registry", dense.Message);

        var sparse = Assert.Throws<InvalidOperationException>(() => new SparseLinearStaticAnalysis(ctx).Run(problem));
        Assert.Equal("injected-registry", sparse.Message);
    }

    private sealed class MarkerContribution : IElementContribution
    {
        public Type ElementDefinitionType => typeof(Spring1DElementDef);

        public IReadOnlyList<DofType> ActiveDofsForNode(FemElementDefinition element, NodeId node) =>
            new[] { DofType.UX };

        public void ApplyStiffness(FemProblem problem, FemElementDefinition element, DofLayout layout, IMatrixAccumulator K) =>
            throw new InvalidOperationException("injected-registry");

        public void ApplyEquivalentLoads(
            FemProblem problem,
            FemElementDefinition element,
            IReadOnlyList<IElementLoad> elementLoads,
            DofLayout layout,
            DenseVector F)
        {
        }

        public object Recover(FemProblem problem, FemElementDefinition element, DofLayout layout, DenseVector uFull) => 0.0;
    }

    [Fact]
    public void Core_new_ElementContributionRegistry_only_at_default_factories()
    {
        var coreDir = Path.Combine(RepoRoot(), "src", "HYFEA", "HYFEA.Core");
        Assert.True(Directory.Exists(coreDir), coreDir);

        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "Elements/ElementContributionRegistry.cs",
        };

        var extra = new List<string>();
        foreach (var file in Directory.GetFiles(coreDir, "*.cs", SearchOption.AllDirectories))
        {
            var rel = file.Substring(coreDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("new ElementContributionRegistry("))
                    continue;
                if (!allowed.Contains(rel))
                    extra.Add(rel + ":" + (i + 1));
            }
        }

        Assert.True(extra.Count == 0,
            "Core 内 new ElementContributionRegistry 只允许默认工厂，请改为注入：\n" + string.Join("\n", extra));
    }

    private static FemProblem BuildSpring()
    {
        return new FemProblemBuilder()
            .AddNode(new NodeId(0), 0, 0)
            .AddNode(new NodeId(1), 1, 0)
            .AddMaterial(new LinearElasticMaterial(new MaterialId(1), 1000, 0.3))
            .AddSection(new AxialSection(new SectionId(1), 1))
            .AddSpring1D(new ElementId(1), new NodeId(0), new NodeId(1), new MaterialId(1), new SectionId(1))
            .AddSupport(new FixedSupport(new NodeId(0), DofType.UX))
            .AddLoad(new NodalLoad(new NodeId(1), DofType.UX, 1))
            .Build();
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HyCADtoolGpt.sln")))
            dir = dir.Parent;
        if (dir == null)
            throw new InvalidOperationException("找不到 HyCADtoolGpt.sln");
        return dir.FullName;
    }
}
