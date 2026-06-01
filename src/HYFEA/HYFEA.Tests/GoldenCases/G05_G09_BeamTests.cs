using System.Linq;
using FluentAssertions;
using HYFEA.Core.Analysis;
using HYFEA.Core.Boundary;
using HYFEA.Core.Dofs;
using HYFEA.Core.Loads;
using HYFEA.Core.Materials;
using HYFEA.Core.Model;
using HYFEA.Core.Results;
using HYFEA.Core.Sections;
using HYFEA.Core.Validation;
using Xunit;

namespace HYFEA.Tests.GoldenCases;

/// <summary>附录 D：Euler 梁 G05–G09，MmN（mm, N, MPa）。</summary>
public sealed class G05_G09_BeamTests
{
    private const double L = 10000.0;
    private const double E = 2.06e5;
    private const double A = 8e4;
    private const double Iz = 1.067e9;
    private static double EI => E * Iz;

    private static readonly MaterialId Mid = new(1);
    private static readonly SectionId Sid = new(1);

    private static FemProblemBuilder BaseBuilder() =>
        new FemProblemBuilder()
            .WithUnits(UnitSystem.MmN)
            .AddMaterial(new LinearElasticMaterial(Mid, E, 0.3))
            .AddSection(new BeamSection(Sid, A, Iz));

    private static Tolerance Tol => new(1e-6, 1e-3, 1e-6, MomentRelative: 1e-4, ShearRelative: 1e-4, RotationRelative: 1e-5);

    /// <summary>G05：悬臂 + 端部集中力 P（-Y）。</summary>
    [Fact]
    public void G05_cantilever_point_load_at_tip()
    {
        const double P = 1000.0;
        var n0 = new NodeId(0);
        var n1 = new NodeId(1);
        var e1 = new ElementId(1);

        var problem = BaseBuilder()
            .AddNode(n0, 0, 0)
            .AddNode(n1, L, 0)
            .AddEulerBeam2D(e1, n0, n1, Mid, Sid)
            .AddSupport(new FixedSupport(n0, DofType.UX))
            .AddSupport(new FixedSupport(n0, DofType.UY))
            .AddSupport(new FixedSupport(n0, DofType.RZ))
            .AddLoad(new NodalLoad(n1, DofType.UY, -P))
            .Build();

        double uyTip = -P * L * L * L / (3.0 * EI);
        double rzTip = -P * L * L / (2.0 * EI);

        var expected = new ExpectedResults(
            ExpectedDisplacements:
            [
                (n1, DofType.UY, uyTip),
                (n1, DofType.RZ, rzTip),
            ],
            ExpectedReactions:
            [
                (n0, DofType.UY, P),
                (n0, DofType.RZ, P * L),
            ],
            ExpectedAxials: [(e1, 0.0)]);

        var report = GoldenCaseRunner.Run(new GoldenCase("G05", problem, expected, Tol));
        report.AllPassed.Should().BeTrue();

        var chk = new LinearStaticAnalysis().Run(problem);
        var bend = chk.BeamEndForces!.Get(e1);
        bend.VA.Should().BeApproximately(P, 1e-6);
        bend.MA.Should().BeApproximately(P * L, P * L * 1e-6);
        Math.Abs(bend.VB + P).Should().BeLessThan(P * 1e-4);
    }

