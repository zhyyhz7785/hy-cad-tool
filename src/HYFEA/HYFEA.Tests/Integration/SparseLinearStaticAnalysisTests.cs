using HYFEA.Core.Analysis;
using HYFEA.Core.Boundary;
using HYFEA.Core.Dofs;
using HYFEA.Core.Loads;
using HYFEA.Core.Materials;
using HYFEA.Core.Model;
using HYFEA.Core.Sections;
using System;
using Xunit;

namespace HYFEA.Tests.Integration;

/// <summary>
/// P2.v0.1 Track A: CSparse.NET 稀疏求解器集成测试。
/// </summary>
public class SparseLinearStaticAnalysisTests
{
    /// <summary>
    /// G10a: 简支梁跨中集中荷载 - 稀疏求解器与稠密求解器结果对比。
    /// </summary>
    [Fact]
    public void G10a_SimpleBeam_SparseVsDense()
    {
        // 模型: L=6m 简支梁, 跨中 -10kN
        var n0 = new NodeId(0);
        var n1 = new NodeId(1);
        var n2 = new NodeId(2);
        var e1 = new ElementId(1);
        var e2 = new ElementId(2);
        var mid = new MaterialId(1);
        var sid = new SectionId(1);

        var problem = new FemProblemBuilder()
            .WithUnits(UnitSystem.SI)
            .AddNode(n0, 0, 0)
            .AddNode(n1, 3, 0)
            .AddNode(n2, 6, 0)
            .AddMaterial(new LinearElasticMaterial(mid, 210e9, 0.3))
            .AddSection(new BeamSection(sid, 0.15, 0.003125))  // A=0.3*0.5=0.15, Iz=0.3*0.5^3/12
            .AddEulerBeam2D(e1, n0, n1, mid, sid)
            .AddEulerBeam2D(e2, n1, n2, mid, sid)
            .AddSupport(new FixedSupport(n0, DofType.UX))
            .AddSupport(new FixedSupport(n0, DofType.UY))
            .AddSupport(new FixedSupport(n2, DofType.UY))
            .AddLoad(new NodalLoad(n1, DofType.UY, -10000))
            .Build();

        // Solve with dense
        var denseSolver = new LinearStaticAnalysis();
        var denseResult = denseSolver.Run(problem);
        Assert.True(denseResult.Success, "Dense solver should succeed");

        // Solve with sparse
        var sparseSolver = new SparseLinearStaticAnalysis();
        var sparseResult = sparseSolver.Run(problem);
        Assert.True(sparseResult.Success, "Sparse solver should succeed");

        // Compare mid-span deflection
        var dense_uY = denseResult.Displacements!.Get(n1, DofType.UY);
        var sparse_uY = sparseResult.Displacements!.Get(n1, DofType.UY);

        Assert.Equal(dense_uY, sparse_uY, 6);  // 相对误差 1e-6
    }

    /// <summary>
    /// G10b: 三角形桁架 - 稀疏求解器压力测试（更大规模）。
    /// </summary>
    [Fact]
    public void G10b_TriangleTruss_Sparse()
    {
        var n0 = new NodeId(0);
        var n1 = new NodeId(1);
        var n2 = new NodeId(2);
        var e1 = new ElementId(1);
        var e2 = new ElementId(2);
        var e3 = new ElementId(3);
        var mid = new MaterialId(1);
        var sid = new SectionId(1);

        var problem = new FemProblemBuilder()
            .WithUnits(UnitSystem.SI)
            .AddNode(n0, 0, 0)
            .AddNode(n1, 4, 0)
            .AddNode(n2, 2, 3)
            .AddMaterial(new LinearElasticMaterial(mid, 200e9, 0.3))
            .AddSection(new AxialSection(sid, 0.01))  // A=100 cm²
            .AddTruss2D(e1, n0, n1, mid, sid)
            .AddTruss2D(e2, n1, n2, mid, sid)
            .AddTruss2D(e3, n2, n0, mid, sid)
            .AddSupport(new FixedSupport(n0, DofType.UX))
            .AddSupport(new FixedSupport(n0, DofType.UY))
            .AddSupport(new FixedSupport(n1, DofType.UY))
            .AddLoad(new NodalLoad(n2, DofType.UY, -50000))
            .Build();

        var solver = new SparseLinearStaticAnalysis();
        var result = solver.Run(problem);
        Assert.True(result.Success, "Sparse solver should succeed");

        var uY = result.Displacements!.Get(n2, DofType.UY);

        // 预期竖向位移 < 0
        Assert.True(uY < 0);
        Assert.True(Math.Abs(uY) > 1e-6);  // 有实际变形
    }
}
