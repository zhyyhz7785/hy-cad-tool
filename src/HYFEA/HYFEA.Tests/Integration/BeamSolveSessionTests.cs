using System;
using System.Threading;
using FluentAssertions;
using HYFEA.Core.Analysis;
using HYFEA.Core.Dofs;
using HYFEA.Core.Elements;
using HYFEA.Core.Loads;
using HYFEA.Core.Model;
using HYFEA.Core.Results;
using HYFEA.Hosting;
using Xunit;

namespace HYFEA.Tests.Integration;

public sealed class BeamSolveSessionTests
{
    /// <summary>悬臂均布（对齐 G06 量级）：端部位移非 NaN 且向下。</summary>
    [Fact]
    public void Cantilever_uniform_load_tip_uy_is_finite_and_negative()
    {
        const double L = 10000.0;
        const double E = 2.06e5;
        const double A = 8e4;
        const double Iz = 1.067e9;
        const double q = 10.0;

        var geom = InMemoryAxisGeometry.Horizontal(L, segments: 4);
        var request = new BeamSolveRequest
        {
            YoungsModulus = E,
            Area = A,
            InertiaZ = Iz,
            Q = q,
            LoadDirection = BeamLoadDirection.GlobalNegY,
            EndA = BeamSupportKind.Fixed,
            EndB = BeamSupportKind.Free,
            Units = UnitSystem.MmN,
        };

        var result = new BeamSolveSession().Run(geom, request);

        result.Success.Should().BeTrue(result.Error?.Message);
        result.Displacements.Should().NotBeNull();

        var tip = new NodeId(4);
        result.Displacements!.TryGet(tip, DofType.UY, out var uy).Should().BeTrue();
        uy.Should().NotBe(double.NaN);
        uy.Should().BeNegative();

        double uyAnalytical = -q * Math.Pow(L, 4) / (8.0 * E * Iz);
        uy.Should().BeApproximately(uyAnalytical, Math.Abs(uyAnalytical) * 0.05);
    }

    [Fact]
    public void Compute_throws_when_token_already_cancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var geom = InMemoryAxisGeometry.Horizontal(10000.0, segments: 4);
        var request = new BeamSolveRequest
        {
            YoungsModulus = 2.06e5,
            Area = 8e4,
            InertiaZ = 1.067e9,
            Q = 10.0,
            LoadDirection = BeamLoadDirection.GlobalNegY,
            EndA = BeamSupportKind.Fixed,
            EndB = BeamSupportKind.Free,
            Units = UnitSystem.MmN,
        };

        Assert.ThrowsAny<OperationCanceledException>(() =>
            new BeamSolveSession().Compute(geom, request, cts.Token));
    }

    [Fact]
    public void Run_presents_via_sink_after_compute()
    {
        var geom = InMemoryAxisGeometry.Horizontal(10000.0, segments: 4);
        var request = CantileverRequest();
        var sink = new RecordingSink();

        var result = new BeamSolveSession().Run(geom, request, sink);

        result.Success.Should().BeTrue(result.Error?.Message);
        sink.Presented.Should().BeTrue();
        sink.LastResult.Should().BeSameAs(result);
        sink.LastProblem.Should().NotBeNull();
        sink.LastProblem!.Elements.Should().NotBeEmpty();
    }

    [Fact]
    public void Session_uses_injected_FemContext_registry()
    {
        var empty = new ElementContributionRegistry(registerBuiltins: false);
        var session = new BeamSolveSession(new FemContext(elements: empty));
        var geom = InMemoryAxisGeometry.Horizontal(10000.0, segments: 2);

        Action act = () => session.Compute(geom, CantileverRequest());

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*No contribution registered*");
    }

    private static BeamSolveRequest CantileverRequest() => new()
    {
        YoungsModulus = 2.06e5,
        Area = 8e4,
        InertiaZ = 1.067e9,
        Q = 10.0,
        LoadDirection = BeamLoadDirection.GlobalNegY,
        EndA = BeamSupportKind.Fixed,
        EndB = BeamSupportKind.Free,
        Units = UnitSystem.MmN,
    };

    private sealed class RecordingSink : IFemResultSink
    {
        public bool Presented { get; private set; }
        public FemResult? LastResult { get; private set; }
        public FemProblem? LastProblem { get; private set; }

        public void Present(FemResult result, FemProblem problem)
        {
            Presented = true;
            LastResult = result;
            LastProblem = problem;
        }
    }
}
