using System.Linq;
using FluentAssertions;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;
using Xunit;

namespace HyCADTool.Refactored.Tests.Infrastructure.Road
{
    /// <summary>
    /// P1：<see cref="HyRoadLayers"/> 常量稳健性测试。
    ///
    /// 目标是把"命名 / 颜色 / 重复"这类容易因重构回归的低级错误拦在测试阶段。
    /// </summary>
    public class HyRoadLayersTests
    {
        [Fact]
        public void GetAll_ReturnsAllKnownLayers()
        {
            var all = HyRoadLayers.GetAll();
            // P0: Alignment / Profile / Corridor / Marking（4）
            // P1.c 新增：Station
            // M3 新增：9 个横断面图层
            // v2 新增：Kerb（路牙独立图层）
            all.Should().HaveCount(15);
            all.Select(t => t.layerName).Should().Contain(HyRoadLayers.StationLayer);
        }

        [Fact]
        public void GetAll_IncludesAllCrossSectionLayers()
        {
            var names = HyRoadLayers.GetAll().Select(t => t.layerName).ToArray();
            names.Should().Contain(new[]
            {
                HyRoadLayers.CrossSectionOutlineLayer,
                HyRoadLayers.CrossSectionCenterlineLayer,
                HyRoadLayers.CrossSectionPavementLayer,
                HyRoadLayers.CrossSectionSidewalkLayer,
                HyRoadLayers.CrossSectionKerbLayer,
                HyRoadLayers.CrossSectionGreenLayer,
                HyRoadLayers.CrossSectionDimensionLayer,
                HyRoadLayers.CrossSectionAnnotationLayer,
                HyRoadLayers.CrossSectionTitleLayer,
                HyRoadLayers.CrossSectionOrientationLayer,
            });
        }

        [Fact]
        public void CrossSectionLayers_AllFollowHorizontalSectionNamingConvention()
        {
            var all = HyRoadLayers.GetAll();
            var csLayers = all.Where(t => t.layerName.Contains("横断面")).ToArray();
            csLayers.Should().HaveCount(10, "M3 共 9 个横断面图层 + v2 路牙独立图层 = 10");
            foreach (var (name, _) in csLayers)
            {
                name.Should().StartWith("05_hy_道路_横断面_");
            }
        }

        [Fact]
        public void LayerNames_AreDistinctAndNonEmpty()
        {
            var names = HyRoadLayers.GetAll().Select(t => t.layerName).ToArray();
            names.Should().OnlyHaveUniqueItems();
            names.Should().NotContain(string.Empty).And.NotContainNulls();
        }

        [Fact]
        public void LayerNames_UseRoadModulePrefix()
        {
            foreach (var (name, _) in HyRoadLayers.GetAll())
            {
                name.Should().StartWith("05_hy_道路_",
                    "统一采用 05_hy_道路_ 前缀，避免与既有 00-04 模块冲突");
            }
        }

        [Fact]
        public void ColorIndex_IsWithinValidAutoCADRange()
        {
            foreach (var (_, color) in HyRoadLayers.GetAll())
            {
                // 1..255 是 AutoCAD 可用色号（0 是 ByBlock，256 是 ByLayer）
                color.Should().BeInRange((short)1, (short)255);
            }
        }
    }
}
