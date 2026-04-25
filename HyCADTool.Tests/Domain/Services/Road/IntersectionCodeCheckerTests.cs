using FluentAssertions;
using HyCADTool.Domain.Services.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>
    /// P3-I1-A：<see cref="IntersectionCodeChecker"/> 规范校核（CJJ 37 附录 B + CJJ 152 表 6.4.2）。
    /// </summary>
    public class IntersectionCodeCheckerTests
    {
        [Theory]
        [InlineData(20, 15)]
        [InlineData(30, 20)]
        [InlineData(40, 25)]
        [InlineData(50, 30)]
        [InlineData(60, 40)]
        [InlineData(80, 50)]
        public void LookupMinCornerRadius_ReturnsExpected(double speed, double expectedRmin)
        {
            IntersectionCodeChecker.LookupMinCornerRadius(speed).Should().Be(expectedRmin);
        }

        [Fact]
        public void LookupMinCornerRadius_BetweenTiers_TakesCeiling()
        {
            IntersectionCodeChecker.LookupMinCornerRadius(25).Should().Be(20);
            IntersectionCodeChecker.LookupMinCornerRadius(45).Should().Be(30);
            IntersectionCodeChecker.LookupMinCornerRadius(70).Should().Be(50);
        }

        [Fact]
        public void LookupMinCornerRadius_AboveMax_Clamps()
        {
            IntersectionCodeChecker.LookupMinCornerRadius(120).Should().Be(50);
        }

        [Fact]
        public void CheckCornerRadius_Pass()
        {
            var r = IntersectionCodeChecker.CheckCornerRadius(25, 30);
            r.Pass.Should().BeTrue();
            r.MinRequired.Should().Be(20);
            r.Radius.Should().Be(25);
        }

        [Fact]
        public void CheckCornerRadius_Fail_WhenBelowMin()
        {
            var r = IntersectionCodeChecker.CheckCornerRadius(15, 50);
            r.Pass.Should().BeFalse();
            r.MinRequired.Should().Be(30);
        }

        [Fact]
        public void CheckCornerRadius_Fail_WhenNonPositive()
        {
            var r = IntersectionCodeChecker.CheckCornerRadius(0, 30);
            r.Pass.Should().BeFalse();
        }

        [Fact]
        public void CheckMinLegCount_Works()
        {
            IntersectionCodeChecker.CheckMinLegCount(3).Pass.Should().BeTrue();
            IntersectionCodeChecker.CheckMinLegCount(2).Pass.Should().BeTrue();
            IntersectionCodeChecker.CheckMinLegCount(1).Pass.Should().BeFalse();
        }

        [Fact]
        public void CheckAdjacentLegAngle_Works()
        {
            IntersectionCodeChecker.CheckAdjacentLegAngle(90).Pass.Should().BeTrue();
            IntersectionCodeChecker.CheckAdjacentLegAngle(10).Pass.Should().BeFalse();
            IntersectionCodeChecker.CheckAdjacentLegAngle(15).Pass.Should().BeTrue();
            IntersectionCodeChecker.CheckAdjacentLegAngle(350).Pass.Should().BeFalse();
        }
    }
}
