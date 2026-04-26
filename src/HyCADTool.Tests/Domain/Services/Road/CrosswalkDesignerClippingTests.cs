using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>
    /// v1.2 —— <see cref="CrosswalkDesigner.ClipStripeByCornerArcs"/> /
    /// <see cref="CrosswalkDesigner.ComputeStripesClipped"/> 的弧线裁切单测。
    ///
    /// <para>锁死从旧 <c>Infrastructure.AutoCAD.Services.CrosswalkService.RayHitArc</c> 迁移过来的
    /// "条纹遇到相邻 CornerArc 凸起时自动裁短"行为，以及若条纹被弧完全吃掉时返回空集。</para>
    /// </summary>
    public class CrosswalkDesignerClippingTests
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

        private static Intersection MakeCross(double r = 8, double hw = 7.5)
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

        // =============================== Ray × Arc ===============================

        [Fact]
        public void TryRayArcIntersect_ReturnsTrue_WhenRayCrossesArc()
        {
            var ix = MakeCross();
            var arc = ix.CornerArcs[0];

            // 从圆心出发沿任一方向，必穿过圆周一次（t = R）
            var ok = CrosswalkDesigner.TryRayArcIntersect(
                arc.Center, new Vector2D(System.Math.Cos(arc.StartAngle + arc.SweepAngle / 2),
                                          System.Math.Sin(arc.StartAngle + arc.SweepAngle / 2)),
                arc, out var hit);

            ok.Should().BeTrue();
            hit.DistanceTo(arc.Center).Should().BeApproximately(arc.Radius, 1e-6);
        }

        [Fact]
        public void TryRayArcIntersect_ReturnsFalse_WhenRayDoesNotHitArc()
        {
            var ix = MakeCross();
            var arc = ix.CornerArcs[0];

            // 从远处向远离圆心的反方向发射，不可能碰弧
            var origin = new Point2D(arc.Center.X + 1000, arc.Center.Y + 1000);
            var dir = new Vector2D(1, 0);
            var ok = CrosswalkDesigner.TryRayArcIntersect(origin, dir, arc, out _);

            ok.Should().BeFalse();
        }

        // =============================== ClipStripe ===============================

        [Fact]
        public void ClipStripe_NotInArc_ReturnsOriginalStripe()
        {
            // 远离所有弧的条纹，裁切应返回原条纹（等长）
            var ix = MakeCross();
            var stripe = new CrosswalkStripe(0, new Point2D(100, 100), new Point2D(105, 100));
            var clipped = CrosswalkDesigner.ClipStripeByCornerArcs(stripe, ix.CornerArcs);

            clipped.HasValue.Should().BeTrue();
            clipped.Value.Length.Should().BeApproximately(stripe.Length, 1e-9);
            clipped.Value.From.IsEqualTo(stripe.From, 1e-9).Should().BeTrue();
            clipped.Value.To.IsEqualTo(stripe.To, 1e-9).Should().BeTrue();
        }

        [Fact]
        public void ClipStripe_PassingThroughArc_IsShortened()
        {
            // 构造一条明确与某 CornerArc 相交的条纹。
            // 对称十字 R=8 hw=7.5：每个 CornerArc 圆心约在 (±0.5, ±0.5) 附近，R=8。
            // 取 CornerArc[0] 的圆心为参考，作一条过圆心的水平条纹 —— 必然穿弧 2 次
            var ix = MakeCross();
            var arc = ix.CornerArcs[0];

            var from = new Point2D(arc.Center.X - 20, arc.Center.Y);
            var to = new Point2D(arc.Center.X + 20, arc.Center.Y);
            var stripe = new CrosswalkStripe(0, from, to);
            double origLen = stripe.Length;

            var clipped = CrosswalkDesigner.ClipStripeByCornerArcs(stripe, new[] { arc });

            clipped.HasValue.Should().BeTrue("过圆心的水平条纹必与弧相交，应被裁短而非丢弃");
            clipped.Value.Length.Should().BeLessThan(origLen,
                $"裁切后长度应短于原长 {origLen} m（原条纹长度 40 m，弧切后最多 R 附近）");
        }

        [Fact]
        public void ClipStripe_CompletelyInsideArc_ReturnsNull_WhenTooShort()
        {
            // 极短条纹 + 放在完全被弧遮挡的位置 → 返回 null
            var ix = MakeCross();
            var arc = ix.CornerArcs[0];

            // 条纹穿过圆心但只有 1 mm 长；两端都在弧内侧 → 被弧吃掉或残存 < minLength
            var from = new Point2D(arc.Center.X - 0.0005, arc.Center.Y);
            var to = new Point2D(arc.Center.X + 0.0005, arc.Center.Y);
            var stripe = new CrosswalkStripe(0, from, to);

            var clipped = CrosswalkDesigner.ClipStripeByCornerArcs(
                stripe, new[] { arc }, minLength: 0.01);

            clipped.HasValue.Should().BeFalse("条纹长 1 mm < minLength 1 cm，应被吃掉");
        }

        // =============================== ComputeStripesClipped ===============================

        [Fact]
        public void ComputeStripesClipped_FallsBackToBasic_WhenNoArcs()
        {
            var ix = MakeCross();
            var cw = CrosswalkDesigner.BuildCrosswalkForLeg(ix, 0);
            var basic = CrosswalkDesigner.ComputeStripes(cw);
            var clipped = CrosswalkDesigner.ComputeStripesClipped(cw, new List<CornerArc>());

            clipped.Count.Should().Be(basic.Count, "无弧时 clipped 应退化为 basic 结果");
        }

        [Fact]
        public void ComputeStripesClipped_NeverExceeds_BasicCount()
        {
            var ix = MakeCross();
            var cw = CrosswalkDesigner.BuildCrosswalkForLeg(ix, 0);
            var basic = CrosswalkDesigner.ComputeStripes(cw);
            var clipped = CrosswalkDesigner.ComputeStripesClipped(cw, ix.CornerArcs);

            clipped.Count.Should().BeLessOrEqualTo(basic.Count,
                "裁切只会丢弃或缩短条纹，不可能新增");
        }

        [Fact]
        public void ComputeStripesClipped_ShortensSomeStripes_InHandCraftedScenario()
        {
            // 手工构造：Crosswalk 从 (0, 0) → (10, 0) 横向延伸，沿 +Y 向内推进 5 m。
            // 在条纹中段正上方放一个半径 3 m 的下半弧（π → 0，sweep=-π），
            // 弧面在 y ∈ [2, 5] 包围圈 —— 保证中段条纹必被裁短。
            var cw = new Crosswalk(
                legIndex: 0,
                baseLeft: new Point2D(0, 0),
                baseRight: new Point2D(10, 0),
                outward: new Vector2D(0, 1),
                gapWidth: 0.0,
                width: 5.0,
                stopLineDistance: 1.0,
                stripeSpacing: 1.0,
                stripeWidth: 0.4);

            // StartAngle=π → EndAngle=2π (CCW)，sweep=+π，扫描下半圆（经过 (5, 5-R) = (5, 2)）。
            var arc = new CornerArc(
                legIndexA: 0, legIndexB: 1,
                center: new Point2D(5, 5),
                radius: 3,
                startPoint: new Point2D(2, 5),
                endPoint: new Point2D(8, 5),
                startAngle: System.Math.PI,
                endAngle: 2 * System.Math.PI,
                sweepAngle: System.Math.PI);

            var basic = CrosswalkDesigner.ComputeStripes(cw);
            var clipped = CrosswalkDesigner.ComputeStripesClipped(cw, new[] { arc });

            basic.Should().OnlyContain(s => System.Math.Abs(s.Length - cw.Width) < 1e-6,
                "basic 条纹未裁切前每条 = Width = 5 m");

            bool someShortened = clipped.Any(s => s.Length < cw.Width - 1e-3);
            someShortened.Should().BeTrue(
                $"下半弧 y ∈ [2,5] 覆盖中段条纹，应至少有一条被裁到长度 < 5 m；实际最小 = {clipped.Min(s => s.Length):F3}");
        }

        // =============================== 边界 / 合约 ===============================

        [Fact]
        public void ClipStripe_Throws_WhenArcsNull()
        {
            var stripe = new CrosswalkStripe(0, new Point2D(0, 0), new Point2D(1, 0));
            ((System.Action)(() => CrosswalkDesigner.ClipStripeByCornerArcs(stripe, null)))
                .Should().Throw<System.ArgumentNullException>();
        }

        [Fact]
        public void ComputeStripesClipped_Throws_WhenArcsNull()
        {
            var ix = MakeCross();
            var cw = CrosswalkDesigner.BuildCrosswalkForLeg(ix, 0);
            ((System.Action)(() => CrosswalkDesigner.ComputeStripesClipped(cw, null)))
                .Should().Throw<System.ArgumentNullException>();
        }
    }
}
