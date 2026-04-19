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
    /// v1.1：<see cref="CrosswalkDesigner"/> 的几何单测。
    ///
    /// <para><b>验收要点</b></para>
    /// <list type="bullet">
    /// <item><see cref="CrosswalkDesigner.BuildCrosswalkForLeg"/> 的 BaseLeft / BaseRight 取相邻 CornerArc 切点；</item>
    /// <item><see cref="CrosswalkDesigner.LayoutForAllLegs"/> 每条 Leg 对应一个 Crosswalk；</item>
    /// <item><see cref="CrosswalkDesigner.ComputeStripes"/> 按 spacing 均布，首条 i=0 在 BaseLeft 侧；</item>
    /// <item>停止线两端点 = OuterLeft/Right + Outward · StopLineDistance；</item>
    /// <item>参数非法时抛异常。</item>
    /// </list>
    /// </summary>
    public class CrosswalkDesignerTests
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

        private static Intersection MakeCross(double r = 20, double hw = 7.5)
        {
            var als = new List<Alignment>
            {
                MakeStraight(new Point2D(-100, 0), new Point2D(0, 0), "W"),
                MakeStraight(new Point2D(100, 0), new Point2D(0, 0), "E"),
                MakeStraight(new Point2D(0, 100), new Point2D(0, 0), "N"),
                MakeStraight(new Point2D(0, -100), new Point2D(0, 0), "S"),
            };
            return IntersectionDesigner.ComputeFromAlignments(als, new Point2D(0, 0), r, hw);
        }

        // ============================== BuildCrosswalkForLeg ==============================

        [Fact]
        public void BuildCrosswalkForLeg_UsesCornerArcTangentPoints()
        {
            var ix = MakeCross();
            var cw = CrosswalkDesigner.BuildCrosswalkForLeg(ix, 0);

            var leftArc = ix.CornerArcs.Single(ca => ca.LegIndexA == 0);
            var rightArc = ix.CornerArcs.Single(ca => ca.LegIndexB == 0);

            cw.BaseLeft.IsEqualTo(leftArc.StartPoint, 1e-9).Should().BeTrue();
            cw.BaseRight.IsEqualTo(rightArc.EndPoint, 1e-9).Should().BeTrue();
            cw.LegIndex.Should().Be(0);
        }

        [Fact]
        public void BuildCrosswalkForLeg_CopiesLegOutward()
        {
            var ix = MakeCross();
            var cw = CrosswalkDesigner.BuildCrosswalkForLeg(ix, 1);
            cw.Outward.Equals(ix.Legs[1].InwardDirection).Should().BeTrue();
        }

        [Fact]
        public void BuildCrosswalkForLeg_DefaultsMatchCrosswalkConstants()
        {
            var ix = MakeCross();
            var cw = CrosswalkDesigner.BuildCrosswalkForLeg(ix, 0);
            cw.GapWidth.Should().Be(Crosswalk.DefaultGapWidth);
            cw.Width.Should().Be(Crosswalk.DefaultWidth);
            cw.StopLineDistance.Should().Be(Crosswalk.DefaultStopLineDistance);
            cw.StripeSpacing.Should().Be(Crosswalk.DefaultStripeSpacing);
            cw.StripeWidth.Should().Be(Crosswalk.DefaultStripeWidth);
        }

        // ============================== LayoutForAllLegs ==============================

        [Fact]
        public void LayoutForAllLegs_OneCrosswalkPerLeg_AndIndicesMatch()
        {
            var ix = MakeCross();
            var list = CrosswalkDesigner.LayoutForAllLegs(ix);
            list.Count.Should().Be(ix.Legs.Count);
            for (int i = 0; i < list.Count; i++)
                list[i].LegIndex.Should().Be(i);
        }

        [Fact]
        public void LayoutForAllLegs_CustomParams_PropagateToEveryCrosswalk()
        {
            var ix = MakeCross();
            var list = CrosswalkDesigner.LayoutForAllLegs(ix,
                gapWidth: 0.5, width: 4.0, stopLineDistance: 1.5, stripeSpacing: 0.45);

            foreach (var cw in list)
            {
                cw.GapWidth.Should().Be(0.5);
                cw.Width.Should().Be(4.0);
                cw.StopLineDistance.Should().Be(1.5);
                cw.StripeSpacing.Should().Be(0.45);
            }
        }

        // ============================== ComputeStripes ==============================

        [Fact]
        public void ComputeStripes_FirstStripe_AtBaseLeftPlusGapOffset()
        {
            var ix = MakeCross(hw: 7.5);
            var cw = CrosswalkDesigner.BuildCrosswalkForLeg(ix, 0,
                gapWidth: 1.0, width: 5.0, stopLineDistance: 2.0, stripeSpacing: 0.6);
            var stripes = CrosswalkDesigner.ComputeStripes(cw);
            stripes.Should().NotBeEmpty();
            stripes[0].Index.Should().Be(0);
            stripes[0].From.IsEqualTo(cw.InnerLeft, 1e-9).Should().BeTrue();
        }

        [Fact]
        public void ComputeStripes_EachStripe_LengthEqualsCrosswalkWidth()
        {
            var ix = MakeCross();
            var cw = CrosswalkDesigner.BuildCrosswalkForLeg(ix, 0, width: 5.0);
            var stripes = CrosswalkDesigner.ComputeStripes(cw);
            foreach (var s in stripes)
                s.Length.Should().BeApproximately(5.0, 1e-6);
        }

        [Fact]
        public void ComputeStripes_CountMatchesFloorRoadWidthOverSpacingPlusOne()
        {
            var ix = MakeCross(hw: 7.5); // RoadWidth = 2·HW = 15（两切点间距离；此场景 90° 十字下等于 HW*2 = 15）
            var cw = CrosswalkDesigner.BuildCrosswalkForLeg(ix, 0, stripeSpacing: 0.6);
            double expectedCount = Math.Floor(cw.RoadWidth / 0.6) + 1;
            var stripes = CrosswalkDesigner.ComputeStripes(cw);
            stripes.Count.Should().Be((int)expectedCount);
        }

        [Fact]
        public void ComputeStripes_AllIncrementsByStripeSpacing_AlongRoadDir()
        {
            var ix = MakeCross();
            var cw = CrosswalkDesigner.BuildCrosswalkForLeg(ix, 0, stripeSpacing: 0.6);
            var stripes = CrosswalkDesigner.ComputeStripes(cw);
            stripes.Count.Should().BeGreaterThan(1);
            double d01 = stripes[0].From.DistanceTo(stripes[1].From);
            d01.Should().BeApproximately(0.6, 1e-6);
        }

        // ============================== ComputeStopLine ==============================

        [Fact]
        public void ComputeStopLine_Matches_OuterPlusOutwardTimesStopLineDistance()
        {
            var ix = MakeCross();
            var cw = CrosswalkDesigner.BuildCrosswalkForLeg(ix, 0, stopLineDistance: 2.0);
            var (l, r) = CrosswalkDesigner.ComputeStopLine(cw);
            var expectL = cw.OuterLeft.Add(cw.Outward * 2.0);
            var expectR = cw.OuterRight.Add(cw.Outward * 2.0);
            l.IsEqualTo(expectL, 1e-9).Should().BeTrue();
            r.IsEqualTo(expectR, 1e-9).Should().BeTrue();
        }

        // ============================== 边界 / 校验 ==============================

        [Fact]
        public void BuildCrosswalkForLeg_OutOfRangeLegIndex_Throws()
        {
            var ix = MakeCross();
            ((Action)(() => CrosswalkDesigner.BuildCrosswalkForLeg(ix, -1)))
                .Should().Throw<ArgumentOutOfRangeException>();
            ((Action)(() => CrosswalkDesigner.BuildCrosswalkForLeg(ix, 99)))
                .Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void BuildCrosswalkForLeg_NullIntersection_Throws()
        {
            ((Action)(() => CrosswalkDesigner.BuildCrosswalkForLeg(null, 0)))
                .Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Crosswalk_Constructor_ValidatesNonPositiveWidth()
        {
            ((Action)(() => new Crosswalk(
                0, new Point2D(0, 0), new Point2D(1, 0), new Vector2D(0, 1),
                width: 0)))
                .Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Crosswalk_Constructor_ValidatesZeroOutward()
        {
            ((Action)(() => new Crosswalk(
                0, new Point2D(0, 0), new Point2D(1, 0), new Vector2D(0, 0))))
                .Should().Throw<ArgumentException>();
        }
    }
}
