using System;
using FluentAssertions;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.Geometry;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    public class RoadGeometryFormulasTests
    {
        private const double Eps = 1e-6;

        [Fact]
        public void TurnAngle_LeftTurn90Deg_ReturnsPositivePiOverTwo()
        {
            var prev = new Point2D(0, 0);
            var pi = new Point2D(100, 0);
            var next = new Point2D(100, 100);

            double turn = RoadGeometryFormulas.TurnAngle(prev, pi, next);
            turn.Should().BeApproximately(Math.PI / 2.0, Eps);
        }

        [Fact]
        public void TurnAngle_RightTurn90Deg_ReturnsNegativePiOverTwo()
        {
            var prev = new Point2D(0, 0);
            var pi = new Point2D(100, 0);
            var next = new Point2D(100, -100);

            double turn = RoadGeometryFormulas.TurnAngle(prev, pi, next);
            turn.Should().BeApproximately(-Math.PI / 2.0, Eps);
        }

        [Fact]
        public void TurnAngle_Collinear_ReturnsZero()
        {
            var prev = new Point2D(0, 0);
            var pi = new Point2D(50, 0);
            var next = new Point2D(100, 0);

            double turn = RoadGeometryFormulas.TurnAngle(prev, pi, next);
            turn.Should().BeApproximately(0, Eps);
        }

        [Fact]
        public void InnerShift_ZeroInputs_ReturnsZero()
        {
            RoadGeometryFormulas.InnerShift(0, 70).Should().Be(0);
            RoadGeometryFormulas.InnerShift(200, 0).Should().Be(0);
            RoadGeometryFormulas.InnerShift(-1, 70).Should().Be(0);
        }

        [Fact]
        public void InnerShift_StandardCase_MatchesFormula()
        {
            // 鸿业截图 JD1：R=200, Ls=70
            double p = RoadGeometryFormulas.InnerShift(200, 70);
            double expected = 70.0 * 70.0 / (24.0 * 200.0)
                              - (70.0 * 70.0 * 70.0 * 70.0) / (2688.0 * 200.0 * 200.0 * 200.0);
            p.Should().BeApproximately(expected, 1e-9);
        }

        [Fact]
        public void TangentShift_StandardCase_MatchesFormula()
        {
            double q = RoadGeometryFormulas.TangentShift(200, 70);
            double expected = 70.0 / 2.0 - (70.0 * 70.0 * 70.0) / (240.0 * 200.0 * 200.0);
            q.Should().BeApproximately(expected, 1e-9);
        }

        [Fact]
        public void TangentLengthSymmetric_MatchesHongyeSample()
        {
            // 鸿业截图 JD1：θ=41.187°, R=200, Ls=70 → T=110.4958（截图界面显示）
            // 鸿业 UI 对转角可能只截取到 3 位小数，T 容差放到 0.02 m 足矣。
            double turnRad = 41.187 * Math.PI / 180.0;
            double T = RoadGeometryFormulas.TangentLengthSymmetric(200, 70, turnRad);
            T.Should().BeApproximately(110.4958, 2e-2);
        }

        [Fact]
        public void CircularArcLength_MatchesHongyeSample()
        {
            // 鸿业截图 JD1：θ=41.187°, R=200, Ls1=Ls2=70 → Ly≈73.7684（界面显示）
            // Ly = R·θ - (Ls1+Ls2)/2，理论 73.7698；鸿业 UI 四舍五入差 0.002 m，精度放宽到 5e-3。
            double turnRad = 41.187 * Math.PI / 180.0;
            double ly = RoadGeometryFormulas.CircularArcLength(200, 70, 70, turnRad);
            ly.Should().BeApproximately(73.7684, 5e-3);
        }

        [Fact]
        public void CircularArcLength_SpiralsLongerThanCurve_ReturnsZero()
        {
            // R=50, θ=10° → R*θ ≈ 8.73 m；Ls1+Ls2 = 40 远大于 2*Rθ = 17.46 → Ly 被吃光
            double turnRad = 10.0 * Math.PI / 180.0;
            double ly = RoadGeometryFormulas.CircularArcLength(50, 20, 20, turnRad);
            ly.Should().Be(0);
        }

        [Fact]
        public void CircularArcLength_NoSpirals_EqualsRTimesTurn()
        {
            double turnRad = Math.PI / 2.0; // 90°
            double ly = RoadGeometryFormulas.CircularArcLength(20, 0, 0, turnRad);
            ly.Should().BeApproximately(20 * Math.PI / 2.0, Eps);
        }

        [Fact]
        public void AsymmetricLs_T1AndT2_Differ()
        {
            double turnRad = Math.PI / 4.0; // 45°
            double t1 = RoadGeometryFormulas.TangentInLength(200, 50, turnRad);
            double t2 = RoadGeometryFormulas.TangentOutLength(200, 100, turnRad);
            // Ls 大的一侧 T 略大（q 的贡献）
            t2.Should().BeGreaterThan(t1);
        }

        [Fact]
        public void FormatTurn_LeftAndRight_UseChineseLabels()
        {
            RoadGeometryFormulas.FormatTurn(Math.PI / 6.0).Should().StartWith("左偏");
            RoadGeometryFormulas.FormatTurn(-Math.PI / 6.0).Should().StartWith("右偏");
            RoadGeometryFormulas.FormatTurn(0).Should().StartWith("直行");
        }

        [Fact]
        public void SpiralParameterA_SqrtRLs()
        {
            RoadGeometryFormulas.SpiralParameterA(200, 70)
                .Should().BeApproximately(Math.Sqrt(200 * 70), 1e-9);
            RoadGeometryFormulas.SpiralParameterA(0, 70).Should().Be(0);
            RoadGeometryFormulas.SpiralParameterA(200, 0).Should().Be(0);
        }
    }
}
