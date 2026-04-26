using FluentAssertions;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Features.Road.CrossSection.Domain;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    public class ElevationDiffRoundtripTests
    {
        [Fact]
        public void ToTemplateAndFromTemplate_ShouldPreserveElevationDiff()
        {
            var layout = CrossSectionLayout.Create(
                new[]
                {
                    CrossSectionBand.Lane(3.5, 1.5, BandSide.Left, "机动1").WithElevationDiff(0.12),
                    CrossSectionBand.GreenStrip(2.0, BandSide.Left, "绿化").WithElevationDiff(-0.05),
                },
                new[]
                {
                    CrossSectionBand.Lane(3.5, 1.5, BandSide.Right, "机动1").WithElevationDiff(0.08),
                },
                centerMedianWidth: 2.0,
                designSpeed: 60);

            var template = CrossSectionLayoutBuilder.ToTemplate(layout);
            var back = CrossSectionLayoutBuilder.FromTemplate(template);

            back.Should().NotBeNull();
            back.LeftBands[0].ElevationDiff.Should().BeApproximately(0.12, 1e-9);
            back.LeftBands[1].ElevationDiff.Should().BeApproximately(-0.05, 1e-9);
            back.RightBands[0].ElevationDiff.Should().BeApproximately(0.08, 1e-9);
        }
    }
}
