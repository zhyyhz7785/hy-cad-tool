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
    /// P3-I1-A：<see cref="IntersectionDesigner"/> 纯几何算法单测。
    /// 几何场景：
    /// - 十字交叉（Cross）：2 条正交直线，4 臂。
    /// - T 形交叉（TShape）：1 条横线 + 1 条南向支路。
    /// - Y 形交叉：3 条分别成 120° 夹角的 Alignment。
    /// - 斜交：主线水平 + 支路 60° 接入。
    /// </summary>
    public class IntersectionDesignerTests
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

        [Fact]
        public void Throws_WhenLessThanTwoAlignments()
        {
            ((Action)(() => IntersectionDesigner.ComputeFromAlignments(
                new[] { MakeStraight(new Point2D(0, 0), new Point2D(100, 0)) },
                new Point2D(50, 0))))
                .Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Throws_WhenRadiusNonPositive()
        {
            var als = new[]
            {
                MakeStraight(new Point2D(-100, 0), new Point2D(0, 0)),
                MakeStraight(new Point2D(0, -100), new Point2D(0, 0)),
            };
            ((Action)(() => IntersectionDesigner.ComputeFromAlignments(als, new Point2D(0, 0), 0)))
                .Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Cross_FourLegs_Ordered_CCW_ByInwardAngle()
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

            ix.Legs.Should().HaveCount(4);

            double[] expectedDegCcw = { 0, 90, 180, 270 };
            for (int i = 0; i < 4; i++)
            {
                double deg = ix.Legs[i].InwardAngle * 180.0 / Math.PI;
                double norm = deg < 0 ? deg + 360 : deg;
                Math.Abs(norm - expectedDegCcw[i]).Should().BeLessThan(1.0,
                    $"Leg {i} InwardAngle {norm}° should match {expectedDegCcw[i]}°");
            }

            ix.CornerArcs.Should().HaveCount(4);
        }

        [Fact]
        public void Cross_DefaultRadius_AllArcsUseSameRadius()
        {
            var als = new List<Alignment>
            {
                MakeStraight(new Point2D(-100, 0), new Point2D(0, 0), "W"),
                MakeStraight(new Point2D(100, 0), new Point2D(0, 0), "E"),
                MakeStraight(new Point2D(0, 100), new Point2D(0, 0), "N"),
                MakeStraight(new Point2D(0, -100), new Point2D(0, 0), "S"),
            };
            var ix = IntersectionDesigner.ComputeFromAlignments(
                als, new Point2D(0, 0), defaultRadius: 20, defaultHalfWidth: 5.0);

            foreach (var arc in ix.CornerArcs)
            {
                arc.Radius.Should().BeApproximately(20, 1e-6);
                arc.ArcLength.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public void Cross_CornerArc_IsTangentToBothCurbLines_PerpendicularOffset()
        {
            // v1.2 几何语义修正：
            //   CornerArc 位于两 Leg 的 <b>物理相邻</b> 一侧（Leg A 右 + Leg B 左）的外边线之间。
            //   外边线距 Alignment 轴 = HalfWidth；圆心距外边线 = R（切距不变）。
            //   圆心位于 "ApproachPoint 的 Alignment 同侧 + 外边线反侧" 方向，故
            //   <b>圆心距 Alignment 轴 = |R - HalfWidth|</b>（修复前错置在 R + HalfWidth 对角镜像）。
            //   圆心到两切点距 = R（永恒成立，见 TryBuildCornerArc 内部自校验）。
            var als = new List<Alignment>
            {
                MakeStraight(new Point2D(-100, 0), new Point2D(0, 0), "W"),
                MakeStraight(new Point2D(0, -100), new Point2D(0, 0), "S"),
                MakeStraight(new Point2D(100, 0), new Point2D(0, 0), "E"),
                MakeStraight(new Point2D(0, 100), new Point2D(0, 0), "N"),
            };
            double half = 5.0, R = 10.0;
            var ix = IntersectionDesigner.ComputeFromAlignments(
                als, new Point2D(0, 0), defaultRadius: R, defaultHalfWidth: half);

            foreach (var arc in ix.CornerArcs)
            {
                var legA = ix.Legs[arc.LegIndexA];
                var legB = ix.Legs[arc.LegIndexB];

                DistanceFromPointToInfiniteLine(legA.ApproachPoint, legA.InwardDirection, arc.Center)
                    .Should().BeApproximately(System.Math.Abs(R - half), 1e-3,
                        "圆心距 Leg A Alignment 轴 = |R - halfWidth|（= R 到外边线，外边线到 Alignment 轴 = halfWidth，同侧差）");

                DistanceFromPointToInfiniteLine(legB.ApproachPoint, legB.InwardDirection, arc.Center)
                    .Should().BeApproximately(System.Math.Abs(R - half), 1e-3);

                arc.Center.DistanceTo(arc.StartPoint).Should().BeApproximately(R, 1e-3);
                arc.Center.DistanceTo(arc.EndPoint).Should().BeApproximately(R, 1e-3);
            }
        }

        [Fact]
        public void TShape_ThreeLegs_ProducesThreeArcs()
        {
            var als = new List<Alignment>
            {
                MakeStraight(new Point2D(-100, 0), new Point2D(0, 0), "W"),
                MakeStraight(new Point2D(100, 0), new Point2D(0, 0), "E"),
                MakeStraight(new Point2D(0, -100), new Point2D(0, 0), "S"),
            };
            var ix = IntersectionDesigner.ComputeFromAlignments(
                als, new Point2D(0, 0), defaultRadius: 15, defaultHalfWidth: 5);

            ix.Legs.Should().HaveCount(3);
            ix.CornerArcs.Count.Should().BeGreaterOrEqualTo(2);
        }

        [Fact]
        public void AlignmentFromFar_EndpointClosestToAround_IsChosen()
        {
            var als = new List<Alignment>
            {
                MakeStraight(new Point2D(-100, 0), new Point2D(-1000, 0), "W_BackToOrigin"),
                MakeStraight(new Point2D(0, -100), new Point2D(0, 0), "S"),
            };

            var ix = IntersectionDesigner.ComputeFromAlignments(
                als, new Point2D(-100, 0), defaultRadius: 10, defaultHalfWidth: 4);

            ix.Legs.Should().HaveCount(2);
            ix.Legs[0].ApproachPoint.DistanceTo(new Point2D(-100, 0))
                .Should().BeLessThan(1e-3, "应选端点距 aroundPoint 更近者");
            ix.Legs[1].ApproachPoint.DistanceTo(new Point2D(0, 0))
                .Should().BeLessThan(1e-3);
        }

        [Fact]
        public void CornerArc_StartEnd_LieOnExpectedCurbLines()
        {
            var als = new List<Alignment>
            {
                MakeStraight(new Point2D(-100, 0), new Point2D(0, 0), "W"),
                MakeStraight(new Point2D(0, -100), new Point2D(0, 0), "S"),
            };
            double half = 5, R = 8;
            var ix = IntersectionDesigner.ComputeFromAlignments(
                als, new Point2D(0, 0), defaultRadius: R, defaultHalfWidth: half);

            ix.CornerArcs.Should().NotBeEmpty();
            foreach (var arc in ix.CornerArcs)
            {
                var legA = ix.Legs[arc.LegIndexA];
                var legB = ix.Legs[arc.LegIndexB];
                DistanceFromPointToInfiniteLine(legA.ApproachPoint, legA.InwardDirection, arc.StartPoint)
                    .Should().BeApproximately(half, 1e-3);
                DistanceFromPointToInfiniteLine(legB.ApproachPoint, legB.InwardDirection, arc.EndPoint)
                    .Should().BeApproximately(half, 1e-3);
            }
        }

        [Fact]
        public void ObliqueIntersection_60Deg_ArcStillValid()
        {
            double rad = 60 * Math.PI / 180;
            var endX = -100 * Math.Cos(rad);
            var endY = -100 * Math.Sin(rad);
            var als = new List<Alignment>
            {
                MakeStraight(new Point2D(-100, 0), new Point2D(0, 0), "W"),
                MakeStraight(new Point2D(endX, endY), new Point2D(0, 0), "SW60"),
            };
            var ix = IntersectionDesigner.ComputeFromAlignments(
                als, new Point2D(0, 0), defaultRadius: 12, defaultHalfWidth: 4);

            ix.Legs.Should().HaveCount(2);
            ix.CornerArcs.Should().NotBeEmpty();
            foreach (var arc in ix.CornerArcs)
            {
                arc.Radius.Should().BeApproximately(12, 1e-6);
                arc.ArcLength.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public void TryLineIntersection_ParallelLines_ReturnsFalse()
        {
            bool ok = IntersectionDesigner.TryLineIntersection(
                new Point2D(0, 0), new Vector2D(1, 0),
                new Point2D(0, 1), new Vector2D(1, 0),
                out _);
            ok.Should().BeFalse();
        }

        [Fact]
        public void TryLineIntersection_OrthogonalLines_GivesOrigin()
        {
            bool ok = IntersectionDesigner.TryLineIntersection(
                new Point2D(-5, 0), new Vector2D(1, 0),
                new Point2D(0, -3), new Vector2D(0, 1),
                out var p);
            ok.Should().BeTrue();
            p.IsEqualTo(new Point2D(0, 0), 1e-9).Should().BeTrue();
        }

        /// <summary>
        /// 点到经过 <paramref name="linePoint"/> 沿 <paramref name="lineDir"/> 的无限直线的垂直距离。
        /// </summary>
        private static double DistanceFromPointToInfiniteLine(Point2D linePoint, Vector2D lineDir, Point2D p)
        {
            if (!lineDir.TryNormalize(out var u, 1e-12)) return double.NaN;
            var v = linePoint.VectorTo(p);
            double proj = v.Dot(u);
            var foot = linePoint.Add(u * proj);
            return foot.DistanceTo(p);
        }
    }
}
