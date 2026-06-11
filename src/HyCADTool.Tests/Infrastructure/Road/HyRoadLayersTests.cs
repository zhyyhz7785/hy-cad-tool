using System.Linq;
using FluentAssertions;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Xdata;
using Xunit;

namespace HyCADTool.Tests.Infrastructure.Road
{
    /// <summary>
    /// <see cref="HyRoadLayers"/> 与 <see cref="LayerBuiltinDefaults"/> 一致性测试。
    /// </summary>
    public class HyRoadLayersTests
    {
        [Fact]
        public void GetAll_HasNoDuplicateLayerNames()
        {
            var names = HyRoadLayers.GetAll().Select(t => t.layerName).ToArray();
            names.Should().OnlyHaveUniqueItems();
            names.Should().NotContain(string.Empty).And.NotContainNulls();
        }

        [Fact]
        public void GetAll_ContainsCoreRoadLayers()
        {
            var names = HyRoadLayers.GetAll().Select(t => t.layerName).ToHashSet();
            names.Should().Contain(LayerBuiltinDefaults.RoadPlaneAlignment);
            names.Should().Contain(LayerBuiltinDefaults.RoadProfile);
            names.Should().Contain(LayerBuiltinDefaults.RoadCrossSectionOutline);
            names.Should().Contain(LayerBuiltinDefaults.RoadCrossSectionTitle);
            names.Should().Contain(LayerBuiltinDefaults.RoadCrossSectionAnnotation);
        }

        [Fact]
        public void GetAll_IncludesAllCrossSectionLayers()
        {
            var names = HyRoadLayers.GetAll().Select(t => t.layerName).ToArray();
            names.Should().Contain(new[]
            {
                LayerBuiltinDefaults.RoadCrossSectionOutline,
                LayerBuiltinDefaults.RoadCrossSectionCenterline,
                LayerBuiltinDefaults.RoadCrossSectionPavement,
                LayerBuiltinDefaults.RoadCrossSectionSidewalk,
                LayerBuiltinDefaults.RoadCrossSectionKerb,
                LayerBuiltinDefaults.RoadCrossSectionGreen,
                LayerBuiltinDefaults.RoadCrossSectionDimension,
                LayerBuiltinDefaults.RoadCrossSectionAnnotation,
                LayerBuiltinDefaults.RoadCrossSectionTitle,
            });
        }

        [Fact]
        public void GetAll_MapsEachEntryToSemanticId()
        {
            foreach (var (layerName, _, _) in HyRoadLayers.GetAll())
            {
                LayerCatalogFactory.MapRoadNameToSemanticId(layerName)
                    .Should().NotBeNullOrEmpty($"layer '{layerName}' should map to a semantic ID");
            }
        }

        [Fact]
        public void ColorIndex_IsWithinValidAutoCADRange()
        {
            foreach (var (_, color, _) in HyRoadLayers.GetAll())
            {
                color.Should().BeInRange((short)1, (short)255);
            }
        }

        [Fact]
        public void RaftBuiltinDefaults_AreRegisteredInCatalog()
        {
            var catalog = LayerCatalogFactory.CreateDefaultItems();
            var bySemantic = catalog.ToDictionary(x => x.SemanticId);

            bySemantic.Should().ContainKey("RaftSlabXTop");
            bySemantic["RaftSlabXTop"].Name.Should().Be(LayerBuiltinDefaults.RaftSlabXTop);
            bySemantic["RaftDimY"].Name.Should().Be(LayerBuiltinDefaults.RaftDimY);
        }
    }
}
