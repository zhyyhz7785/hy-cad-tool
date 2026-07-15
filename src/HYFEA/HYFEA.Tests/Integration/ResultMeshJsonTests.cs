using FluentAssertions;
using HYFEA.Core.Loads;
using HYFEA.Core.Model;
using HYFEA.Hosting;
using HYFEA.Viz;
using Xunit;

namespace HYFEA.Tests.Integration;

public sealed class ResultMeshJsonTests
{
    [Fact]
    public void Cantilever_mesh_json_contains_contract_fields()
    {
        const int segments = 4;
        var geom = InMemoryAxisGeometry.Horizontal(10000.0, segments);
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

        var problem = BeamProblemFactory.Build(geom.GetAxisNodes(), request);
        var result = new BeamSolveSession().Run(geom, request);
        result.Success.Should().BeTrue(result.Error?.Message);

        var mesh = BeamResultMeshBuilder.From(problem, result);
        var json = ResultMeshJson.ToJson(mesh);

        json.Should().Contain("\"activeScalar\":\"U_mag\"");
        json.Should().Contain("\"points\":[");
        json.Should().Contain("\"lines\":[");
        json.Should().Contain("\"UX\":[");
        json.Should().Contain("\"UY\":[");
        json.Should().Contain("\"U_mag\":[");

        // points: 3*(segments+1); lines: 2*segments
        mesh.NumberOfPoints.Should().Be(segments + 1);
        mesh.NumberOfLines.Should().Be(segments);
        ResultMeshJson.ToUtf8(mesh).Length.Should().BeGreaterThan(50);
    }
}
