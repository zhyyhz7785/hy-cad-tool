using System.IO;
using FluentAssertions;
using HYFEA.Core.Loads;
using HYFEA.Core.Model;
using HYFEA.Hosting;
using HYFEA.Viz;
using Xunit;

namespace HYFEA.Tests.Integration;

public sealed class VtuWriterTests
{
    [Fact]
    public void Cantilever_uniform_export_contains_displacement_fields()
    {
        const double L = 10000.0;
        const int segments = 4;

        var geom = InMemoryAxisGeometry.Horizontal(L, segments);
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
        mesh.NumberOfPoints.Should().Be(segments + 1);
        mesh.NumberOfLines.Should().Be(segments);
        mesh.PointData.Should().ContainKeys("UX", "UY", "U_mag");
        mesh.PointData["U_mag"][segments].Should().BeGreaterThan(0);
        mesh.PointVectors.Should().ContainKey("U");
        mesh.PointVectors["U"].Should().HaveCount((segments + 1) * 3);

        var path = Path.Combine(Path.GetTempPath(), "hyfea-vtu-test-" + Path.GetRandomFileName() + ".vtu");
        try
        {
            VtuWriter.Write(mesh, path);
            var text = File.ReadAllText(path);
            text.Should().Contain("<Piece");
            text.Should().Contain("NumberOfPoints=\"" + (segments + 1) + "\"");
            text.Should().Contain("NumberOfCells=\"" + segments + "\"");
            text.Should().Contain("Name=\"UX\"");
            text.Should().Contain("Name=\"UY\"");
            text.Should().Contain("Name=\"U_mag\"");
            text.Should().Contain("Name=\"U\"");
            text.Should().Contain("NumberOfComponents=\"3\"");
            text.Should().Contain("Vectors=\"U\"");
            text.Should().Contain("UnstructuredGrid");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
