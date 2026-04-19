using System;
using FluentAssertions;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.ValueObjects.Road
{
    /// <summary>
    /// T4 验收：<see cref="AlignmentDefaults"/> 默认值 / Clone / Validate。
    /// </summary>
    public class AlignmentDefaultsTests
    {
        [Fact]
        public void Default_HasReasonableValues()
        {
            var d = new AlignmentDefaults();
            d.DefaultRadius.Should().BeGreaterThan(0);
            d.DefaultSpiralIn.Should().Be(0);
            d.DefaultSpiralOut.Should().Be(0);
            d.DefaultStartStation.Should().Be(0);
            d.Validate();
        }

        [Fact]
        public void Clone_ProducesIndependentCopy()
        {
            var a = new AlignmentDefaults { DefaultRadius = 100, DefaultSpiralIn = 40 };
            var b = a.Clone();
            b.DefaultRadius.Should().Be(100);
            b.DefaultSpiralIn.Should().Be(40);

            b.DefaultRadius = 200;
            a.DefaultRadius.Should().Be(100, "克隆后修改副本不应影响原件");
        }

        [Theory]
        [InlineData(-1, 0, 0)]
        [InlineData(0, -5, 0)]
        [InlineData(0, 0, -5)]
        public void Validate_Throws_OnNegativeRadiusOrSpiral(double r, double lin, double lout)
        {
            var d = new AlignmentDefaults
            {
                DefaultRadius = r,
                DefaultSpiralIn = lin,
                DefaultSpiralOut = lout,
            };
            Action act = () => d.Validate();
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Validate_Allows_NegativeStartStation()
        {
            // 工程实际：复测旧线时 BP 可以链在负桩
            var d = new AlignmentDefaults { DefaultStartStation = -500 };
            Action act = () => d.Validate();
            act.Should().NotThrow();
        }
    }
}