    /// <summary>G06：悬臂 + 全局 −Y 均布 q。</summary>
    [Fact]
    public void G06_cantilever_uniform_global_neg_y()
    {
        const double q = 10.0;
        var n0 = new NodeId(0);
        var n1 = new NodeId(1);
        var e1 = new ElementId(1);

        var problem = BaseBuilder()
            .AddNode(n0, 0, 0)
            .AddNode(n1, L, 0)
            .AddEulerBeam2D(e1, n0, n1, Mid, Sid)
            .AddSupport(new FixedSupport(n0, DofType.UX))
            .AddSupport(new FixedSupport(n0, DofType.UY))
            .AddSupport(new FixedSupport(n0, DofType.RZ))
            .AddElementLoad(new UniformBeamLoad(e1, BeamLoadDirection.GlobalNegY, q))
            .Build();

        double uyTip = -q * Math.Pow(L, 4) / (8.0 * EI);
        double rzTip = -q * Math.Pow(L, 3) / (6.0 * EI);

        var expected = new ExpectedResults(
            ExpectedDisplacements:
            [
                (n1, DofType.UY, uyTip),
                (n1, DofType.RZ, rzTip),
            ],
            ExpectedReactions:
            [
                (n0, DofType.UY, q * L),
            ],
            ExpectedAxials: [(e1, 0.0)],
            ExpectedBeamEnds:
            [
                // BeamEndValues: (N, VA, MA, VB, MB)；与 RecoverEndForces 符号约定一致（固端 MA 与残差 dual 同号）
                (e1, new BeamEndValues(0, q * L, q * L * L / 2.0, 0, 0)),
            ]);

        var report = GoldenCaseRunner.Run(new GoldenCase("G06", problem, expected, Tol));
        report.AllPassed.Should().BeTrue($"{string.Join("; ", report.Lines.Select(l => $"{l.Label} exp={l.Expected} act={l.Actual} rel={l.RelError:E3}"))}");
    }

    /// <summary>G07：简支 + 跨中集中力。</summary>
    [Fact]
    public void G07_ss_mid_point_load()
    {
        const double P = 10000.0;
        int nSeg = 4;
        var b = BaseBuilder();
        for (int i = 0; i <= nSeg; i++)
            b.AddNode(new NodeId(i), L * i / nSeg, 0);
        for (int i = 0; i < nSeg; i++)
            b.AddEulerBeam2D(new ElementId(i + 1), new NodeId(i), new NodeId(i + 1), Mid, Sid);

        var nLeft = new NodeId(0);
        var nMid = new NodeId(nSeg / 2);
        var nRight = new NodeId(nSeg);
        b.AddSupport(new FixedSupport(nLeft, DofType.UX));
        b.AddSupport(new FixedSupport(nLeft, DofType.UY));
        b.AddSupport(new FixedSupport(nRight, DofType.UX));
        b.AddSupport(new FixedSupport(nRight, DofType.UY));
        b.AddLoad(new NodalLoad(nMid, DofType.UY, -P));
        var problem = b.Build();

        double uyMid = -P * L * L * L / (48.0 * EI);
        var expected = new ExpectedResults(
            ExpectedDisplacements: [(nMid, DofType.UY, uyMid)],
            ExpectedReactions:
            [
                (nLeft, DofType.UY, P / 2.0),
                (nRight, DofType.UY, P / 2.0),
            ],
            ExpectedAxials: null);

        var report = GoldenCaseRunner.Run(new GoldenCase("G07", problem, expected, Tol));
        report.AllPassed.Should().BeTrue();

        var analysis = new LinearStaticAnalysis();
        var res = analysis.Run(problem);
        res.Success.Should().BeTrue();
        // 跨中 x=L/2 所在单元为第 (nSeg/2+1) 段：A 端即中点
        var midSpanEl = new ElementId(nSeg / 2 + 1);
        var be = res.BeamEndForces!.Get(midSpanEl);
        double mMidClosed = P * L / 4.0;
        be.MA.Should().BeApproximately(-mMidClosed, Math.Abs(mMidClosed) * 1e-3);
    }

