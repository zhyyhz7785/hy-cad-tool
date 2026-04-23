using Autodesk.AutoCAD.DatabaseServices;
using FluentAssertions;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.ValueObjects.Road
{
    public class CrossSectionAnnotationStyleTests
    {
        [Fact]
        public void Ctor_WithoutLayerArgs_UsesHyRoadDefaultLayers()
        {
            var style = new CrossSectionAnnotationStyle(
                default(ObjectId),
                default(ObjectId),
                default(ObjectId),
                textHeightModel: 0.25,
                mleaderLandingGapModel: 0.05,
                mleaderArrowSizeModel: 0.2);

            style.TextLayerName.Should().Be(HyRoadLayers.CrossSectionAnnotationLayer);
            style.DimensionLayerName.Should().Be(HyRoadLayers.CrossSectionDimensionLayer);
            style.TitleLayerName.Should().Be(HyRoadLayers.CrossSectionTitleLayer);
        }

        [Fact]
        public void Ctor_NegativePrecision_IsClampedToZero()
        {
            var style = new CrossSectionAnnotationStyle(
                default(ObjectId),
                default(ObjectId),
                default(ObjectId),
                textHeightModel: 0.25,
                mleaderLandingGapModel: 0.05,
                mleaderArrowSizeModel: 0.2,
                precision: -3);

            style.Precision.Should().Be(0);
        }
    }
}
