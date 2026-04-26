using System;
using System.Linq;
using FluentAssertions;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>
    /// P3-I2 车道分界标线（<see cref="LaneMarkingDesigner"/>）几何单测。
    ///
    /// <para><b>验收要点</b></para>
    /// <list type="bullet">
    /// <item>构造校验（退化点、负线宽）；</item>
    /// <item><see cref="LaneMarkingDesigner.ComputeDashSegments"/>：段数 / 段长 / 段间距 / 末段截断 / 末段过短丢弃；</item>
    /// <item><see cref="LaneMarkingDesigner.ComputeDoubleOffsets"/>：左右两条平行线各偏移 spacing/2，方向正确；</item>
    /// <item><see cref="LaneMarkingDesigner.ExpandSegments"/>：按 <see cref="LaneMarkingKind"/> 正确分派。</item>
    /// </list>
    /// </summary>
    public class LaneMarkingDesignerTests
    {
        private static LaneMarking Horizontal(double length, LaneMarkingKind kind, double w = LaneMarking.DefaultStripeWidth)
            => LaneMarkingDesigner.Create(
                new Point2D(0, 0), new Point2D(length, 0), kind, w);

        // ---------------------------------------------------------------
        //  构造 / 校验
        // ---------------------------------------------------------------

        [Fact]
        public void Create_WithValidInputs_ReturnsLaneMarkingWithExpectedGeometry()
        {
            var m = LaneMarkingDesigner.Create(new Point2D(1, 1), new Point2D(11, 1), LaneMarkingKind.SolidWhite);
            m.Id.Should().NotBe(Guid.Empty);
            m.Start.X.Should().Be(1);
            m.End.X.Should().Be(11);
            m.Length.Should().BeApproximately(10, 1e-9);
            m.Center.X.Should().BeApproximately(6, 1e-9);
            m.Direction.X.Should().BeApproximately(1, 1e-9);
            m.Kind.Should().Be(LaneMarkingKind.SolidWhite);
            m.StripeWidth.Should().Be(LaneMarking.DefaultStripeWidth);
        }

        [Fact]
        public void Create_WithCoincidentPoints_Throws()
        {
            Action act = () => LaneMarkingDesigner.Create(new Point2D(0, 0), new Point2D(0, 0), LaneMarkingKind.SolidWhite);
            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-0.1)]
        public void Create_WithNonPositiveWidth_Throws(double w)
        {
            Action act = () => LaneMarkingDesigner.Create(new Point2D(0, 0), new Point2D(10, 0), LaneMarkingKind.SolidWhite, w);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        // ---------------------------------------------------------------
        //  ComputeDashSegments
        // ---------------------------------------------------------------

        [Fact]
        public void ComputeDashSegments_OnExactMultipleOfStep_ProducesExpectedCountAndLengths()
        {
            // L = 18，step = 2+4 = 6 → 3 个周期，段长都 = 2.0
            var m = Horizontal(18.0, LaneMarkingKind.DashedWhite);
            var segs = LaneMarkingDesigner.ComputeDashSegments(m, dashLength: 2.0, gapLength: 4.0);

            segs.Should().HaveCount(3);
            foreach (var s in segs)
            {
                var len = Math.Sqrt(Math.Pow(s.End.X - s.Start.X, 2) + Math.Pow(s.End.Y - s.Start.Y, 2));
                len.Should().BeApproximately(2.0, 1e-9);
            }
            segs[0].Start.X.Should().BeApproximately(0.0, 1e-9);
            segs[0].End.X.Should().BeApproximately(2.0, 1e-9);
            segs[1].Start.X.Should().BeApproximately(6.0, 1e-9);
            segs[1].End.X.Should().BeApproximately(8.0, 1e-9);
            segs[2].Start.X.Should().BeApproximately(12.0, 1e-9);
            segs[2].End.X.Should().BeApproximately(14.0, 1e-9);
        }

        [Fact]
        public void ComputeDashSegments_LastDashClippedByEnd_IsTruncatedOrDropped()
        {
            // L = 7，step = 6，dash = 2：
            //   周期 0: t=0 → dash [0,2]
            //   周期 1: t=6 → dash [6,7]（被截断为 1.0，大于默认 minDash=0.5，保留）
            var m = Horizontal(7.0, LaneMarkingKind.DashedWhite);
            var segs = LaneMarkingDesigner.ComputeDashSegments(m, 2.0, 4.0);

            segs.Should().HaveCount(2);
            segs[1].Start.X.Should().BeApproximately(6.0, 1e-9);
            segs[1].End.X.Should().BeApproximately(7.0, 1e-9);
        }

        [Fact]
        public void ComputeDashSegments_LastDashTooShort_IsDropped()
        {
            // L = 6.2，step = 6，dash = 2：
            //   周期 0: [0,2]
            //   周期 1: t=6 → dash [6, 6.2]（长度 0.2 < minDash=0.5 → 丢弃）
            var m = Horizontal(6.2, LaneMarkingKind.DashedWhite);
            var segs = LaneMarkingDesigner.ComputeDashSegments(m, 2.0, 4.0, minDashLength: 0.5);

            segs.Should().HaveCount(1);
            segs[0].End.X.Should().BeApproximately(2.0, 1e-9);
        }

        [Fact]
        public void ComputeDashSegments_SegmentShorterThanOneDash_ReturnsSingleClippedDash()
        {
            // L = 1.5（小于 dash=2）→ 单段被截断为 [0, 1.5]，> minDash
            var m = Horizontal(1.5, LaneMarkingKind.DashedWhite);
            var segs = LaneMarkingDesigner.ComputeDashSegments(m, 2.0, 4.0);

            segs.Should().HaveCount(1);
            segs[0].Start.X.Should().BeApproximately(0.0, 1e-9);
            segs[0].End.X.Should().BeApproximately(1.5, 1e-9);
        }

        [Fact]
        public void ComputeDashSegments_SupportsNon_AxisAligned_Direction()
        {
            // 45° 方向：dx=dy=√2/2；L=√200=14.142…
            var m = LaneMarkingDesigner.Create(
                new Point2D(0, 0), new Point2D(10, 10), LaneMarkingKind.DashedWhite);
            var segs = LaneMarkingDesigner.ComputeDashSegments(m, 2.0, 4.0);

            // 沿方向每段画段长度应为 2.0（欧几里得距离）
            foreach (var s in segs)
            {
                var len = Math.Sqrt(Math.Pow(s.End.X - s.Start.X, 2) + Math.Pow(s.End.Y - s.Start.Y, 2));
                len.Should().BeLessOrEqualTo(2.0 + 1e-9);
            }
            // 首段起点 = 原点；末段终点 ≤ 终点
            segs.First().Start.X.Should().BeApproximately(0, 1e-9);
            segs.Last().End.X.Should().BeLessOrEqualTo(10 + 1e-9);
        }

        [Theory]
        [InlineData(-1, 4)]
        [InlineData(2, 0)]
        public void ComputeDashSegments_NonPositiveParams_Throws(double dash, double gap)
        {
            var m = Horizontal(10, LaneMarkingKind.DashedWhite);
            Action act = () => LaneMarkingDesigner.ComputeDashSegments(m, dash, gap);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        // ---------------------------------------------------------------
        //  ComputeDoubleOffsets
        // ---------------------------------------------------------------

        [Fact]
        public void ComputeDoubleOffsets_OnHorizontalLine_ReturnsTwoParallelLinesOffsetByHalfSpacing()
        {
            var m = Horizontal(10, LaneMarkingKind.DoubleYellow);
            var (ls, le, rs, re) = LaneMarkingDesigner.ComputeDoubleOffsets(m, spacing: 0.2);

            // Direction=(1,0)，Perpendicular=(0,1) → Left 偏 +Y 0.1，Right 偏 -Y 0.1
            ls.Y.Should().BeApproximately(+0.1, 1e-9);
            le.Y.Should().BeApproximately(+0.1, 1e-9);
            rs.Y.Should().BeApproximately(-0.1, 1e-9);
            re.Y.Should().BeApproximately(-0.1, 1e-9);

            // X 相同（两条线的起/止 X 不变）
            ls.X.Should().BeApproximately(0.0, 1e-9);
            le.X.Should().BeApproximately(10.0, 1e-9);
            rs.X.Should().BeApproximately(0.0, 1e-9);
            re.X.Should().BeApproximately(10.0, 1e-9);
        }

        [Fact]
        public void ComputeDoubleOffsets_PreservesSegmentLength()
        {
            var m = LaneMarkingDesigner.Create(new Point2D(0, 0), new Point2D(6, 8), LaneMarkingKind.DoubleYellow);
            // 原段长 = 10
            var (ls, le, rs, re) = LaneMarkingDesigner.ComputeDoubleOffsets(m, spacing: 0.2);

            double leftLen = Math.Sqrt(Math.Pow(le.X - ls.X, 2) + Math.Pow(le.Y - ls.Y, 2));
            double rightLen = Math.Sqrt(Math.Pow(re.X - rs.X, 2) + Math.Pow(re.Y - rs.Y, 2));
            leftLen.Should().BeApproximately(10, 1e-9);
            rightLen.Should().BeApproximately(10, 1e-9);

            // 左右两线的距离 ≈ spacing
            double midDist = Math.Sqrt(Math.Pow(ls.X - rs.X, 2) + Math.Pow(ls.Y - rs.Y, 2));
            midDist.Should().BeApproximately(0.2, 1e-9);
        }

        [Fact]
        public void ComputeDoubleOffsets_NonPositiveSpacing_Throws()
        {
            var m = Horizontal(5, LaneMarkingKind.DoubleYellow);
            Action act = () => LaneMarkingDesigner.ComputeDoubleOffsets(m, 0);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        // ---------------------------------------------------------------
        //  ExpandSegments 分派
        // ---------------------------------------------------------------

        [Theory]
        [InlineData(LaneMarkingKind.SolidWhite)]
        [InlineData(LaneMarkingKind.SolidYellow)]
        public void ExpandSegments_ForSolid_ReturnsSingleSegment(LaneMarkingKind kind)
        {
            var m = Horizontal(10, kind);
            var segs = LaneMarkingDesigner.ExpandSegments(m);
            segs.Should().HaveCount(1);
            segs[0].Start.X.Should().Be(0);
            segs[0].End.X.Should().Be(10);
        }

        [Fact]
        public void ExpandSegments_ForDoubleYellow_ReturnsTwoParallel()
        {
            var m = Horizontal(10, LaneMarkingKind.DoubleYellow);
            var segs = LaneMarkingDesigner.ExpandSegments(m);
            segs.Should().HaveCount(2);
            // 两段 X 范围相同，Y 不同
            segs[0].Start.X.Should().BeApproximately(0, 1e-9);
            segs[0].End.X.Should().BeApproximately(10, 1e-9);
            segs[1].Start.X.Should().BeApproximately(0, 1e-9);
            segs[1].End.X.Should().BeApproximately(10, 1e-9);
            Math.Abs(segs[0].Start.Y - segs[1].Start.Y).Should().BeApproximately(0.2, 1e-9);
        }

        [Fact]
        public void ExpandSegments_ForDashedWhite_ReturnsMultipleDashes()
        {
            var m = Horizontal(18, LaneMarkingKind.DashedWhite);
            var segs = LaneMarkingDesigner.ExpandSegments(m);
            segs.Should().HaveCount(3);
        }
    }
}
