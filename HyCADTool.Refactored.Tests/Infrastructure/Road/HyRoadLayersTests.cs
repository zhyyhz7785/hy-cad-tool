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
        public void GetAll_Returns4Layers()
        {
            var all = HyRoadLayers.GetAll();
            all.Should().HaveCount(4);
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
