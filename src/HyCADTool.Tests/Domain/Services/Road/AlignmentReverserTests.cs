using System;
using System.Collections.Generic;
using FluentAssertions;
using HyCADTool.Features.Road.PlanAlignment.Domain;
using HyCAD.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>
    /// T8 验收：<see cref="AlignmentReverser.ReversePiElements"/> 正确性。
    /// </summary>
    public class AlignmentReverserTests
    {
        [Fact]
        public void ThrowsOnNull()
        {
            Action act = () => AlignmentReverser.ReversePiElements(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void ReversedOrder_FirstBecomesLast()
        {
            var src = new[]
            {
                new PiElement(new Point2D(0, 0), tag: "BP"),
                new PiElement(new Point2D(100, 0), radius: 30, spiralIn: 5, spiralOut: 10, tag: "PI1"),
                new PiElement(new Point2D(200, 0), tag: "EP"),
            };

            var reversed = AlignmentReverser.ReversePiElements(src);

            reversed.Should().HaveCount(3);
            reversed[0].P.X.Should().Be(200);
            reversed[0].Tag.Should().Be("EP");
            reversed[2].P.X.Should().Be(0);
            reversed[2].Tag.Should().Be("BP");
        }

        [Fact]
        public void SpiralInAndOut_AreSwapped()
        {
            var src = new[]
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(100, 0), radius: 30, spiralIn: 5, spiralOut: 10),
                new PiElement(new Point2D(200, 100)),
            };

            var reversed = AlignmentReverser.ReversePiElements(src);

            // 原 PI1（index 1）反转后仍在 index 1，但缓和入/出要互换
            reversed[1].SpiralIn.Should().Be(10);
            reversed[1].SpiralOut.Should().Be(5);
            reversed[1].Radius.Should().Be(30, "半径标量不变");
        }

        [Fact]
        public void SourceListIsNotMutated()
        {
            var src = new[]
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(100, 0), spiralIn: 5, spiralOut: 10),
                new PiElement(new Point2D(200, 0)),
            };

            var _ = AlignmentReverser.ReversePiElements(src);

            src[0].P.X.Should().Be(0);
            src[2].P.X.Should().Be(200);
            src[1].SpiralIn.Should().Be(5);
            src[1].SpiralOut.Should().Be(10);
        }

        [Fact]
        public void DoubleReverse_RoundTrips()
        {
            var src = new[]
            {
                new PiElement(new Point2D(0, 0), tag: "BP"),
                new PiElement(new Point2D(100, 50), radius: 40, spiralIn: 8, spiralOut: 12, tag: "PI1"),
                new PiElement(new Point2D(200, 100), tag: "EP"),
            };

            var rev = AlignmentReverser.ReversePiElements(src);
            var back = AlignmentReverser.ReversePiElements(rev);

            for (int i = 0; i < src.Length; i++)
            {
                back[i].P.X.Should().Be(src[i].P.X);
                back[i].P.Y.Should().Be(src[i].P.Y);
                back[i].Radius.Should().Be(src[i].Radius);
                back[i].SpiralIn.Should().Be(src[i].SpiralIn);
                back[i].SpiralOut.Should().Be(src[i].SpiralOut);
                back[i].Tag.Should().Be(src[i].Tag);
            }
        }

        // ---------- A2 新增：ReverseStationEquations ----------

        [Fact]
        public void ReverseStationEquations_MirrorsBeforeRaw_KeepsAheadStation()
        {
            var eqs = new List<StationEquation>
            {
                new StationEquation(100, 1500),
                new StationEquation(800, 9000),
            };

            var rev = AlignmentReverser.ReverseStationEquations(eqs, totalRawLength: 1000);

            rev.Should().HaveCount(2);
            rev[0].BeforeRaw.Should().BeApproximately(200, 1e-9, "1000 − 800 = 200 (倒序 → 升序 → 此为新最小)");
            rev[0].AheadStation.Should().Be(9000);
            rev[1].BeforeRaw.Should().BeApproximately(900, 1e-9, "1000 − 100 = 900");
            rev[1].AheadStation.Should().Be(1500);
        }

        [Fact]
        public void ReverseStationEquations_DoubleReverse_RoundTrips()
        {
            var eqs = new List<StationEquation>
            {
                new StationEquation(100, 1500),
                new StationEquation(800, 9000),
            };

            var r1 = AlignmentReverser.ReverseStationEquations(eqs, 1000);
            var r2 = AlignmentReverser.ReverseStationEquations(r1, 1000);

            r2.Should().HaveCount(2);
            r2[0].BeforeRaw.Should().BeApproximately(100, 1e-9);
            r2[0].AheadStation.Should().Be(1500);
            r2[1].BeforeRaw.Should().BeApproximately(800, 1e-9);
            r2[1].AheadStation.Should().Be(9000);
        }

        [Fact]
        public void ReverseStationEquations_DropsNearEndpointEquations()
        {
            // BeforeRaw=999.999 镜像后 = 0.001（≤ tolerance=1e-3？实际默认 1e-6），仍保留
            // BeforeRaw=0.0005 镜像后 = 999.9995（仍 < 1000 − 1e-6 = 999.999999），保留
            // BeforeRaw=1000 - 1e-8 镜像后 = 1e-8（< 1e-6），剔除
            var eqs = new List<StationEquation>
            {
                new StationEquation(1000 - 1e-8, 123), // 应被剔除
                new StationEquation(500, 5000),        // 保留
            };

            var rev = AlignmentReverser.ReverseStationEquations(eqs, 1000);
            rev.Should().HaveCount(1);
            rev[0].BeforeRaw.Should().BeApproximately(500, 1e-9);
        }

        [Fact]
        public void ReverseStationEquations_EmptySource_ReturnsEmpty()
        {
            AlignmentReverser.ReverseStationEquations(null, 1000).Should().BeEmpty();
            AlignmentReverser.ReverseStationEquations(new List<StationEquation>(), 1000).Should().BeEmpty();
        }

        [Fact]
        public void ReverseStationEquations_NonPositiveLength_Throws()
        {
            Action act = () => AlignmentReverser.ReverseStationEquations(new List<StationEquation>(), 0);
            act.Should().Throw<ArgumentOutOfRangeException>();
            Action act2 = () => AlignmentReverser.ReverseStationEquations(new List<StationEquation>(), -1);
            act2.Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}
