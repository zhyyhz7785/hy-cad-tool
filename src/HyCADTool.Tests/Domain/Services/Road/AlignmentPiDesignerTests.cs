using System;
using System.Linq;
using FluentAssertions;
using HyCADTool.Features.Road.PlanAlignment.Domain;
using HyCADTool.Shared.Geometry;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    public class AlignmentPiDesignerTests
    {
        private const double Eps = 1e-6;
        // tan(22.5°) = bulge of a 90° CCW arc（AutoCAD bulge = tan(θ/4)）
        private const double Tan22_5 = 0.41421356237309515;

        [Fact]
        public void Throws_WhenPiPointsNull()
        {
            Action act = () => AlignmentPiDesigner.Build((System.Collections.Generic.IReadOnlyList<Point2D>)null, 30);
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
            r.SpiraledPiCount.Should().Be(0);
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

        // ========================================================================================
        // Step A 新增：逐 PI 独立半径
        // ========================================================================================

        [Fact]
        public void PerPiRadius_DifferentRadiusEachPi_ProducesCorrespondingArcs()
        {
            // 三个内部 PI：第一个 R=10、第二个 R=0（折线）、第三个 R=20。
            var elements = new[]
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(100, 0), radius: 10),
                new PiElement(new Point2D(100, 100), radius: 0), // 折线
                new PiElement(new Point2D(200, 100), radius: 20),
                new PiElement(new Point2D(200, 200)),
            };

            var r = AlignmentPiDesigner.Build(elements);

            r.CurvedPiCount.Should().Be(2);
            r.StraightPiCount.Should().Be(1);
            // 顶点：2 首尾 + 2*2 圆角 + 1 折线 PI = 7
            r.Polyline.VertexCount.Should().Be(7);
            r.PerPi.Should().HaveCount(3);
            r.PerPi[0].Status.Should().Be(PiDiagnosticStatus.Curved);
            r.PerPi[1].Status.Should().Be(PiDiagnosticStatus.Straight);
            r.PerPi[2].Status.Should().Be(PiDiagnosticStatus.Curved);
        }

        [Fact]
        public void PerPiRadius_FallbackRadius_AppliedWhenPiRadiusIsZero()
        {
            var elements = new[]
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(100, 0)), // R=0
                new PiElement(new Point2D(100, 100)),
            };

            var r = AlignmentPiDesigner.Build(
                elements,
                new PiDesignOptions { FallbackRadius = 20 });

            r.CurvedPiCount.Should().Be(1);
            r.PerPi[0].Radius.Should().BeApproximately(20, Eps);
        }

        // ========================================================================================
        // Step B 新增：缓和曲线
        // ========================================================================================

        [Fact]
        public void Spiral_ZeroLength_DegradesToCircularArc_AndProducesIdenticalShape()
        {
            // 不传 Ls 时，结果应与统一 R 完全相同（验证向后兼容）
            var pts = new[]
            {
                new Point2D(0, 0),
                new Point2D(100, 0),
                new Point2D(100, 100),
            };
            var refResult = AlignmentPiDesigner.Build(pts, 20);

            var elements = new[]
            {
                new PiElement(pts[0]),
                new PiElement(pts[1], radius: 20, spiralIn: 0, spiralOut: 0),
                new PiElement(pts[2]),
            };
            var newResult = AlignmentPiDesigner.Build(elements);

            newResult.Polyline.VertexCount.Should().Be(refResult.Polyline.VertexCount);
            for (int i = 0; i < refResult.Polyline.VertexCount; i++)
            {
                newResult.Polyline.GetPointAt(i).X.Should().BeApproximately(refResult.Polyline.GetPointAt(i).X, 1e-9);
                newResult.Polyline.GetPointAt(i).Y.Should().BeApproximately(refResult.Polyline.GetPointAt(i).Y, 1e-9);
                newResult.Polyline.GetBulgeAt(i).Should().BeApproximately(refResult.Polyline.GetBulgeAt(i), 1e-9);
            }
        }

        [Fact]
        public void Spiral_BothSidesNonZero_ProducesSpiraledStatusAndExtraVertices()
        {
            // 段长 100，R=20，Ls_in = Ls_out = 5（保证容纳）
            var elements = new[]
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(100, 0), radius: 20, spiralIn: 5, spiralOut: 5),
                new PiElement(new Point2D(100, 100)),
            };

            var r = AlignmentPiDesigner.Build(elements);

            r.SpiraledPiCount.Should().Be(1);
            r.CurvedPiCount.Should().Be(0);
            r.SkippedCount.Should().Be(0);
            // 顶点数 = 首 + TS + (steps-1) 入侧中间点 + SC + (steps-1) 出侧中间点 + ST + 末
            // 默认 SpiralSteps=12 → 顶点 = 1 + 1 + 11 + 1 + 11 + 1 + 1 = 27
            r.Polyline.VertexCount.Should().Be(27);
            r.Polyline.HasArcs.Should().BeTrue();
            r.PerPi[0].Status.Should().Be(PiDiagnosticStatus.Spiraled);
        }

        [Fact]
        public void Spiral_OverlongLs_DegradesToCircularArc_WithWarning()
        {
            // 段长 10，R=5，Ls=20（超长）→ 应降级为纯圆曲线
            var elements = new[]
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(10, 0), radius: 5, spiralIn: 20, spiralOut: 20),
                new PiElement(new Point2D(10, 10)),
            };

            var r = AlignmentPiDesigner.Build(elements);

            r.CurvedPiCount.Should().Be(1);
            r.SpiraledPiCount.Should().Be(0);
            r.SkippedCount.Should().Be(0);
            r.Warnings.Should().NotBeEmpty();
            r.Warnings[0].Should().Contain("缓和曲线超长");
        }

        // ========================================================================================
        // Validation
        // ========================================================================================

        [Fact]
        public void AlignmentValidate_DetectsTooShortAndCoincidentVertex()
        {
            var alignment = new HyCADTool.Domain.Models.Road.Alignment
            {
                Centerline = new Polyline3D(isClosed: false),
            };
            alignment.Centerline.AddVertex(new Point3D(0, 0, 0));

            var v = alignment.Validate();
            v.Ok.Should().BeFalse();
            v.Errors.Should().Contain(s => s.Contains("顶点不足"));
        }

        [Fact]
        public void AlignmentValidate_OkForNormalAlignment()
        {
            var pts = new[]
            {
                new Point2D(0, 0),
                new Point2D(100, 0),
                new Point2D(100, 100),
            };
            var r = AlignmentPiDesigner.Build(pts, 20);
            var alignment = new HyCADTool.Domain.Models.Road.Alignment
            {
                Centerline = r.Polyline,
            };

            var v = alignment.Validate();
            v.Ok.Should().BeTrue();
            v.Errors.Should().BeEmpty();
        }
    }
}
