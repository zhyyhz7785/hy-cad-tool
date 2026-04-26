using FluentAssertions;
using HyCADTool.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.ValueObjects.Road
{
    /// <summary>
    /// T7 验收：<see cref="StationEquation"/> 构造 / 克隆。
    /// </summary>
    public class StationEquationTests
    {
        [Fact]
        public void DefaultCtor_YieldsZeroPair()
        {
            var eq = new StationEquation();
            eq.BeforeRaw.Should().Be(0);
            eq.AheadStation.Should().Be(0);
        }

        [Fact]
        public void Ctor_StoresArguments()
        {
            var eq = new StationEquation(123.456, 999.999);
            eq.BeforeRaw.Should().Be(123.456);
            eq.AheadStation.Should().Be(999.999);
        }

        [Fact]
        public void Clone_CreatesIndependentCopy()
        {
            var a = new StationEquation(10, 200);
            var b = a.Clone();
            b.BeforeRaw.Should().Be(10);
            b.AheadStation.Should().Be(200);
            b.BeforeRaw = 11;
            a.BeforeRaw.Should().Be(10, "克隆体修改不影响原件");
        }

        [Fact]
        public void ToString_IncludesValues()
        {
            var eq = new StationEquation(50, 1000);
            eq.ToString().Should().Contain("50").And.Contain("1000");
        }
    }
}
