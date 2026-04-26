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
    /// v1.1：<see cref="KerbChainDesigner.ComputeKerbSegments"/> 的几何单测。
    ///
    /// <para><b>验收要点</b></para>
    /// <list type="bullet">
    /// <item>十字口（4 臂 90°）→ 2N = 8 段（全部有效）；</item>
    /// <item>From = ApproachPoint ± Perpendicular(uA) · HalfWidth，严格吻合；</item>
    /// <item>To 对应相邻 CornerArc 的 StartPoint（Left）/ EndPoint（Right）；</item>
    /// <item>方向与 −InwardDirection 同向（Dot &gt; 0）；</item>
    /// <item>空交叉口 / 无 CornerArc → 空集合。</item>
    /// </list>
    /// </summary>
    public class KerbChainDesignerTests
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

        // ============================== 段数 ==============================

        [Fact]
        public void Cross_Produces_2N_KerbSegments()
        {
            var ix = MakeCross();
            var segs = KerbChainDesigner.ComputeKerbSegments(ix);
            segs.Should().HaveCount(2 * ix.Legs.Count);
        }

        [Fact]
        public void Cross_EachLeg_HasOneLeftAndOneRightSegment()
        {
            var ix = MakeCross();
            var segs = KerbChainDesigner.ComputeKerbSegments(ix).ToList();
            for (int i = 0; i < ix.Legs.Count; i++)
            {
                segs.Count(s => s.LegIndex == i && s.Side == KerbSide.Left).Should().Be(1);
                segs.Count(s => s.LegIndex == i && s.Side == KerbSide.Right).Should().Be(1);
            }
        }

        // ============================== From 锚点 ==============================

        [Fact]
        public void From_Anchor_IsApproachPlusPerpendicularHalfWidth()
        {
            var ix = MakeCross();
            var segs = KerbChainDesigner.ComputeKerbSegments(ix);

            foreach (var s in segs)
            {
                var leg = ix.Legs[s.LegIndex];
                var perp = leg.InwardDirection.Perpendicular();
                var expected = s.Side == KerbSide.Left
                    ? leg.ApproachPoint.Add(perp * leg.HalfWidth)
                    : leg.ApproachPoint.Add(perp * -leg.HalfWidth);
                s.From.IsEqualTo(expected, 1e-9).Should().BeTrue(
                    $"Leg#{s.LegIndex} {s.Side}：From 锚点应在 ApproachPoint 侧向投影");
            }
        }

        // ============================== To 切点对齐 CornerArc ==============================

        [Fact]
        public void To_MapsTo_CornerArcStartOrEnd()
        {
            // v1.2 语义对齐：修复后的 IntersectionDesigner 里
            //   StartPoint ∈ LegIndexA 的 <b>右</b>侧外边线  →  配 KerbSide.Right
            //   EndPoint   ∈ LegIndexB 的 <b>左</b>侧外边线  →  配 KerbSide.Left
            var ix = MakeCross();
            var segs = KerbChainDesigner.ComputeKerbSegments(ix);

            foreach (var s in segs)
            {
                if (s.Side == KerbSide.Left)
                {
                    var arc = ix.CornerArcs.Single(ca => ca.LegIndexB == s.LegIndex);
                    s.To.IsEqualTo(arc.EndPoint, 1e-9).Should().BeTrue(
                        $"Leg#{s.LegIndex} Left：切点 = Arc(LegIndexB==i).EndPoint");
                }
                else
                {
                    var arc = ix.CornerArcs.Single(ca => ca.LegIndexA == s.LegIndex);
                    s.To.IsEqualTo(arc.StartPoint, 1e-9).Should().BeTrue(
                        $"Leg#{s.LegIndex} Right：切点 = Arc(LegIndexA==i).StartPoint");
                }
            }
        }

        // ============================== 方向：与 +InwardDirection 同向 ==============================
        // IntersectionLeg.InwardDirection 定义 = "从 ApproachPoint 指向交叉口中心"。KerbChainDesigner 要求
        // 切点在锚点的 +InwardDirection 侧，即 (To − From) · InwardDir > 0。

        [Fact]
        public void Segment_PointsAlongInwardDirection()
        {
            var ix = MakeCross();
            var segs = KerbChainDesigner.ComputeKerbSegments(ix);

            foreach (var s in segs)
            {
                var leg = ix.Legs[s.LegIndex];
                double dx = s.To.X - s.From.X;
                double dy = s.To.Y - s.From.Y;
                double proj = dx * leg.InwardDirection.X + dy * leg.InwardDirection.Y;
                proj.Should().BeGreaterThan(0,
                    $"Leg#{s.LegIndex} {s.Side}：Kerb 段应沿 +InwardDirection（Leg 外延方向）延伸");
            }
        }

        [Fact]
        public void Segment_Length_ForSquareCross_IsTangentMinusHalfWidth()
        {
            // 方形十字（θ = π/2, 等宽）+ R=20, HW=7.5。v1.2 修正后：
            //   两 Leg 物理相邻一侧外边线延长交点 X 位于 (-HW, -HW)（修复前错置在 +HW, +HW 对角镜像）；
            //   Leg ApproachPoint=(0,0) 的侧向锚点 = (0, ±HW) 与 X 同侧；
            //   切点 = X + uA·T，其中 T = R/tan(π/4) = R = 20，位于 X 的 +uA 方向 T 米处；
            //   故段长 = 锚点沿外边线（垂直 perp 方向）到切点的距离 = T − HW = 12.5。
            var ix = MakeCross(r: 20, hw: 7.5);
            var segs = KerbChainDesigner.ComputeKerbSegments(ix);

            foreach (var s in segs)
            {
                s.Length.Should().BeApproximately(12.5, 1e-6);
            }
        }

        // ============================== 边界 ==============================

        [Fact]
        public void EmptyIntersection_Returns_Empty()
        {
            var ix = new Intersection();
            KerbChainDesigner.ComputeKerbSegments(ix).Should().BeEmpty();
        }

        [Fact]
        public void Null_Throws()
        {
            ((System.Action)(() => KerbChainDesigner.ComputeKerbSegments(null)))
                .Should().Throw<System.ArgumentNullException>();
        }

        [Fact]
        public void HasKerbChain_Defaults_ToFalse()
        {
            new Intersection().HasKerbChain.Should().BeFalse();
        }
    }
}
