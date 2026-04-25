using System;
using FluentAssertions;
using HyCADTool.Domain.Services.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>M8.3 DataNormalizationService 测试。</summary>
    public class DataNormalizationServiceTests
    {
        [Fact]
        public void RoundToInt_RoundsAwayFromZero()
        {
            DataNormalizationService.Normalize(3.4, DataNormalizationService.Mode.RoundToInt).Should().Be(3);
            DataNormalizationService.Normalize(3.5, DataNormalizationService.Mode.RoundToInt).Should().Be(4);
            DataNormalizationService.Normalize(-2.5, DataNormalizationService.Mode.RoundToInt).Should().Be(-3);
        }

        [Fact]
        public void RoundToDecimals_Works()
        {
            DataNormalizationService.Normalize(3.14159, DataNormalizationService.Mode.RoundToDecimals, 2).Should().Be(3.14);
            DataNormalizationService.Normalize(3.145, DataNormalizationService.Mode.RoundToDecimals, 2).Should().Be(3.15);
        }

        [Fact]
        public void RoundToDecimals_ClampsDigits()
        {
            DataNormalizationService.Normalize(3.14159, DataNormalizationService.Mode.RoundToDecimals, -1)
                .Should().Be(3);
            DataNormalizationService.Normalize(3.14159, DataNormalizationService.Mode.RoundToDecimals, 99)
                .Should().BeApproximately(3.14159, 1e-10);
        }

        [Fact]
        public void Scale_Multiplies()
        {
            DataNormalizationService.Normalize(1.5, DataNormalizationService.Mode.Scale, 10).Should().Be(15);
            DataNormalizationService.Normalize(0.3, DataNormalizationService.Mode.Scale, 100).Should().BeApproximately(30, 1e-9);
        }

        [Fact]
        public void Divide_Divides()
        {
            DataNormalizationService.Normalize(15, DataNormalizationService.Mode.Divide, 10).Should().Be(1.5);
        }

        [Fact]
        public void Divide_ByZeroThrows()
        {
            Action act = () => DataNormalizationService.Normalize(1, DataNormalizationService.Mode.Divide, 0);
            act.Should().Throw<DivideByZeroException>();
        }

        [Fact]
        public void DropDecimal_MovesDecimalPointRight()
        {
            DataNormalizationService.Normalize(3.45, DataNormalizationService.Mode.DropDecimal)
                .Should().Be(345);
            DataNormalizationService.Normalize(0.001, DataNormalizationService.Mode.DropDecimal)
                .Should().Be(1);
            DataNormalizationService.Normalize(12, DataNormalizationService.Mode.DropDecimal)
                .Should().Be(12); // 整数不动
        }

        [Fact]
        public void NanInput_Returned()
        {
            DataNormalizationService.Normalize(double.NaN, DataNormalizationService.Mode.RoundToInt).Should().Be(double.NaN);
            DataNormalizationService.Normalize(double.PositiveInfinity, DataNormalizationService.Mode.Scale, 2)
                .Should().Be(double.PositiveInfinity);
        }

        [Fact]
        public void NormalizeMany_ProducesNewArray()
        {
            var src = new[] { 1.25, 2.47, 3.51 };
            var result = DataNormalizationService.NormalizeMany(src, DataNormalizationService.Mode.RoundToInt);
            result.Should().Equal(new double[] { 1, 2, 4 });
            src.Should().Equal(new[] { 1.25, 2.47, 3.51 }); // 不修改原数组
        }

        [Fact]
        public void NormalizeMany_Null_ReturnsNull()
        {
            DataNormalizationService.NormalizeMany(null, DataNormalizationService.Mode.RoundToInt).Should().BeNull();
        }

        [Fact]
        public void CountDecimalDigits_BasicCases()
        {
            DataNormalizationService.CountDecimalDigits(0).Should().Be(0);
            DataNormalizationService.CountDecimalDigits(1).Should().Be(0);
            DataNormalizationService.CountDecimalDigits(1.5).Should().Be(1);
            DataNormalizationService.CountDecimalDigits(3.14).Should().Be(2);
            DataNormalizationService.CountDecimalDigits(0.001).Should().Be(3);
        }

        [Fact]
        public void Unknown_ModeReturnsInput()
        {
            double v = 42.5;
            DataNormalizationService.Normalize(v, (DataNormalizationService.Mode)99).Should().Be(v);
        }
    }
}
