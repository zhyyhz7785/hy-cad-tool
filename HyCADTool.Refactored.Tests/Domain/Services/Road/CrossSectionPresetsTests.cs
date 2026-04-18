using FluentAssertions;
using HyCADTool.Refactored.Domain.Services.Road;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.Services.Road
{
    /// <summary>
    /// 保证 3 个预设都能通过 <see cref="CrossSectionCodeChecker"/>。
    ///
    /// 任何对 checker / preset 的改动都应保持本测试绿，否则用户一点预设就出红字，违反直觉。
    /// </summary>
    public class CrossSectionPresetsTests
    {
        [Fact]
        public void UrbanArterial_Preset_PassesAllChecks()
        {
            var layout = CrossSectionPresets.CreateCjj37UrbanArterial();
            layout.TotalWidth.Should().BeApproximately(2 * (3.5 * 3 + 3.0) + 2.0, 1e-6);
            CrossSectionCodeChecker.Check(layout).AllPassed.Should().BeTrue();
        }

        [Fact]
        public void SecondaryRoad_Preset_PassesAllChecks()
        {
            var layout = CrossSectionPresets.CreateCjj37SecondaryRoad();
            layout.TotalWidth.Should().BeApproximately(2 * (3.5 * 2 + 2.5), 1e-6);
            CrossSectionCodeChecker.Check(layout).AllPassed.Should().BeTrue();
        }

        [Fact]
        public void LocalRoad_Preset_PassesAllChecks()
        {
            var layout = CrossSectionPresets.CreateCjj37LocalRoad();
            layout.TotalWidth.Should().BeApproximately(2 * (3.5 + 2.0), 1e-6);
            CrossSectionCodeChecker.Check(layout).AllPassed.Should().BeTrue();
        }

        [Fact]
        public void All_ContainsThreePresets()
        {
            CrossSectionPresets.All.Should().HaveCount(3);
            foreach (var p in CrossSectionPresets.All)
            {
                p.Key.Should().NotBeNullOrEmpty();
                p.DisplayName.Should().NotBeNullOrEmpty();
                p.Create().Should().NotBeNull();
            }
        }
    }
}
