using System;
using FluentAssertions;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Configuration.User;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.Services.Road
{
    public sealed class RoadMaterialFillPresetsTests : IDisposable
    {
        public RoadMaterialFillPresetsTests()
        {
            RoadMaterialFillPresets.RestoreDefaults();
        }

        public void Dispose()
        {
            RoadMaterialFillPresets.RestoreDefaults();
        }

        [Fact]
        public void ApplyUserSettings_ShouldMergeDefaultsAddedAndRemovedItems()
        {
            var settings = new RoadMaterialFillSettings
            {
                SurfaceAdded = { "自定义面层" },
                SurfaceRemoved = { "粘层油(PC-3)" },
                BaseAdded = { "自定义基层" },
                SubbaseRemoved = { "路基处理8%灰土" },
            };

            RoadMaterialFillPresets.ApplyUserSettings(settings);

            RoadMaterialFillPresets.Surface.Should().Contain("自定义面层");
            RoadMaterialFillPresets.Surface.Should().NotContain("粘层油(PC-3)");
            RoadMaterialFillPresets.Base.Should().Contain("自定义基层");
            RoadMaterialFillPresets.Subbase.Should().NotContain("路基处理8%灰土");
        }

        [Fact]
        public void ExportUserSettings_ShouldCaptureDeltaAgainstDefaults()
        {
            RoadMaterialFillPresets.TryAdd(StructureLayerKind.Surface, "新增面层");
            RoadMaterialFillPresets.TryRemove(StructureLayerKind.Base, "14%灰土");

            var settings = RoadMaterialFillPresets.ExportUserSettings();

            settings.SurfaceAdded.Should().ContainSingle().Which.Should().Be("新增面层");
            settings.BaseRemoved.Should().ContainSingle().Which.Should().Be("14%灰土");
            settings.SurfaceRemoved.Should().BeEmpty();
            settings.BaseAdded.Should().BeEmpty();
        }

        [Fact]
        public void RestoreDefaults_ShouldRecoverBuiltinItemsAfterUserChanges()
        {
            RoadMaterialFillPresets.TryRemove(StructureLayerKind.Surface, "粘层油(PC-3)");
            RoadMaterialFillPresets.TryAdd(StructureLayerKind.Surface, "临时候选");

            RoadMaterialFillPresets.RestoreDefaults();

            RoadMaterialFillPresets.Surface.Should().Contain("粘层油(PC-3)");
            RoadMaterialFillPresets.Surface.Should().NotContain("临时候选");
        }

        [Fact]
        public void SuggestHatchFor_AsphaltMaterial_ReturnsArConc()
        {
            RoadMaterialFillPresets.SuggestHatchFor(StructureLayerKind.Surface, "细粒式SBS改性沥青混凝土(AC-13C)")
                .Should().Be("AR-CONC");
        }

        [Fact]
        public void HatchPatternNames_ShouldExposeBuiltinList()
        {
            RoadMaterialFillPresets.HatchPatternNames.Should().Contain("SOLID");
            RoadMaterialFillPresets.HatchPatternNames.Should().Contain("AR-CONC");
        }
    }
}
