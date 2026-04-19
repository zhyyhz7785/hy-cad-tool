using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.Services.Road
{
    /// <summary>
    /// v1.2 —— <see cref="IntersectionDesigner.TryBuildCornerArc"/> 的 <b>位置性</b>断言。
    ///
    /// <para><b>为什么需要</b></para>
    /// v1.1 之前的几何测试（<see cref="IntersectionDesignerTests"/>）只断言
    /// "切点到无限直线距离 = HalfWidth"，与切点所处的象限无关 —— 因此无法区分 corner 算到了
    /// 物理相邻的角还是对角象限。本文件通过指定 <b>非对称</b>（W + S 两臂）+ <b>非退化</b>
    /// （ApproachPoint ≠ aroundPoint）场景下的期望坐标，把"镜像对角错位" bug 锁死。
    ///
    /// <para><b>参考几何</b></para>
    /// - W: 从 (-100, 0) 到 (-5, 0)，ApproachPoint = (-5, 0)，InwardDirection = (+1, 0)（朝中心 (0,0)）。
    /// - S: 从 (0, -100) 到 (0, -5)，ApproachPoint = (0, -5)，InwardDirection = (0, +1)。
    /// - R = 8, HalfWidth = 7.5. CCW 排序后 Legs[0]=W, Legs[1]=S.
    /// - W/S 两条外边线 y=+7.5 和 x=+7.5 在代数上于 (7.5, 7.5) 相交，但 <b>物理</b> corner 应该在西南
    ///   （第三象限）—— 与两条 Leg 的物理位置（x≤-5, y≤-5）对应的是两条 Leg <b>面向交叉口那一侧</b>
    ///   路缘外边线的延长交点（-7.5, -7.5），其切点对应的圆心应在 (-0.5, -0.5) 附近。
    /// </summary>
    public class IntersectionDesignerCornerPositionTests
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

        private static Intersection NonDegenerateWestSouth()
        {
            var als = new List<Alignment>
            {
                MakeStraight(new Point2D(-100, 0), new Point2D(-5, 0), "W"),
                MakeStraight(new Point2D(0, -100), new Point2D(0, -5), "S"),
            };
            return IntersectionDesigner.ComputeFromAlignments(
                als, new Point2D(0, 0), defaultRadius: 8, defaultHalfWidth: 7.5);
        }

        [Fact]
        public void CornerArc_Center_Lies_InPhysicalCorner_NotInDiagonalMirror()
        {
            var ix = NonDegenerateWestSouth();
            ix.CornerArcs.Should().HaveCount(1,
                "W 和 S 为 CCW 相邻仅有一对，Arc[1]=(S,W) 因 theta<=0 应被剔除");

            var arc = ix.CornerArcs[0];

            // 物理西南 corner 的圆心应当在 W/S 两 Leg 物理所在象限（第三象限附近）。
            arc.Center.X.Should().BeLessThan(0,
                "W 物理在 x≤-5 一带；CornerArc 作为 W/S 相邻处的圆角，其圆心 X 应 <=0");
            arc.Center.Y.Should().BeLessThan(0,
                "S 物理在 y≤-5 一带；CornerArc 作为 W/S 相邻处的圆角，其圆心 Y 应 <=0");

            // 数值期望：内角 90°，T=R=8, D=R√2≈11.3，bisUnit=(1,1)/√2 朝 Leg 对面的 corner；
            // 正确实现下 center = bisUnit 反向偏移 = (-0.5, -0.5)。
            arc.Center.DistanceTo(new Point2D(-0.5, -0.5))
                .Should().BeLessThan(0.05, "90° 十字对称场景下圆心应在 (-0.5, -0.5)");
        }

        [Fact]
        public void CornerArc_Tangents_Lie_On_Legs_Physical_CurbLines()
        {
            var ix = NonDegenerateWestSouth();
            var arc = ix.CornerArcs[0];

            // StartPoint 在 Leg W 的物理外边线上：y 应该接近 HalfWidth = 7.5 或 -7.5，x 在 W 的存在区间内（x≤0）。
            System.Math.Abs(System.Math.Abs(arc.StartPoint.Y) - 7.5).Should().BeLessThan(1e-3);
            arc.StartPoint.X.Should().BeLessThan(0, "StartPoint 应在 W 的物理外边线上，x<0");

            // EndPoint 在 Leg S 的物理外边线上：x 接近 ±7.5，y≤0。
            System.Math.Abs(System.Math.Abs(arc.EndPoint.X) - 7.5).Should().BeLessThan(1e-3);
            arc.EndPoint.Y.Should().BeLessThan(0, "EndPoint 应在 S 的物理外边线上，y<0");
        }

        [Fact]
        public void CornerArc_Center_IsInward_From_Both_Legs_Approach_Points()
        {
            var ix = NonDegenerateWestSouth();
            var arc = ix.CornerArcs[0];
            var legA = ix.Legs[arc.LegIndexA];
            var legB = ix.Legs[arc.LegIndexB];

            // 圆心应位于 Leg A 外侧（从 ApproachPoint 沿 -InwardDirection 前进侧），
            // 因为它是"两 Leg 背离中心那一侧外边线延长线"构造的圆角。
            var vA = legA.ApproachPoint.VectorTo(arc.Center);
            var dotA = vA.X * legA.InwardDirection.X + vA.Y * legA.InwardDirection.Y;
            dotA.Should().BeLessThan(0,
                "圆心在 Leg A ApproachPoint 的 -InwardDirection 侧（即远离交叉口中心）");

            var vB = legB.ApproachPoint.VectorTo(arc.Center);
            var dotB = vB.X * legB.InwardDirection.X + vB.Y * legB.InwardDirection.Y;
            dotB.Should().BeLessThan(0,
                "圆心在 Leg B ApproachPoint 的 -InwardDirection 侧");
        }
    }
}
