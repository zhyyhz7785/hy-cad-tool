using System;
using System.Collections.Generic;
using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>
    /// P3-I1-C：<see cref="CurbRampDesigner"/> 纯几何单测。
    ///
    /// <para><b>验收要点</b></para>
    /// <list type="bullet">
    /// <item>每个 <see cref="CornerArc"/> 布置 1 个 Ramp；Ramp.FrontCenter 必位于 CornerArc 上（距 Center = Radius）；</item>
    /// <item>Ramp.OutwardNormal 指向人行道（v1.2 起 = <see cref="CornerArc.Center"/> → <c>FrontCenter</c>，
    /// 与 <see cref="CurbRampDesignerDirectionTests"/> 的物理方向断言相符）；</item>
    /// <item>Tangent ⟂ OutwardNormal（两者点积 ≈ 0）；</item>
    /// <item>BackCenter / FrontLeft / FrontRight 几何一致。</item>
    /// </list>
    /// </summary>
    public class CurbRampDesignerTests
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

        private static Intersection MakeCrossIntersection(double radius = 20, double halfWidth = 7.5)
        {
            var als = new List<Alignment>
            {
                MakeStraight(new Point2D(-100, 0), new Point2D(0, 0), "W"),
                MakeStraight(new Point2D(100, 0), new Point2D(0, 0), "E"),
                MakeStraight(new Point2D(0, 100), new Point2D(0, 0), "N"),
                MakeStraight(new Point2D(0, -100), new Point2D(0, 0), "S"),
            };
            return IntersectionDesigner.ComputeFromAlignments(als, new Point2D(0, 0), radius, halfWidth);
        }

        // ============================== 边界校验 ==============================

        [Fact]
        public void Throws_WhenIntersectionNull()
        {
            ((Action)(() => CurbRampDesigner.LayoutRampsOnCornerArcs(null)))
                .Should().Throw<ArgumentNullException>();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Throws_WhenWidthNonPositive(double width)
        {
            var ix = MakeCrossIntersection();
            ((Action)(() => CurbRampDesigner.LayoutRampsOnCornerArcs(ix, width: width)))
                .Should().Throw<ArgumentOutOfRangeException>();
        }

        // ============================== 布置数量 ==============================

        [Fact]
        public void Cross_Produces_OneRampPerCornerArc()
        {
            var ix = MakeCrossIntersection();
            ix.CornerArcs.Should().NotBeEmpty();

            var ramps = CurbRampDesigner.LayoutRampsOnCornerArcs(ix);

            ramps.Should().HaveCount(ix.CornerArcs.Count);
            ix.CurbRamps.Should().BeEquivalentTo(ramps, "Designer 必须同步写回 Intersection.CurbRamps");
        }

        // ============================== 几何正确性 ==============================

        [Fact]
        public void Ramp_FrontCenter_LiesOnCornerArcCircle()
        {
            var ix = MakeCrossIntersection();
            CurbRampDesigner.LayoutRampsOnCornerArcs(ix);

            for (int i = 0; i < ix.CurbRamps.Count; i++)
            {
                var ramp = ix.CurbRamps[i];
                var arc = ix.CornerArcs[ramp.CornerArcIndex];
                double dist = ramp.FrontCenter.DistanceTo(arc.Center);
                dist.Should().BeApproximately(arc.Radius, 1e-6,
                    $"Ramp#{i} FrontCenter 必须落在 CornerArc 圆周上（R={arc.Radius}）");
            }
        }

        [Fact]
        public void Ramp_OutwardNormal_PointsFrom_ArcCenter_To_FrontCenter()
        {
            // v1.2：修复 IntersectionDesigner 镜像 bug 后，CornerArc 的圆心在交叉口内部、
            // 人行道在弧外侧 —— OutwardNormal 必须从圆心 <b>向外</b> 指向 FrontCenter。
            // 旧断言（反号）是 v1.1 镜像几何下的"假绿"。
            var ix = MakeCrossIntersection();
            CurbRampDesigner.LayoutRampsOnCornerArcs(ix);

            foreach (var ramp in ix.CurbRamps)
            {
                var arc = ix.CornerArcs[ramp.CornerArcIndex];
                var expected = arc.Center.VectorTo(ramp.FrontCenter).TryNormalize(out var exp, 1e-9) ? exp : Vector2D.Zero;
                ramp.OutwardNormal.X.Should().BeApproximately(expected.X, 1e-6);
                ramp.OutwardNormal.Y.Should().BeApproximately(expected.Y, 1e-6);
            }
        }

        [Fact]
        public void Ramp_Tangent_IsPerpendicularToOutwardNormal()
        {
            var ix = MakeCrossIntersection();
            CurbRampDesigner.LayoutRampsOnCornerArcs(ix);

            foreach (var ramp in ix.CurbRamps)
            {
                double dot = ramp.Tangent.Dot(ramp.OutwardNormal);
                dot.Should().BeApproximately(0, 1e-9, "Tangent ⟂ OutwardNormal");
                ramp.Tangent.Length.Should().BeApproximately(1, 1e-9);
                ramp.OutwardNormal.Length.Should().BeApproximately(1, 1e-9);
            }
        }

        [Fact]
        public void Ramp_BackCenter_DistanceFromFront_EqualsDepth()
        {
            var ix = MakeCrossIntersection();
            CurbRampDesigner.LayoutRampsOnCornerArcs(ix, depth: 2.4);

            foreach (var ramp in ix.CurbRamps)
            {
                ramp.FrontCenter.DistanceTo(ramp.BackCenter).Should().BeApproximately(2.4, 1e-9);
            }
        }

        [Fact]
        public void Ramp_FrontLeftRight_AreSymmetricAroundFrontCenter()
        {
            var ix = MakeCrossIntersection();
            CurbRampDesigner.LayoutRampsOnCornerArcs(ix, width: 1.5);

            foreach (var ramp in ix.CurbRamps)
            {
                var mid = new Point2D(
                    (ramp.FrontLeft.X + ramp.FrontRight.X) / 2.0,
                    (ramp.FrontLeft.Y + ramp.FrontRight.Y) / 2.0);
                mid.IsEqualTo(ramp.FrontCenter, 1e-6).Should().BeTrue();
                ramp.FrontLeft.DistanceTo(ramp.FrontRight).Should().BeApproximately(1.5, 1e-9);
            }
        }

        // ============================== Kind / 尺寸透传 ==============================

        [Fact]
        public void Ramp_InheritsKindAndDimensions_FromArguments()
        {
            var ix = MakeCrossIntersection();
            CurbRampDesigner.LayoutRampsOnCornerArcs(
                ix,
                kind: CurbRampKind.Fan,
                width: 2.0,
                depth: 1.5,
                slope: 1.0 / 20.0);

            ix.CurbRamps.Should().OnlyContain(r =>
                r.Kind == CurbRampKind.Fan
                && Math.Abs(r.Width - 2.0) < 1e-9
                && Math.Abs(r.Depth - 1.5) < 1e-9
                && Math.Abs(r.Slope - 1.0 / 20.0) < 1e-9);
        }

        [Fact]
        public void Area_EqualsWidthTimesDepth()
        {
            var ramp = new CurbRamp(
                cornerArcIndex: 0,
                kind: CurbRampKind.SingleFace,
                frontCenter: new Point2D(0, 0),
                tangent: new Vector2D(1, 0),
                outwardNormal: new Vector2D(0, 1),
                width: 1.5,
                depth: 1.8);
            ramp.Area.Should().BeApproximately(1.5 * 1.8, 1e-9);
        }
    }
}
