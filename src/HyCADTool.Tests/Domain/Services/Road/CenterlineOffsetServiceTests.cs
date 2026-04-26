using System;
using System.Collections.Generic;
using FluentAssertions;
using HyCADTool.Features.Road.PlanAlignment.Domain;
using HyCADTool.Shared.Geometry;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>
    /// T9 验收：<see cref="CenterlineOffsetService"/> 对离散 Polyline 的平行偏移。
    /// </summary>
    public class CenterlineOffsetServiceTests
    {
        private const double Tol = 1e-6;

        [Fact]
        public void Throws_OnNull() =>
            ((Action)(() => CenterlineOffsetService.Offset((IReadOnlyList<Point2D>)null, 1)))
                .Should().Throw<ArgumentNullException>();

        [Fact]
        public void Throws_WhenLessThanTwoVertices() =>
            ((Action)(() => CenterlineOffsetService.Offset(new[] { new Point2D(0, 0) }, 1)))
                .Should().Throw<ArgumentException>();

        [Fact]
        public void ZeroOffset_ReturnsCopyOfInput()
        {
            var src = new List<Point2D>
            {
                new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10),
            };
            var result = CenterlineOffsetService.Offset(src, 0);
            result.Should().HaveCount(3);
            for (int i = 0; i < 3; i++)
            {
                result[i].X.Should().Be(src[i].X);
                result[i].Y.Should().Be(src[i].Y);
            }
        }

        [Fact]
        public void StraightLine_LeftOffset_ShiftsByPositiveY()
        {
            // 沿 +X 走的一段直线，左侧（+Y）偏移 5。
            var src = new[] { new Point2D(0, 0), new Point2D(100, 0) };
            var result = CenterlineOffsetService.Offset(src, 5);
            result.Should().HaveCount(2);
            result[0].X.Should().BeApproximately(0, Tol);
            result[0].Y.Should().BeApproximately(5, Tol);
            result[1].X.Should().BeApproximately(100, Tol);
            result[1].Y.Should().BeApproximately(5, Tol);
        }

        [Fact]
        public void StraightLine_RightOffset_ShiftsByNegativeY()
        {
            var src = new[] { new Point2D(0, 0), new Point2D(100, 0) };
            var result = CenterlineOffsetService.Offset(src, -3);
            result[0].Y.Should().BeApproximately(-3, Tol);
            result[1].Y.Should().BeApproximately(-3, Tol);
        }

        [Fact]
        public void RightAngleCorner_LeftOffset_PreservesPerpendicularDistance()
        {
            // L 形：(0,0)→(100,0)→(100,100)，"左侧"90°外拐折点。
            // 左偏 5 后：直段偏到 y=5，再偏到 x=95；拐点交点为 (95, 5)？
            // 注意：这是"内拐"（内侧是左上角）。左侧法向：第一段 (0,1)、第二段 (-1,0)。
            // 偏移后两条段：y=5（第一段）、x=95（第二段）。交点 = (95, 5)。
            var src = new[]
            {
                new Point2D(0, 0),
                new Point2D(100, 0),
                new Point2D(100, 100),
            };
            var result = CenterlineOffsetService.Offset(src, 5);
            result.Should().HaveCount(3);
            result[0].X.Should().BeApproximately(0, Tol);
            result[0].Y.Should().BeApproximately(5, Tol);
            result[1].X.Should().BeApproximately(95, Tol, "90° 内拐折点交于 (95, 5)");
            result[1].Y.Should().BeApproximately(5, Tol);
            result[2].X.Should().BeApproximately(95, Tol);
            result[2].Y.Should().BeApproximately(100, Tol);
        }

        [Fact]
        public void RightAngleCorner_RightOffset_PushesOutward()
        {
            // 同 L 形，右偏 5 → 外拐折点 = (105, -5)。
            var src = new[]
            {
                new Point2D(0, 0),
                new Point2D(100, 0),
                new Point2D(100, 100),
            };
            var result = CenterlineOffsetService.Offset(src, -5);
            result[0].Y.Should().BeApproximately(-5, Tol);
            result[1].X.Should().BeApproximately(105, Tol);
            result[1].Y.Should().BeApproximately(-5, Tol);
            result[2].X.Should().BeApproximately(105, Tol);
        }

        [Fact]
        public void CollinearSegments_DoNotBreak()
        {
            // 三个顶点共线：偏移结果仍在平行线上
            var src = new[]
            {
                new Point2D(0, 0), new Point2D(50, 0), new Point2D(100, 0),
            };
            var result = CenterlineOffsetService.Offset(src, 5);
            result.Should().HaveCount(3);
            foreach (var p in result) p.Y.Should().BeApproximately(5, Tol);
        }

        [Fact]
        public void DuplicateVertices_AreFiltered()
        {
            // 两个重合顶点：去重后仍是一根直线
            var src = new[]
            {
                new Point2D(0, 0),
                new Point2D(100, 0),
                new Point2D(100, 0),
                new Point2D(200, 0),
            };
            var result = CenterlineOffsetService.Offset(src, 5);
            result.Should().HaveCount(3, "重合顶点被过滤，剩 3 个");
            foreach (var p in result) p.Y.Should().BeApproximately(5, Tol);
        }

        [Fact]
        public void Polyline3D_Overload_PreservesZ()
        {
            var src = new Polyline3D(new[]
            {
                new Point3D(0, 0, 1.5),
                new Point3D(100, 0, 2.5),
            });
            var result = CenterlineOffsetService.Offset(src, 5);
            result.VertexCount.Should().Be(2);
            result.GetPointAt(0).Z.Should().BeApproximately(1.5, Tol);
            result.GetPointAt(1).Z.Should().BeApproximately(2.5, Tol);
        }

        // ---------- A3 新增：ComputeMinCurvatureRadius + 偏移自检 ----------

        [Fact]
        public void ComputeMinCurvatureRadius_AllStraight_ReturnsInfinity()
        {
            var poly = new Polyline3D(new[]
            {
                new Point3D(0, 0, 0),
                new Point3D(100, 0, 0),
                new Point3D(200, 0, 0),
            });
            double r = CenterlineOffsetService.ComputeMinCurvatureRadius(poly);
            double.IsInfinity(r).Should().BeTrue();
        }

        [Fact]
        public void ComputeMinCurvatureRadius_SemicircleBulge_RecoversRadius()
        {
            // 半圆（θ=π → bulge = tan(π/4) = 1，弦长 2R）——R=50，chord=100
            var poly = new Polyline3D(
                new List<Point3D>
                {
                    new Point3D(0, 0, 0),
                    new Point3D(100, 0, 0),
                },
                isClosed: false,
                bulges: new List<double> { 1.0 });
            double r = CenterlineOffsetService.ComputeMinCurvatureRadius(poly);
            r.Should().BeApproximately(50, 1e-6);
        }

        [Fact]
        public void ComputeMinCurvatureRadius_MultipleArcs_ReturnsMin()
        {
            // 段 0：bulge=1（半圆 R=50，chord=100）
            // 段 1：bulge=tan(π/8)（四分之一圆，R=chord/sqrt(2)=70.711，chord=100）
            double b1 = 1.0;              // 半圆 → R=50
            double b2 = Math.Tan(Math.PI / 8.0); // 1/4 圆
            var poly = new Polyline3D(
                new List<Point3D>
                {
                    new Point3D(0, 0, 0),
                    new Point3D(100, 0, 0),
                    new Point3D(200, 0, 0),
                },
                isClosed: false,
                bulges: new List<double> { b1, b2 });

            double rMin = CenterlineOffsetService.ComputeMinCurvatureRadius(poly);
            // 最小应为半圆段的 50
            rMin.Should().BeApproximately(50, 1e-4);
        }

        [Fact]
        public void Offset_Throws_WhenAbsOffsetExceedsMinRadius()
        {
            // 半圆 R=50；offset=50 恰好触底
            var poly = new Polyline3D(
                new List<Point3D>
                {
                    new Point3D(0, 0, 0),
                    new Point3D(100, 0, 0),
                },
                isClosed: false,
                bulges: new List<double> { 1.0 });

            ((Action)(() => CenterlineOffsetService.Offset(poly, 50)))
                .Should().Throw<InvalidOperationException>()
                .WithMessage("*最小曲率半径*");

            // offset=60 必然抛
            ((Action)(() => CenterlineOffsetService.Offset(poly, 60)))
                .Should().Throw<InvalidOperationException>();

            // offset=10 OK
            var ok = CenterlineOffsetService.Offset(poly, 10);
            ok.VertexCount.Should().BeGreaterOrEqualTo(2);
        }

        [Fact]
        public void Offset_OnStraightLine_NoCurvatureCheck_Trips()
        {
            // 全直线时 rMin = +inf，应不触发自检（哪怕 offset 很大）
            var poly = new Polyline3D(new[]
            {
                new Point3D(0, 0, 0),
                new Point3D(1000, 0, 0),
            });
            var result = CenterlineOffsetService.Offset(poly, 999);
            result.VertexCount.Should().Be(2);
            result.GetPointAt(0).Y.Should().BeApproximately(999, Tol);
        }
    }
}
