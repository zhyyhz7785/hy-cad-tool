using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.Geometry;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>
    /// v1.2 —— <see cref="IntersectionDesigner.TryBuildCornerArc"/> 的 <b>位置性</b>断言。
    ///
    /// <para><b>为什么需要</b></para>
    /// v1.1 之前的几何测试（<see cref="IntersectionDesignerTests"/>）只断言
    /// "切点到无限直线距离 = HalfWidth"，与切点所处的象限 / 外边线侧别无关 —— 因此无法区分
    /// corner 算到了物理相邻的 corner 还是对角镜像。本文件通过指定 <b>非对称</b>（W + S 两臂）+
    /// <b>非退化</b>（ApproachPoint ≠ aroundPoint）场景下的期望象限与外边线侧别，把镜像 bug 锁死。
    ///
    /// <para><b>参考几何</b></para>
    /// W: <c>(-100, 0) → (-5, 0)</c>，ApproachPoint = (-5, 0)，InwardDirection = (+1, 0)；<br/>
    /// S: <c>(0, -100) → (0, -5)</c>，ApproachPoint = (0, -5)，InwardDirection = (0, +1)；<br/>
    /// CCW 排序后 Legs[0] = W, Legs[1] = S. R = 8, HalfWidth = 7.5.
    /// <para><b>物理 corner</b> 位于 W-S 相邻的西南方 —— 外边线 <c>y = -7.5</c>（W 南侧）与
    /// <c>x = -7.5</c>（S 西侧）相交于 <c>(-7.5, -7.5)</c>，圆心沿内角平分线 (+1, +1)/√2 朝
    /// <b>交叉口内部</b>（东北方向）偏移 D = R·√2 = 8√2，得 Center ≈ <c>(0.5, 0.5)</c>。</para>
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

        /// <summary>StartPoint 在 Leg W <b>朝 S 那一侧</b>（即物理相邻侧）外边线 <c>y = -7.5</c> 上，
        /// 而不是镜像的 <c>y = +7.5</c>。</summary>
        [Fact]
        public void StartPoint_On_LegA_PhysicallyAdjacent_CurbLine()
        {
            var ix = NonDegenerateWestSouth();
            ix.CornerArcs.Should().HaveCount(1);
            var arc = ix.CornerArcs[0];
            arc.StartPoint.Y.Should().BeApproximately(
                -7.5, 1e-3,
                "Leg W 的 CornerArc 起点切在 W 的南侧外边线 y=-7.5 上（与 S 物理相邻的一侧），"
                + "而非对角镜像的 y=+7.5（W 背对 S 的一侧）");
        }

        /// <summary>EndPoint 在 Leg S <b>朝 W 那一侧</b>（即物理相邻侧）外边线 <c>x = -7.5</c> 上。</summary>
        [Fact]
        public void EndPoint_On_LegB_PhysicallyAdjacent_CurbLine()
        {
            var ix = NonDegenerateWestSouth();
            var arc = ix.CornerArcs[0];
            arc.EndPoint.X.Should().BeApproximately(
                -7.5, 1e-3,
                "Leg S 的 CornerArc 终点切在 S 的西侧外边线 x=-7.5 上（与 W 物理相邻的一侧），"
                + "而非对角镜像的 x=+7.5");
        }

        /// <summary>圆心应在交叉口内侧（由 W-S 两外边线的内角朝东北，距 (-7.5,-7.5) 约 D=8√2）。</summary>
        [Fact]
        public void Center_In_Intersection_Interior_Not_Mirrored_Far_Outside()
        {
            var ix = NonDegenerateWestSouth();
            var arc = ix.CornerArcs[0];
            arc.Center.DistanceTo(new Point2D(0.5, 0.5))
                .Should().BeLessThan(0.05,
                    "对称 90° 场景下修复后 Center 应在 (0.5, 0.5)（交叉口内部，靠近原点）；"
                    + "未修复时 Center 落在对角镜像 (15.5, 15.5) 附近");
        }

        /// <summary>圆弧的"扫出几何区域"必须覆盖 W-S 物理西南 corner —— 圆弧上的中点应落在第三象限。</summary>
        [Fact]
        public void Arc_MidPoint_Lies_In_Physical_SouthWest_Corner()
        {
            var ix = NonDegenerateWestSouth();
            var arc = ix.CornerArcs[0];

            // 圆弧中点 = Center + R · (AngleBisector of Start/End) —— 走 CW 的短弧。
            double midAngle = 0.5 * (arc.StartAngle + arc.EndAngle);
            // 若 sweep 跨越 ±π 的边界则取补角
            double rawDiff = arc.EndAngle - arc.StartAngle;
            if (rawDiff > System.Math.PI) midAngle -= System.Math.PI;
            else if (rawDiff < -System.Math.PI) midAngle += System.Math.PI;

            double mx = arc.Center.X + arc.Radius * System.Math.Cos(midAngle);
            double my = arc.Center.Y + arc.Radius * System.Math.Sin(midAngle);

            mx.Should().BeLessThan(0, "弧中点应落在西南角：x<0");
            my.Should().BeLessThan(0, "弧中点应落在西南角：y<0");
        }
    }
}
