using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Features.Road.PlanAlignment.Domain;
using HyCADTool.Shared.Geometry;
using Xunit;

namespace HyCADTool.Tests.Domain.Models.Road
{
    public class AlignmentSourcePiExtractionTests
    {
        private const double Eps = 1e-3;

        [Fact]
        public void TryCreatePiTableFromBulgeCenterline_LeftTurn90_RoundTripsApproximateRadius()
        {
            var pts = new[]
            {
                new Point2D(0, 0),
                new Point2D(100, 0),
                new Point2D(100, 100),
            };

            var built = AlignmentPiDesigner.Build(pts, 20);
            built.Polyline.HasArcs.Should().BeTrue();

            var src = AlignmentSource.TryCreatePiTableFromBulgeCenterline(built.Polyline);
            src.Should().NotBeNull();
            src.PiElements.Count.Should().Be(3);

            src.PiElements[0].P.X.Should().BeApproximately(0, Eps);
            src.PiElements[0].Radius.Should().Be(0);
            src.PiElements[1].Radius.Should().BeApproximately(20, 0.05);
            src.PiElements[2].P.X.Should().BeApproximately(100, Eps);
            src.PiElements[2].P.Y.Should().BeApproximately(100, Eps);
        }

        [Fact]
        public void TryCreatePiTableFromBulgeCenterline_StraightChain_DelegatesToVertices()
        {
            var pl = new Polyline3D(new[]
            {
                new Point3D(0, 0, 0),
                new Point3D(10, 0, 0),
                new Point3D(10, 10, 0),
            }, isClosed: false);

            var src = AlignmentSource.TryCreatePiTableFromBulgeCenterline(pl);
            src.Should().NotBeNull();
            src.PiElements.Count.Should().Be(3);
            src.PiElements[1].P.X.Should().BeApproximately(10, Eps);
        }
    }
}
