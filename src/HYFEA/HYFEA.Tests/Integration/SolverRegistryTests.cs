using System;
using HYFEA.Core.Analysis;
using HYFEA.Core.Boundary;
using HYFEA.Core.Dofs;
using HYFEA.Core.Loads;
using HYFEA.Core.Materials;
using HYFEA.Core.Model;
using HYFEA.Core.Sections;
using HYFEA.Core.Solvers;
using Xunit;

namespace HYFEA.Tests.Integration;

/// <summary>
/// P2.v0.2: SolverRegistry 与 ILinearSystemSolver 切换测试。
/// </summary>
public class SolverRegistryTests
{
    /// <summary>
    /// 注册表基础行为:默认求解器、按键获取、切换默认。
    /// </summary>
    [Fact]
    public void Registry_default_get_and_switch()
    {
        var registry = new SolverRegistry();

        // 默认 csparse (Track A)
        Assert.Equal("CSparse.NET Cholesky", registry.Default.Name);
        Assert.True(registry.Default.SupportsSparse);

        // 按键获取
        var dense = registry.Get("dense_gauss");
        Assert.Equal("Dense Gauss Elimination (Track B)", dense.Name);
        Assert.False(dense.SupportsSparse);

        // 切换默认
        registry.SetDefault("dense_gauss");
        Assert.Equal("Dense Gauss Elimination (Track B)", registry.Default.Name);

        // 未注册键抛异常
        Assert.Throws<ArgumentException>(() => registry.Get("nonexistent"));
    }

    /// <summary>
    /// G11: 悬臂梁 — 同一问题在 csparse / dense_gauss 两个求解器下结果一致(相对误差 <1e-9)。
    /// </summary>
    [Fact]
    public void G11_cantilever_solver_switch_results_match()
    {
        var problem = BuildCantilever();
        var registry = new SolverRegistry();

        var resultSparse = new LinearStaticAnalysis(registry.Get("csparse")).Run(problem);
        var resultDense = new LinearStaticAnalysis(registry.Get("dense_gauss")).Run(problem);

        Assert.True(resultSparse.Success);
        Assert.True(resultDense.Success);

        var n1 = new NodeId(1);
        double uySparse = resultSparse.Displacements!.Get(n1, DofType.UY);
        double uyDense = resultDense.Displacements!.Get(n1, DofType.UY);

        Assert.True(Math.Abs(uySparse - uyDense) < Math.Abs(uyDense) * 1e-9,
            $"uy mismatch: sparse={uySparse}, dense={uyDense}");

        double rzSparse = resultSparse.Displacements.Get(n1, DofType.RZ);
        double rzDense = resultDense.Displacements.Get(n1, DofType.RZ);

        Assert.True(Math.Abs(rzSparse - rzDense) < Math.Abs(rzDense) * 1e-9,
            $"rz mismatch: sparse={rzSparse}, dense={rzDense}");

        // 反力一致
        var n0 = new NodeId(0);
        double rySparse = resultSparse.Reactions!.Get(n0, DofType.UY);
        double ryDense = resultDense.Reactions!.Get(n0, DofType.UY);
        Assert.True(Math.Abs(rySparse - ryDense) < Math.Abs(ryDense) * 1e-9);
    }

    /// <summary>
    /// WithSolver 便捷构造 + 解析解验证 (悬臂 δ = PL³/3EI)。
    /// </summary>
    [Fact]
    public void WithSolver_factory_matches_analytical()
    {
        var problem = BuildCantilever();

        const double L = 10000.0;
        const double E = 2.06e5;
        const double Iz = 1.067e9;
        const double P = 1000.0;
        double uyExpected = -P * L * L * L / (3.0 * E * Iz);

        foreach (var key in new[] { "csparse", "dense_gauss" })
        {
            var result = LinearStaticAnalysis.WithSolver(key).Run(problem);
            Assert.True(result.Success, $"{key} should succeed");

            double uy = result.Displacements!.Get(new NodeId(1), DofType.UY);
            Assert.True(Math.Abs(uy - uyExpected) < Math.Abs(uyExpected) * 1e-6,
                $"{key}: uy={uy}, expected={uyExpected}");
        }
    }

    /// <summary>
    /// SparseLinearStaticAnalysis 拒绝不支持稀疏的求解器。
    /// </summary>
    [Fact]
    public void SparseAnalysis_rejects_dense_only_solver()
    {
        Assert.Throws<ArgumentException>(() =>
            new SparseLinearStaticAnalysis(new DenseSystemSolver()));
    }

    /// <summary>悬臂梁 MmN:L=10m,端部 -1kN。</summary>
    private static FemProblem BuildCantilever()
    {
        var n0 = new NodeId(0);
        var n1 = new NodeId(1);
        var mid = new MaterialId(1);
        var sid = new SectionId(1);

        return new FemProblemBuilder()
            .WithUnits(UnitSystem.MmN)
            .AddMaterial(new LinearElasticMaterial(mid, 2.06e5, 0.3))
            .AddSection(new BeamSection(sid, 8e4, 1.067e9))
            .AddNode(n0, 0, 0)
            .AddNode(n1, 10000, 0)
            .AddEulerBeam2D(new ElementId(1), n0, n1, mid, sid)
            .AddSupport(new FixedSupport(n0, DofType.UX))
            .AddSupport(new FixedSupport(n0, DofType.UY))
            .AddSupport(new FixedSupport(n0, DofType.RZ))
            .AddLoad(new NodalLoad(n1, DofType.UY, -1000.0))
            .Build();
    }
}
