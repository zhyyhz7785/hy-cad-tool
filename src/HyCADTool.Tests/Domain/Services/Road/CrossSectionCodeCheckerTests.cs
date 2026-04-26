using System.Linq;
using FluentAssertions;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>
    /// <see cref="CrossSectionCodeChecker"/> 7 项规范逐项 happy/sad path，及 Suggestion 文案。
    /// </summary>
    public class CrossSectionCodeCheckerTests
    {
        // 构造一个默认"全部合规"的布局（无分隔带，左右各 2 条 3.5m/1.5% 车道 + 3.0m 人行道）
        private static CrossSectionLayout BuildCompliantLayout()
        {
            return CrossSectionLayout.Create(
                new[]
                {
                    CrossSectionBand.Lane(3.5, 1.5),
                    CrossSectionBand.Lane(3.5, 1.5),
                    CrossSectionBand.Sidewalk(3.0, 1.5),
                },
                new[]
                {
                    CrossSectionBand.Lane(3.5, 1.5, BandSide.Right),
                    CrossSectionBand.Lane(3.5, 1.5, BandSide.Right),
                    CrossSectionBand.Sidewalk(3.0, 1.5, BandSide.Right),
                },
                centerMedianWidth: 0,
                designSpeed: 60);
        }

        [Fact]
        public void Compliant_Layout_Passes_All()
        {
            var layout = BuildCompliantLayout();
            var report = CrossSectionCodeChecker.Check(layout);
            report.AllPassed.Should().BeTrue();
            report.Items.Should().HaveCount(7);
        }

        // ① 车道宽度
        [Fact]
        public void FailedLaneWidth_SuggestsRaiseOrLower()
        {
            var layout = CrossSectionLayout.Create(
                new[] { CrossSectionBand.Lane(2.5, 1.5), CrossSectionBand.Lane(3.5, 1.5) },
                new[] { CrossSectionBand.Lane(3.5, 1.5, BandSide.Right), CrossSectionBand.Lane(3.5, 1.5, BandSide.Right) },
                0, 60);
            var item = CrossSectionCodeChecker.Check(layout).Items.First(i => i.Name == "机动车道宽度");
            item.Passed.Should().BeFalse();
            item.Suggestion.Should().Contain("建议");
        }

        [Fact]
        public void LaneWidth_TooWide_Fails()
        {
            var layout = CrossSectionLayout.Create(
                new[] { CrossSectionBand.Lane(4.0, 1.5) },
                new[] { CrossSectionBand.Lane(4.0, 1.5, BandSide.Right) },
                0, 60);
            var item = CrossSectionCodeChecker.Check(layout).Items.First(i => i.Name == "机动车道宽度");
            item.Passed.Should().BeFalse();
        }

        // ② 人行道宽度
        [Fact]
        public void FailedSidewalkWidth_Suggestion()
        {
            var layout = CrossSectionLayout.Create(
                new[] { CrossSectionBand.Lane(3.5, 1.5), CrossSectionBand.Sidewalk(1.0, 1.5) },
                new[] { CrossSectionBand.Lane(3.5, 1.5, BandSide.Right), CrossSectionBand.Sidewalk(1.0, 1.5, BandSide.Right) },
                0, 60);
            var item = CrossSectionCodeChecker.Check(layout).Items.First(i => i.Name == "人行道宽度");
            item.Passed.Should().BeFalse();
            item.Suggestion.Should().Contain("1.50");
        }

        // ③ 非机动车道宽度
        [Fact]
        public void FailedNonMotorizedWidth_Suggestion()
        {
            var layout = CrossSectionLayout.Create(
                new[] { CrossSectionBand.Lane(3.5, 1.5), CrossSectionBand.NonMotor(1.5, 1.5) },
                new[] { CrossSectionBand.Lane(3.5, 1.5, BandSide.Right), CrossSectionBand.NonMotor(1.5, 1.5, BandSide.Right) },
                0, 60);
            var item = CrossSectionCodeChecker.Check(layout).Items.First(i => i.Name == "非机动车道宽度");
            item.Passed.Should().BeFalse();
            item.Suggestion.Should().Contain("2.50");
        }

        // ④ 车道横坡
        [Fact]
        public void FailedLaneSlope_BelowMin_Suggests()
        {
            var layout = CrossSectionLayout.Create(
                new[] { CrossSectionBand.Lane(3.5, 0.5) },
                new[] { CrossSectionBand.Lane(3.5, 0.5, BandSide.Right) },
                0, 60);
            var item = CrossSectionCodeChecker.Check(layout).Items.First(i => i.Name == "机动车道横坡");
            item.Passed.Should().BeFalse();
            item.Suggestion.Should().Contain("1.0");
        }

        [Fact]
        public void FailedLaneSlope_AboveMax_Fails()
        {
            var layout = CrossSectionLayout.Create(
                new[] { CrossSectionBand.Lane(3.5, 3.0) },
                new[] { CrossSectionBand.Lane(3.5, 3.0, BandSide.Right) },
                0, 60);
            var item = CrossSectionCodeChecker.Check(layout).Items.First(i => i.Name == "机动车道横坡");
            item.Passed.Should().BeFalse();
        }

        // ⑤ 人行道 / 非机动车道横坡
        [Fact]
        public void FailedSidewalkSlope_Suggestion()
        {
            var layout = CrossSectionLayout.Create(
                new[] { CrossSectionBand.Lane(3.5, 1.5), CrossSectionBand.Sidewalk(3.0, 0.5) },
                new[] { CrossSectionBand.Lane(3.5, 1.5, BandSide.Right), CrossSectionBand.Sidewalk(3.0, 0.5, BandSide.Right) },
                0, 60);
            var item = CrossSectionCodeChecker.Check(layout).Items.First(i => i.Name == "人行道/非机动车道横坡");
            item.Passed.Should().BeFalse();
            item.Suggestion.Should().Contain("1.0");
        }

        // ⑥ 中央分隔带
        [Fact]
        public void MedianTooSmall_Fails_With_Suggestion()
        {
            var layout = CrossSectionLayout.Create(
                new[] { CrossSectionBand.Lane(3.5, 1.5) },
                new[] { CrossSectionBand.Lane(3.5, 1.5, BandSide.Right) },
                centerMedianWidth: 0.6,
                designSpeed: 60);
            var item = CrossSectionCodeChecker.Check(layout).Items.First(i => i.Name == "中央分隔带宽");
            item.Passed.Should().BeFalse();
            item.Suggestion.Should().Contain("分隔带");
        }

        [Fact]
        public void Median_Zero_Or_Bigger_Than_Min_Passes()
        {
            var noMedian = CrossSectionLayout.Create(
                new[] { CrossSectionBand.Lane(3.5, 1.5) },
                new[] { CrossSectionBand.Lane(3.5, 1.5, BandSide.Right) },
                centerMedianWidth: 0,
                designSpeed: 60);
            CrossSectionCodeChecker.Check(noMedian).Items
                .First(i => i.Name == "中央分隔带宽").Passed.Should().BeTrue();

            var bigMedian = noMedian.WithCenterMedianWidth(2.0);
            CrossSectionCodeChecker.Check(bigMedian).Items
                .First(i => i.Name == "中央分隔带宽").Passed.Should().BeTrue();
        }

        // ⑦ 对称
        [Fact]
        public void Asymmetric_Fails_With_AverageSuggestion()
        {
            var layout = CrossSectionLayout.Create(
                new[] { CrossSectionBand.Lane(3.5, 1.5), CrossSectionBand.Lane(3.5, 1.5) },
                new[] { CrossSectionBand.Lane(3.5, 1.5, BandSide.Right) },
                0, 60);
            var item = CrossSectionCodeChecker.Check(layout).Items.First(i => i.Name == "左右对称");
            item.Passed.Should().BeFalse();
            item.Suggestion.Should().Contain("镜像");
        }

        [Fact]
        public void ToString_IncludesSuggestion_WhenFailed()
        {
            var layout = CrossSectionLayout.Create(
                new[] { CrossSectionBand.Lane(2.5, 1.5) },
                new[] { CrossSectionBand.Lane(2.5, 1.5, BandSide.Right) },
                0, 60);
            var item = CrossSectionCodeChecker.Check(layout).Items.First(i => i.Name == "机动车道宽度");
            item.ToString().Should().Contain("建议");
        }

        [Fact]
        public void Report_Contains_DesignSpeed()
        {
            var layout = BuildCompliantLayout().WithDesignSpeed(80);
            CrossSectionCodeChecker.Check(layout).DesignSpeed.Should().Be(80);
        }
    }
}
