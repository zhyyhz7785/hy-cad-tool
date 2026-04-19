using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.Services.Road
{
    /// <summary>
    /// P3-I1-C：<see cref="TactilePavingDesigner"/> 单测。
    ///
    /// <para><b>验收要点</b></para>
    /// <list type="bullet">
    /// <item>Stop 盲道数量 == CurbRamp 数；每条 Stop 中心线起止 = 对应 Ramp.FrontLeft/FrontRight；</item>
    /// <item>Advance 盲道数量 == 2 × Legs.Count（每条 Leg 左右各一）；</item>
    /// <item>Advance 中心线从 Leg.ApproachPoint 侧向偏 ≥ halfWidth + 0.25，沿 −Inward 延伸 L；</item>
    /// <item>所有盲道 IsValid。</item>
    /// </list>
    /// </summary>
    public class TactilePavingDesignerTests
    {
        private static Alignment MakeStraight(Point2D start, Point2D end, string name = null)
        {
            var cl = new Polyline3D(
                new[] { new Point3D(start.X, start.Y, 0), new Point3D(end.X, end.Y, 0) },
                isClosed: false,
                bulges: new[] { 0.0, 0.0 });
            return new Alignment
            {
                Name = name ?? $"{start}->{end}",
                StartStation = 0,
                Centerline = cl,
            };
        }

        private static Intersection MakeCrossIntersection()
        {
            var als = new List<Alignment>
            {
                MakeStraight(new Point2D(-100, 0), new Point2D(0, 0), "W"),
                MakeStraight(new Point2D(100, 0), new Point2D(0, 0), "E"),
                MakeStraight(new Point2D(0, 100), new Point2D(0, 0), "N"),
                MakeStraight(new Point2D(0, -100), new Point2D(0, 0), "S"),
            };
            var ix = IntersectionDesigner.ComputeFromAlignments(
                als, new Point2D(0, 0), defaultRadius: 20, defaultHalfWidth: 7.5);
            CurbRampDesigner.LayoutRampsOnCornerArcs(ix);
            return ix;
        }

        [Fact]
        public void Throws_WhenIntersectionNull()
        {
            ((Action)(() => TactilePavingDesigner.LayoutTactilePaving(null)))
                .Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Cross_Produces_OneStopPerRamp_AndTwoAdvancePerLeg()
        {
            var ix = MakeCrossIntersection();
            int rampCount = ix.CurbRamps.Count;
            int legCount = ix.Legs.Count;

            var pavings = TactilePavingDesigner.LayoutTactilePaving(ix);

            ix.TactilePavings.Should().BeEquivalentTo(pavings);
            pavings.Count(p => p.Kind == TactilePavingKind.Stop).Should().Be(rampCount);
            pavings.Count(p => p.Kind == TactilePavingKind.Advance).Should().Be(legCount * 2);
        }

        [Fact]
        public void Stop_Centerline_MatchesRamp_FrontLeftRight()
        {
            var ix = MakeCrossIntersection();
            TactilePavingDesigner.LayoutTactilePaving(ix);

            foreach (var stop in ix.TactilePavings.Where(p => p.Kind == TactilePavingKind.Stop))
            {
                var ramp = ix.CurbRamps.First(r => r.CornerArcIndex == stop.CornerArcIndex);
                stop.Centerline.Should().HaveCount(2);
                stop.Centerline[0].IsEqualTo(ramp.FrontLeft, 1e-6).Should().BeTrue();
                stop.Centerline[1].IsEqualTo(ramp.FrontRight, 1e-6).Should().BeTrue();
            }
        }

        [Fact]
        public void Stop_WidthDefaults_ToStopKindDefault()
        {
            var ix = MakeCrossIntersection();
            TactilePavingDesigner.LayoutTactilePaving(ix);

            foreach (var stop in ix.TactilePavings.Where(p => p.Kind == TactilePavingKind.Stop))
            {
                stop.Width.Should().BeApproximately(TactilePaving.DefaultStopWidth, 1e-9);
            }
        }

        [Fact]
        public void Advance_Centerline_OffsetsFromApproachPoint_ByHalfWidthPlusMinClear()
        {
            var ix = MakeCrossIntersection();
            const double L = 10.0;
            TactilePavingDesigner.LayoutTactilePaving(ix, advanceLengthFromApproach: L);

            var advList = ix.TactilePavings
                .Where(p => p.Kind == TactilePavingKind.Advance)
                .ToList();

            foreach (var leg in ix.Legs)
            {
                double offset = leg.HalfWidth + TactilePaving.MinDistanceFromKerb;
                var perp = leg.InwardDirection.Perpendicular();
                var expectedLeftStart = leg.ApproachPoint.Add(perp * offset);
                var expectedRightStart = leg.ApproachPoint.Add(perp * (-offset));

                // 每个 Leg 的"左 / 右"Advance 至少各存在一条（在 Cross 口 4 条 Alignment 共享 ApproachPoint 时，
                // 不同 Leg 的 start 可能跨 Leg 重合 —— 因此只断言"存在"而不强约束"恰好一条"）。
                advList.Should().Contain(p => p.Centerline[0].IsEqualTo(expectedLeftStart, 1e-6),
                    $"Leg({leg.Tag ?? leg.ApproachPoint.ToString()}) 左侧 Advance 起点应在 {expectedLeftStart}");
                advList.Should().Contain(p => p.Centerline[0].IsEqualTo(expectedRightStart, 1e-6),
                    $"Leg({leg.Tag ?? leg.ApproachPoint.ToString()}) 右侧 Advance 起点应在 {expectedRightStart}");
            }

            advList.Should().OnlyContain(p => Math.Abs(p.Length - L) < 1e-6);
        }

        [Fact]
        public void Advance_WidthDefaults_ToAdvanceKindDefault()
        {
            var ix = MakeCrossIntersection();
            TactilePavingDesigner.LayoutTactilePaving(ix);

            foreach (var adv in ix.TactilePavings.Where(p => p.Kind == TactilePavingKind.Advance))
            {
                adv.Width.Should().BeApproximately(TactilePaving.DefaultAdvanceWidth, 1e-9);
            }
        }

        [Fact]
        public void AllPavings_IsValid()
        {
            var ix = MakeCrossIntersection();
            TactilePavingDesigner.LayoutTactilePaving(ix);

            ix.TactilePavings.Should().NotBeEmpty();
            ix.TactilePavings.Should().OnlyContain(p => p.IsValid);
        }

        [Fact]
        public void CustomWidths_AreHonored()
        {
            var ix = MakeCrossIntersection();
            TactilePavingDesigner.LayoutTactilePaving(
                ix,
                stopWidth: 0.40,
                advanceWidth: 0.50);

            ix.TactilePavings
                .Where(p => p.Kind == TactilePavingKind.Stop)
                .Should().OnlyContain(p => Math.Abs(p.Width - 0.40) < 1e-9);
            ix.TactilePavings
                .Where(p => p.Kind == TactilePavingKind.Advance)
                .Should().OnlyContain(p => Math.Abs(p.Width - 0.50) < 1e-9);
        }
    }
}
