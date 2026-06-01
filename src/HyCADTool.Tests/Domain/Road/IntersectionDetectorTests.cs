using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCAD.Geometry;
using Xunit;

namespace HyCADTool.Tests.Domain.Road
{
    public class IntersectionDetectorTests
    {
        [Fact]
        public void Detect_TwoCrossingLines_OneCluster()
        {
            var a = new Alignment { Name = "A" };
            a.Centerline = new Polyline3D(
                new[] { new Point3D(0, 0, 0), new Point3D(100, 0, 0) }, isClosed: false);
            var b = new Alignment { Name = "B" };
            b.Centerline = new Polyline3D(
                new[] { new Point3D(50, -50, 0), new Point3D(50, 50, 0) }, isClosed: false);

            var det = new IntersectionDetector();
            var clusters = det.Detect(new[] { a, b }, 1.0);
            clusters.Should().HaveCount(1);
            clusters[0].AlignmentIds.Should().HaveCount(2);
        }

        [Fact]
        public void Detect_Parallel_NoCluster()
        {
            var a = new Alignment { Name = "A" };
            a.Centerline = new Polyline3D(
                new[] { new Point3D(0, 0, 0), new Point3D(100, 0, 0) }, isClosed: false);
            var b = new Alignment { Name = "B" };
            b.Centerline = new Polyline3D(
                new[] { new Point3D(0, 1, 0), new Point3D(100, 1, 0) }, isClosed: false);

            var det = new IntersectionDetector();
            var clusters = det.Detect(new[] { a, b }, 0.5);
            clusters.Should().BeEmpty();
        }
    }
}