    /// <summary>G08：简支 + 均布 q（全局 −Y）。</summary>
    [Fact]
    public void G08_ss_uniform()
    {
        const double q = 10.0;
        int nSeg = 4;
        var b = BaseBuilder();
        for (int i = 0; i <= nSeg; i++)
            b.AddNode(new NodeId(i), L * i / nSeg, 0);
        for (int i = 0; i < nSeg; i++)
        {
            var eid = new ElementId(i + 1);
            b.AddEulerBeam2D(eid, new NodeId(i), new NodeId(i + 1), Mid, Sid);
            b.AddElementLoad(new UniformBeamLoad(eid, BeamLoadDirection.GlobalNegY, q));
        }

        var nLeft = new NodeId(0);
        var nMid = new NodeId(nSeg / 2);
        var nRight = new NodeId(nSeg);
        b.AddSupport(new FixedSupport(nLeft, DofType.UX));
        b.AddSupport(new FixedSupport(nLeft, DofType.UY));
        b.AddSupport(new FixedSupport(nRight, DofType.UX));
        b.AddSupport(new FixedSupport(nRight, DofType.UY));
        var problem = b.Build();

        double uyMid = -5.0 * q * Math.Pow(L, 4) / (384.0 * EI);
        var expected = new ExpectedResults(
            ExpectedDisplacements: [(nMid, DofType.UY, uyMid)],
            ExpectedReactions:
            [
                (nLeft, DofType.UY, q * L / 2.0),
                (nRight, DofType.UY, q * L / 2.0),
            ]);

        var report = GoldenCaseRunner.Run(new GoldenCase("G08", problem, expected, Tol));
        report.AllPassed.Should().BeTrue();

        var analysis = new LinearStaticAnalysis();
        var res = analysis.Run(problem);
        var midSpanEl = new ElementId(nSeg / 2 + 1);
        var be = res.BeamEndForces!.Get(midSpanEl);
        double mMid = q * L * L / 8.0;
        Math.Abs(be.MA).Should().BeApproximately(mMid, mMid * 2e-3);
    }

    /// <summary>G09：两端固接 + 均布 q；16 段网格 + 闭式对比（容差放宽）。</summary>
    [Fact]
    public void G09_fixed_fixed_uniform()
    {
        const double q = 10.0;
        int nSeg = 16;
        var b = BaseBuilder();
        for (int i = 0; i <= nSeg; i++)
            b.AddNode(new NodeId(i), L * i / nSeg, 0);
        for (int i = 0; i < nSeg; i++)
        {
            var eid = new ElementId(i + 1);
            b.AddEulerBeam2D(eid, new NodeId(i), new NodeId(i + 1), Mid, Sid);
            b.AddElementLoad(new UniformBeamLoad(eid, BeamLoadDirection.GlobalNegY, q));
        }

        var n0 = new NodeId(0);
        var nN = new NodeId(nSeg);
        b.AddSupport(new FixedSupport(n0, DofType.UX));
        b.AddSupport(new FixedSupport(n0, DofType.UY));
        b.AddSupport(new FixedSupport(n0, DofType.RZ));
        b.AddSupport(new FixedSupport(nN, DofType.UX));
        b.AddSupport(new FixedSupport(nN, DofType.UY));
        b.AddSupport(new FixedSupport(nN, DofType.RZ));
        var problem = b.Build();

        double uyMidClosed = -q * Math.Pow(L, 4) / (384.0 * EI);
        double mEndClosed = q * L * L / 12.0;
        double mSpanClosed = q * L * L / 24.0;

        var analysis = new LinearStaticAnalysis();
        var res = analysis.Run(problem);
        res.Success.Should().BeTrue();
        var nMid = new NodeId(nSeg / 2);
        res.Displacements!.Get(nMid, DofType.UY).Should().BeApproximately(uyMidClosed, Math.Abs(uyMidClosed) * 0.02);

        var endA = res.BeamEndForces!.Get(new ElementId(1));
        Math.Abs(endA.MA).Should().BeApproximately(mEndClosed, mEndClosed * 0.02);
        var midEl = new ElementId(nSeg / 2);
        var midE = res.BeamEndForces.Get(midEl);
        Math.Abs(midE.MA + midE.MB).Should().BeLessThan(mSpanClosed * 0.1);
        Math.Max(Math.Abs(midE.MA), Math.Abs(midE.MB)).Should().BeApproximately(mSpanClosed, mSpanClosed * 0.05);
    }
}
