using System.Collections.Generic;
using FluentAssertions;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.Services.Road
{
    /// <summary>
    /// T7 验收：<see cref="StationConverter.ToDisplayStation"/> 按方程映射 raw → 显示桩号。
    /// </summary>
    public class StationConverterTests
    {
        private const double Tol = 1e-9;

        [Fact]
        public void NoEquations_IsPurePassThrough()
        {
            StationConverter.ToDisplayStation(0, 0, null).Should().BeApproximately(0, Tol);
            StationConverter.ToDisplayStation(123, 0, null).Should().BeApproximately(123, Tol);
            StationConverter.ToDisplayStation(123, 1000, null).Should().BeApproximately(1123, Tol);
            StationConverter.ToDisplayStation(50, 0, new List<StationEquation>())
                .Should().BeApproximately(50, Tol);
        }

        [Fact]
        public void SingleEquation_BeforeRawApplies_WhenRawAtOrAfterBeforeRaw()
        {
            // K5+300 跳到 K6+000：eq.BeforeRaw = 5300, eq.AheadStation = 6000。
            // Alignment.StartStation = 0，raw=5300 映射 6000，raw=5400 映射 6100。
            var eqs = new List<StationEquation> { new StationEquation(5300, 6000) };
            StationConverter.ToDisplayStation(5299.9, 0, eqs).Should().BeApproximately(5299.9, 1e-6);
            StationConverter.ToDisplayStation(5300.0, 0, eqs).Should().BeApproximately(6000.0, Tol);
            StationConverter.ToDisplayStation(5400.0, 0, eqs).Should().BeApproximately(6100.0, Tol);
        }

        [Fact]
        public void TwoEquations_ComposeSequentially()
        {
            // 第一跳：raw=100 → display=1000 （Δ=+900）
            // 第二跳：raw=300 → display=5000 （Δ 相对于 1000+(300-100)=1200 也就是 +3800）
            var eqs = new List<StationEquation>
            {
                new StationEquation(100, 1000),
                new StationEquation(300, 5000),
            };
            StationConverter.ToDisplayStation(0, 0, eqs).Should().BeApproximately(0, Tol);
            StationConverter.ToDisplayStation(100, 0, eqs).Should().BeApproximately(1000, Tol);
            StationConverter.ToDisplayStation(200, 0, eqs).Should().BeApproximately(1100, Tol);
            StationConverter.ToDisplayStation(300, 0, eqs).Should().BeApproximately(5000, Tol);
            StationConverter.ToDisplayStation(500, 0, eqs).Should().BeApproximately(5200, Tol);
        }

        [Fact]
        public void StartStation_IsRespectedWhenNoApplicableEquation()
        {
            var eqs = new List<StationEquation> { new StationEquation(500, 9999) };
            StationConverter.ToDisplayStation(200, 1000, eqs)
                .Should().BeApproximately(1200, Tol, "raw < BeforeRaw → 仅叠加 StartStation");
        }

        [Fact]
        public void NegativeRaw_IsLeftAsRawPlusStart()
        {
            var eqs = new List<StationEquation> { new StationEquation(500, 9999) };
            StationConverter.ToDisplayStation(-10, 0, eqs).Should().BeApproximately(-10, Tol);
        }

        [Fact]
        public void ValidateAscending_RejectsDuplicateBeforeRaw()
        {
            var eqs = new List<StationEquation>
            {
                new StationEquation(100, 500),
                new StationEquation(100, 1000),
            };
            var (ok, errors) = StationConverter.ValidateAscending(eqs);
            ok.Should().BeFalse();
            errors.Should().NotBeEmpty();
        }

        [Fact]
        public void ValidateAscending_RejectsNegativeBeforeRaw()
        {
            var eqs = new List<StationEquation> { new StationEquation(-1, 100) };
            var (ok, _) = StationConverter.ValidateAscending(eqs);
            ok.Should().BeFalse();
        }

        [Fact]
        public void ValidateAscending_RejectsBeforeRawBeyondTotal()
        {
            var eqs = new List<StationEquation> { new StationEquation(1200, 1500) };
            var (ok, _) = StationConverter.ValidateAscending(eqs, totalRawLength: 1000);
            ok.Should().BeFalse();
        }

        [Fact]
        public void ValidateAscending_AcceptsStrictlyAscendingInRange()
        {
            var eqs = new List<StationEquation>
            {
                new StationEquation(100, 500),
                new StationEquation(300, 2000),
                new StationEquation(800, 10000),
            };
            var (ok, errors) = StationConverter.ValidateAscending(eqs, totalRawLength: 1000);
            ok.Should().BeTrue();
            errors.Should().BeEmpty();
        }

        [Fact]
        public void CloneSorted_SortsByBeforeRawAndDeepCopies()
        {
            var a = new StationEquation(300, 5000);
            var b = new StationEquation(100, 1000);
            var src = new List<StationEquation> { a, b };
            var cloned = StationConverter.CloneSorted(src);
            cloned.Should().HaveCount(2);
            cloned[0].BeforeRaw.Should().Be(100);
            cloned[1].BeforeRaw.Should().Be(300);
            cloned[0].BeforeRaw = 77;
            src[1].BeforeRaw.Should().Be(100, "深克隆后修改不影响源");
        }

        [Fact]
        public void CloneSorted_NullInputYieldsEmptyList()
        {
            StationConverter.CloneSorted(null).Should().BeEmpty();
        }

        // ---------- A1 新增：FromDisplayStation 逆函数 ----------

        [Fact]
        public void FromDisplayStation_NoEquations_IsPureOffset()
        {
            StationConverter.FromDisplayStation(0, 0, null).Should().BeApproximately(0, Tol);
            StationConverter.FromDisplayStation(1000, 0, null).Should().BeApproximately(1000, Tol);
            StationConverter.FromDisplayStation(1123, 1000, null).Should().BeApproximately(123, Tol);
            StationConverter.FromDisplayStation(50, 0, new List<StationEquation>())
                .Should().BeApproximately(50, Tol);
        }

        [Fact]
        public void FromDisplayStation_SingleEquation_InvertsToDisplay()
        {
            var eqs = new List<StationEquation> { new StationEquation(5300, 6000) };
            // display=6000 → raw=5300；display=6100 → raw=5400；display=5200 → raw=5200（方程前）
            StationConverter.FromDisplayStation(6000, 0, eqs).Should().BeApproximately(5300, Tol);
            StationConverter.FromDisplayStation(6100, 0, eqs).Should().BeApproximately(5400, Tol);
            StationConverter.FromDisplayStation(5200, 0, eqs).Should().BeApproximately(5200, Tol);
        }

        [Fact]
        public void FromDisplayStation_IsInverseOfToDisplay_WhenStrictlyAscending()
        {
            var eqs = new List<StationEquation>
            {
                new StationEquation(100, 1000),
                new StationEquation(300, 5000),
            };
            var raws = new[] { 0.0, 50.0, 99.99, 100.0, 100.01, 200.0, 299.99, 300.0, 300.01, 500.0 };
            foreach (var raw in raws)
            {
                var d = StationConverter.ToDisplayStation(raw, 0, eqs);
                var back = StationConverter.FromDisplayStation(d, 0, eqs);
                back.Should().BeApproximately(raw, 1e-6, $"raw={raw} 应为 toDisplay→from 的不动点");
            }
        }
    }
}
