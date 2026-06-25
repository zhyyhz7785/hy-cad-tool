using HyCAD.Tables.Layout;
using Xunit;

namespace HyCADTool.Tests.Features.Tables
{
    public sealed class PaperPresetCatalogTests
    {
        [Theory]
        [InlineData(PaperPreset.A4, 277)]
        [InlineData(PaperPreset.A3, 400)]
        [InlineData(PaperPreset.A2, 574)]
        public void ResolveTargetWidthMm_standardPresets(PaperPreset preset, double expected)
        {
            var width = PaperPresetCatalog.ResolveTargetWidthMm(preset);
            Assert.Equal(expected, width, 3);
        }

        [Fact]
        public void ResolveTargetWidthMm_custom_returnsFallback()
        {
            var width = PaperPresetCatalog.ResolveTargetWidthMm(PaperPreset.Custom);
            Assert.Equal(PaperPresetCatalog.CustomFallbackWidthMm, width);
        }

        [Fact]
        public void TableViewport_CreateDefault_usesA3()
        {
            var viewport = TableViewport.CreateDefault();
            Assert.Equal(PaperPreset.A3, viewport.PaperPreset);
            Assert.Equal(400, viewport.TargetWidthMm, 3);
        }
    }
}
