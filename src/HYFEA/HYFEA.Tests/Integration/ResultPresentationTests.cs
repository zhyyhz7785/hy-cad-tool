using System.IO;
using FluentAssertions;
using HYFEA.Core.Loads;
using HYFEA.Core.Model;
using HYFEA.Hosting;
using HYFEA.Viz;
using Xunit;

namespace HYFEA.Tests.Integration;

public sealed class ResultPresentationTests
{
    [Fact]
    public void Build_writes_json_and_vtu_from_successful_solve()
    {
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

        var output = new BeamSolveSession().Compute(geom, request);
        output.Result.Success.Should().BeTrue(output.Result.Error?.Message);

        var path = Path.Combine(Path.GetTempPath(), "hyfea-presentation-test.vtu");
        if (File.Exists(path))
            File.Delete(path);

        var presentation = ResultPresentation.Build(output.Problem, output.Result, path);

        presentation.Mesh.Should().NotBeNull();
        presentation.MeshJson.Should().Contain("\"points\":[");
        presentation.VtuPath.Should().Be(path);
        File.Exists(path).Should().BeTrue();
    }
}
