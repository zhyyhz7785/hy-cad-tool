using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.Services.Road
{
    /// <summary>M7.1 CrossSectionPresetService 基本行为。</summary>
    public class CrossSectionPresetServiceTests : IDisposable
    {
        private readonly string _dir;

        public CrossSectionPresetServiceTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "hycad-preset-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
            catch { }
        }

        [Fact]
        public void LoadAll_IncludesThreeBuiltInPresets()
        {
            var svc = new CrossSectionPresetService(_dir);
            var all = svc.LoadAll();
            all.Count.Should().BeGreaterOrEqualTo(3);
            all.Select(p => p.Key).Should().Contain(new[] { "urban-arterial", "secondary", "local" });
        }

        [Fact]
        public void LoadByName_ReturnsBuiltInLayout()
        {
            var svc = new CrossSectionPresetService(_dir);
            var layout = svc.LoadByName("urban-arterial");
            layout.Should().NotBeNull();
            layout.DesignSpeed.Should().Be(60);
            layout.LeftBands.Should().NotBeEmpty();
        }

        [Fact]
        public void SaveAndLoadUserPreset_RoundTrips()
        {
            var svc = new CrossSectionPresetService(_dir);

            // 基于内置做小改：宽度换一下
            var layout = svc.LoadByName("urban-arterial");
            var newLayout = layout.WithDesignSpeed(70).WithTitle("自定义方案A").WithPlanStripLength(8.4);

            svc.SaveUserPreset("my-custom", "我的方案 A", newLayout);

            var all = svc.LoadAll();
            all.Select(p => p.Key).Should().Contain("my-custom");

            var loaded = svc.LoadByName("my-custom");
            loaded.Should().NotBeNull();
            loaded.DesignSpeed.Should().Be(70);
            loaded.Title.Should().Be("自定义方案A");
            loaded.PlanStripLength.Should().BeApproximately(8.4, 1e-9);
        }

        [Fact]
        public void DeleteUserPreset_RemovesFile()
        {
            var svc = new CrossSectionPresetService(_dir);
            var layout = svc.LoadByName("secondary");
            svc.SaveUserPreset("temp", "临时", layout);

            svc.DeleteUserPreset("temp").Should().BeTrue();
            svc.DeleteUserPreset("temp").Should().BeFalse(); // 第二次删不到了
            svc.LoadByName("temp").Should().BeNull();
        }

        [Fact]
        public void LoadByName_UnknownReturnsNull()
        {
            var svc = new CrossSectionPresetService(_dir);
            svc.LoadByName("nonexistent").Should().BeNull();
            svc.LoadByName(null).Should().BeNull();
        }

        [Fact]
        public void SaveUserPreset_RejectsNullInputs()
        {
            var svc = new CrossSectionPresetService(_dir);
            Action a = () => svc.SaveUserPreset(null, "d", new CrossSectionPresetService(_dir).LoadByName("local"));
            a.Should().Throw<ArgumentException>();
            Action b = () => svc.SaveUserPreset("k", "d", null);
            b.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void SanitizesUnsafeFileNameChars()
        {
            var svc = new CrossSectionPresetService(_dir);
            var layout = svc.LoadByName("local");
            svc.SaveUserPreset("path/with:bad*chars", "display", layout);

            var all = svc.LoadAll();
            all.Select(p => p.Key).Should().Contain(k => k.Contains("path"));
        }

        [Fact]
        public void DefaultUserDirectory_UnderAppData()
        {
            CrossSectionPresetService.DefaultUserDirectory
                .Should().Contain("HyCAD").And.Contain("presets").And.Contain("crosssection");
        }

        [Fact]
        public void LoadAll_WhenUserDirMissing_OnlyBuiltInReturned()
        {
            // _dir 还不存在
            var svc = new CrossSectionPresetService(_dir);
            var all = svc.LoadAll();
            all.Count.Should().Be(CrossSectionPresets.All.Count);
        }

        [Fact]
        public void LoadAll_CorruptedUserFile_Skipped()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "bad.json"), "{not-json");

            var svc = new CrossSectionPresetService(_dir);
            var all = svc.LoadAll();
            all.Count.Should().Be(CrossSectionPresets.All.Count); // 坏文件不影响内置
        }

        [Fact]
        public void SaveAndLoadUserPreset_PreservesExtendedBandFields()
        {
            var svc = new CrossSectionPresetService(_dir);
            var layout = svc.LoadByName("urban-arterial");
            var left = layout.LeftBands.ToList();
            left[0] = left[0]
                .WithElevationDiff(0.12)
                .WithInnerElevationDiff(-0.04)
                .WithSlopeType(RoadSlopeType.Double)
                .WithCrownProfile(RoadCrownProfile.Parabolic)
                .WithSurfaceLayer(RoadSurfaceLayer.PavementSurface)
                .WithOuterKerb(new KerbSpec(RoadKerbType.Curb, "15x10x50", 0.18, 0.15))
                .WithInnerKerb(new KerbSpec(RoadKerbType.Plain, "10x10x30", 0.02, 0.10));
            var updated = layout.WithLeftBands(left);

            svc.SaveUserPreset("extended-field-case", "扩展字段方案", updated);
            var loaded = svc.LoadByName("extended-field-case");

            loaded.Should().NotBeNull();
            var band = loaded.LeftBands[0];
            band.ElevationDiff.Should().BeApproximately(0.12, 1e-9);
            band.InnerElevationDiff.Should().BeApproximately(-0.04, 1e-9);
            band.SlopeType.Should().Be(RoadSlopeType.Double);
            band.CrownProfile.Should().Be(RoadCrownProfile.Parabolic);
            band.SurfaceLayer.Should().Be(RoadSurfaceLayer.PavementSurface);
            band.OuterKerb.Type.Should().Be(RoadKerbType.Curb);
            band.InnerKerb.Type.Should().Be(RoadKerbType.Plain);
        }

        [Fact]
        public void SaveUserPreset_SameDisplayName_OverwritesExistingUserPreset()
        {
            var svc = new CrossSectionPresetService(_dir);
            var first = svc.LoadByName("local").WithTitle("第一版");
            var second = svc.LoadByName("secondary").WithTitle("第二版");

            svc.SaveUserPreset("preset-a", "同名方案", first);
            svc.SaveUserPreset("preset-b", "同名方案", second);

            var all = svc.LoadAll();
            all.Count(p => p.DisplayName == "同名方案").Should().Be(1);

            var sameName = all.Single(p => p.DisplayName == "同名方案");
            var loaded = svc.LoadByName(sameName.Key);
            loaded.Title.Should().Be("第二版");
        }

        [Fact]
        public void DeleteUserPresetByDisplayName_RemovesOnlyUserPreset()
        {
            var svc = new CrossSectionPresetService(_dir);
            var layout = svc.LoadByName("local");
            svc.SaveUserPreset("for-delete", "待删除方案", layout);

            svc.DeleteUserPresetByDisplayName("待删除方案").Should().BeTrue();
            svc.DeleteUserPresetByDisplayName("待删除方案").Should().BeFalse();
            svc.DeleteUserPresetByDisplayName("城市主干路（CJJ37）").Should().BeFalse("内置预设不在用户目录，不允许删除");
        }
    }
}
