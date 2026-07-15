using System;
using System.Diagnostics;
using HYFEA.Core.Analysis;
using HYFEA.Core.Boundary;
using HYFEA.Core.Dofs;
using HYFEA.Core.Loads;
using HYFEA.Core.Materials;
using HYFEA.Core.Model;
using HYFEA.Core.Results;
using HYFEA.Core.Sections;
using HYFEA.Core.Solvers;
using Xunit;
using Xunit.Abstractions;

namespace HYFEA.Tests.Performance;

/// <summary>
/// P2.v0.3: 求解器性能基准 — csparse (Track A) vs dense_gauss (Track B)。
/// 悬臂梁长链模型,DOF 规模 ~100 / ~500 / ~1000。
/// 输出到测试日志;断言仅要求结果一致 + 大规模时稀疏不慢于稠密。
/// </summary>
public sealed class SolverBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public SolverBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(33)]    // 34 节点 × 3 DOF = 102 DOF
    [InlineData(166)]   // 167 节点 × 3 DOF = 501 DOF
    [InlineData(333)]   // 334 节点 × 3 DOF = 1002 DOF
    public void Benchmark_beam_chain_csparse_vs_dense(int elementCount)
    {
        var problem = BuildBeamChain(elementCount);
        int dofCount = 3 * (elementCount + 1);

        // 预热(JIT)
        new SparseLinearStaticAnalysis().Run(BuildBeamChain(4));
        LinearStaticAnalysis.WithSolver("dense_gauss").Run(BuildBeamChain(4));

        // Track A: 稀疏装配 + CSparse Cholesky
        var swSparse = Stopwatch.StartNew();
        var resSparse = new SparseLinearStaticAnalysis().Run(problem);
        swSparse.Stop();

        // Track B: 稠密装配 + Gauss 消元
        var swDense = Stopwatch.StartNew();
        var resDense = LinearStaticAnalysis.WithSolver("dense_gauss").Run(problem);
        swDense.Stop();

        Assert.True(resSparse.Success, "sparse solve should succeed");
        Assert.True(resDense.Success, "dense solve should succeed");

        double speedup = swDense.Elapsed.TotalMilliseconds /
                         Math.Max(swSparse.Elapsed.TotalMilliseconds, 0.001);

        _output.WriteLine($"DOF={dofCount}  elements={elementCount}");
        _output.WriteLine($"  CSparse (Track A): {swSparse.Elapsed.TotalMilliseconds:F1} ms");
        _output.WriteLine($"  DenseGauss (Track B): {swDense.Elapsed.TotalMilliseconds:F1} ms");
        _output.WriteLine($"  Speedup: {speedup:F1}x");

        // 结果一致性:自由端挠度相对误差 <1e-6。
        // 注:细分悬臂链条件数随 DOF 增长(~(L/dx)⁴),Cholesky 与 Gauss 消元顺序不同,
        // 浮点舍入差在 1000 DOF 时达 ~1e-7 相对量级,属正常数值行为;1e-9 仅适用于小模型(见 G11)。
        var tip = new NodeId(elementCount);
        double uySparse = resSparse.Displacements!.Get(tip, DofType.UY);
        double uyDense = resDense.Displacements!.Get(tip, DofType.UY);
        Assert.True(Math.Abs(uySparse - uyDense) < Math.Abs(uyDense) * 1e-6,
            $"tip uy mismatch: sparse={uySparse:E15}, dense={uyDense:E15}");

        // 与解析解对比:悬臂均布 δ = qL⁴/(8EI)
        double uyExpected = -Q * Math.Pow(TotalLength, 4) / (8.0 * E * Iz);
        Assert.True(Math.Abs(uySparse - uyExpected) < Math.Abs(uyExpected) * 1e-6,
            $"tip uy vs analytical: got={uySparse:E9}, expected={uyExpected:E9}");

        // 大规模时稀疏不应慢于稠密(留 20% 波动余量)
        if (dofCount >= 500)
        {
            Assert.True(
                swSparse.Elapsed.TotalMilliseconds < swDense.Elapsed.TotalMilliseconds * 1.2,
                $"sparse ({swSparse.Elapsed.TotalMilliseconds:F1} ms) should not be slower than dense ({swDense.Elapsed.TotalMilliseconds:F1} ms) at {dofCount} DOF");
        }
    }

    private const double TotalLength = 10000.0;  // mm
    private const double E = 2.06e5;             // MPa
    private const double A = 8e4;                // mm²
    private const double Iz = 1.067e9;           // mm⁴
    private const double Q = 0.01;               // N/mm 均布(全局 -Y)

    /// <summary>悬臂梁长链(MmN):固定端在 node 0,均布荷载覆盖所有单元。</summary>
    private static FemProblem BuildBeamChain(int elementCount)
    {
        var mid = new MaterialId(1);
        var sid = new SectionId(1);
        var b = new FemProblemBuilder()
            .WithUnits(UnitSystem.MmN)
            .AddMaterial(new LinearElasticMaterial(mid, E, 0.3))
            .AddSection(new BeamSection(sid, A, Iz));

        double dx = TotalLength / elementCount;
        for (int i = 0; i <= elementCount; i++)
            b.AddNode(new NodeId(i), i * dx, 0);

        for (int i = 0; i < elementCount; i++)
        {
            var eid = new ElementId(i + 1);
            b.AddEulerBeam2D(eid, new NodeId(i), new NodeId(i + 1), mid, sid);
            b.AddElementLoad(new UniformBeamLoad(eid, BeamLoadDirection.GlobalNegY, Q));
        }

        b.AddSupport(new FixedSupport(new NodeId(0), DofType.UX));
        b.AddSupport(new FixedSupport(new NodeId(0), DofType.UY));
        b.AddSupport(new FixedSupport(new NodeId(0), DofType.RZ));

        return b.Build();
    }
}
