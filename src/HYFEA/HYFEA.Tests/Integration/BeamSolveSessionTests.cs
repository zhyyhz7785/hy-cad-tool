using FluentAssertions;
using HYFEA.Core.Dofs;
using HYFEA.Core.Loads;
using HYFEA.Core.Model;
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
}
