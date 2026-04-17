using System;
using FluentAssertions;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.Services.Road
{
    public class AlignmentPiDesignerTests
    {
        private const double Eps = 1e-6;
        // tan(22.5°) = bulge of a 90° CCW arc（AutoCAD bulge = tan(θ/4)）
        private const double Tan22_5 = 0.41421356237309515;

        [Fact]
        public void Throws_WhenPiPointsNull()
        {
            Action act = () => AlignmentPiDesigner.Build(null, 30);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Throws_WhenLessThanTwoPoints()
        {
            Action act = () => AlignmentPiDesigner.Build(new[] { new Point2D(0, 0) }, 30);
            act.Should().Throw<ArgumentException>()
                .WithMessage("*至少需要 2 个 PI 点*");
        }

        [Fact]
        public void Throws_WhenAdjacentPointsCoincide()
        {
            var pts = new[]
            {
                new Point2D(0, 0),
                new Point2D(0, 0),
                new Point2D(100, 0),
            };
            Action act = () => AlignmentPiDesigner.Build(pts, 30);
            act.Should().Throw<ArgumentException>()
                .WithMessage("*重合*");
        }

        [Fact]
        public void TwoPoints_ProducesStraightLineWithTwoVertices()
        {
            var pts = new[] { new Point2D(0, 0), new Point2D(100, 0) };

            var r = AlignmentPiDesigner.Build(pts, 30);

            r.Polyline.VertexCount.Should().Be(2);
            r.Polyline.HasArcs.Should().BeFalse();
            r.Polyline.GetPointAt(0).X.Should().Be(0);
            r.Polyline.GetPointAt(1).X.Should().Be(100);
            r.CurvedPiCount.Should().Be(0);
            r.StraightPiCount.Should().Be(0);
            r.SkippedCount.Should().Be(0);
            r.Warnings.Should().BeEmpty();
        }

        [Fact]
        public void LeftTurn90_ProducesFourVerticesAndPositiveBulge()
        {
            // P0=(0,0) → P1=(100,0) → P2=(100,100)，逆时针 90°，R=20
            var pts = new[]
            {
                new Point2D(0, 0),
                new Point2D(100, 0),
                new Point2D(100, 100),
            };

            var r = AlignmentPiDesigner.Build(pts, 20);

            r.Polyline.VertexCount.Should().Be(4);
            r.CurvedPiCount.Should().Be(1);
            r.SkippedCount.Should().Be(0);
            r.Warnings.Should().BeEmpty();

            // 顶点：(0,0) → TC=(80,0) → CT=(100,20) → (100,100)
            r.Polyline.GetPointAt(0).X.Should().BeApproximately(0, Eps);
            r.Polyline.GetPointAt(1).X.Should().BeApproximately(80, Eps);
            r.Polyline.GetPointAt(1).Y.Should().BeApproximately(0, Eps);
            r.Polyline.GetPointAt(2).X.Should().BeApproximately(100, Eps);
            r.Polyline.GetPointAt(2).Y.Should().BeApproximately(20, Eps);
            r.Polyline.GetPointAt(3).Y.Should().BeApproximately(100, Eps);

            // 左转：bulge > 0，数值等于 tan(22.5°)
            r.Polyline.GetBulgeAt(0).Should().Be(0);
            r.Polyline.GetBulgeAt(1).Should().BeApproximately(Tan22_5, Eps);
            r.Polyline.GetBulgeAt(2).Should().Be(0);
            r.Polyline.GetBulgeAt(3).Should().Be(0);
        }

        [Fact]
        public void RightTurn90_ProducesNegativeBulge()
        {
            // P0=(0,0) → P1=(100,0) → P2=(100,-100)，顺时针 90°
            var pts = new[]
            {
                new Point2D(0, 0),
                new Point2D(100, 0),
                new Point2D(100, -100),
            };

            var r = AlignmentPiDesigner.Build(pts, 20);

            r.Polyline.VertexCount.Should().Be(4);
            r.CurvedPiCount.Should().Be(1);

            // 顶点：(0,0) → TC=(80,0) → CT=(100,-20) → (100,-100)
            r.Polyline.GetPointAt(1).X.Should().BeApproximately(80, Eps);
            r.Polyline.GetPointAt(2).Y.Should().BeApproximately(-20, Eps);

            // 右转：bulge < 0
            r.Polyline.GetBulgeAt(1).Should().BeApproximately(-Tan22_5, Eps);
        }

        [Fact]
        public void ZeroRadius_ProducesPurePolylineWithAllPiKept()
        {
            var pts = new[]
            {
                new Point2D(0, 0),
                new Point2D(100, 0),
                new Point2D(100, 100),
                new Point2D(0, 100),
            };

            var r = AlignmentPiDesigner.Build(pts, 0);

            r.Polyline.VertexCount.Should().Be(4);
            r.Polyline.HasArcs.Should().BeFalse();
            r.CurvedPiCount.Should().Be(0);
            r.StraightPiCount.Should().Be(2); // 两个内部 PI 都保留为折线
            r.SkippedCount.Should().Be(0);
        }

        [Fact]
        public void CollinearPoint_PreservedAsStraightVertex()
        {
            // P0 → P1 → P2 共线：P1 是"冗余 PI"，保留顶点但不加圆角
            var pts = new[]
            {
                new Point2D(0, 0),
                new Point2D(50, 0),
                new Point2D(100, 0),
            };

            var r = AlignmentPiDesigner.Build(pts, 30);

            r.Polyline.VertexCount.Should().Be(3);
            r.Polyline.HasArcs.Should().BeFalse();
            r.CurvedPiCount.Should().Be(0);
            r.StraightPiCount.Should().Be(1);
        }

        [Fact]
        public void RadiusOverflow_SkipsWithWarning()
        {
            // 段长仅 5m，但 R=100，T=100 远大于段长 → 跳过圆角，保留 PI 顶点
            var pts = new[]
            {
                new Point2D(0, 0),
                new Point2D(5, 0),
                new Point2D(5, 5),
            };

            var r = AlignmentPiDesigner.Build(pts, 100);

            r.Polyline.VertexCount.Should().Be(3);
            r.Polyline.HasArcs.Should().BeFalse();
            r.CurvedPiCount.Should().Be(0);
            r.SkippedCount.Should().Be(1);
            r.Warnings.Should().HaveCount(1);
            r.Warnings[0].Should().Contain("切线长");
            r.Warnings[0].Should().Contain("建议半径");
        }

        [Fact]
        public void FivePointPath_WithTwoTurns_ProducesCorrectVertexCount()
        {
            // P0=(0,0) → P1=(100,0) → P2=(100,100) → P3=(200,100) → P4=(200,200)
            // 三个内部 PI：P1 左转 90°、P2 右转 90°、P3 左转 90°
            var pts = new[]
            {
                new Point2D(0, 0),
                new Point2D(100, 0),
                new Point2D(100, 100),
                new Point2D(200, 100),
                new Point2D(200, 200),
            };

            var r = AlignmentPiDesigner.Build(pts, 20);

            // 顶点数 = 2（首尾）+ 3 * 2（每个内部 PI 两个顶点）= 8
            r.Polyline.VertexCount.Should().Be(8);
            r.CurvedPiCount.Should().Be(3);
            r.SkippedCount.Should().Be(0);
            r.Warnings.Should().BeEmpty();

            // bulge 模式：[0, +, 0, -, 0, +, 0, 0]
            r.Polyline.GetBulgeAt(1).Should().BeApproximately(Tan22_5, Eps);
            r.Polyline.GetBulgeAt(3).Should().BeApproximately(-Tan22_5, Eps);
            r.Polyline.GetBulgeAt(5).Should().BeApproximately(Tan22_5, Eps);

            // 其他位置 bulge = 0
            r.Polyline.GetBulgeAt(0).Should().Be(0);
            r.Polyline.GetBulgeAt(2).Should().Be(0);
            r.Polyline.GetBulgeAt(4).Should().Be(0);
            r.Polyline.GetBulgeAt(6).Should().Be(0);
            r.Polyline.GetBulgeAt(7).Should().Be(0);
        }

        [Fact]
        public void Elevation_AppliedToAllVertices()
        {
            var pts = new[]
            {
                new Point2D(0, 0),
                new Point2D(100, 0),
                new Point2D(100, 100),
            };

            var r = AlignmentPiDesigner.Build(pts, 20, elevation: 12.5);

            for (int i = 0; i < r.Polyline.VertexCount; i++)
            {
                r.Polyline.GetPointAt(i).Z.Should().BeApproximately(12.5, Eps);
            }
        }

        [Fact]
        public void SmallTurnBelowThreshold_KeptAsStraight()
        {
            // 内部 PI 偏转 0.1°，远小于默认 0.5° 阈值 → 不加圆角
            var pts = new[]
            {
                new Point2D(0, 0),
                new Point2D(1000, 0),
                new Point2D(2000, 1.745), // tan(0.1°) ≈ 0.001745
            };

            var r = AlignmentPiDesigner.Build(pts, 30);

            r.Polyline.VertexCount.Should().Be(3);
            r.Polyline.HasArcs.Should().BeFalse();
            r.CurvedPiCount.Should().Be(0);
            r.StraightPiCount.Should().Be(1);
        }
    }
}
