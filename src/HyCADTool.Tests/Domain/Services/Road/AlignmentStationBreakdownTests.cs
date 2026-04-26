using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using HyCADTool.Features.Road.PlanAlignment.Domain;
using HyCADTool.Shared.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>
    /// T0 验收：桩号分解服务。
    ///
    /// 用「可以手算」的场景验证：
    /// - 两点直线
    /// - 3 PI 90° 左/右转 + 纯圆曲线
    /// - 3 PI 90° + 左右对称缓和曲线
    /// - 5 PI 多转角 + 桩号链单调递增
    /// - ZeroRadius / Collinear / RadiusOverflow 三类「Straight」分支保留 PI 点
    /// - Alignment.StartStation 偏移
    ///
    /// 所有桩号 / 长度 / 坐标验证误差 &lt; 1e-3 m（工程精度阈值）。
    /// </summary>
    public class AlignmentStationBreakdownTests
    {
        private const double Tol = 1e-3;

        [Fact]
        public void Throws_WhenElementsNull()
        {
            Action act = () => AlignmentStationBreakdown.Build(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Throws_WhenLessThanTwoElements()
        {
            var pts = new[] { new PiElement(new Point2D(0, 0)) };
            Action act = () => AlignmentStationBreakdown.Build(pts);
            act.Should().Throw<ArgumentException>().WithMessage("*至少需要 2 个*");
        }

        [Fact]
        public void TwoPoints_StraightLine_OneSegmentAndTwoPoints()
        {
            var els = new[]
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(100, 0)),
            };

            var b = AlignmentStationBreakdown.Build(els);

            b.Segments.Should().HaveCount(1);
            b.Segments[0].Kind.Should().Be(SegmentKind.Line);
            b.Segments[0].LengthM.Should().BeApproximately(100, Tol);
            b.Segments[0].StationStartM.Should().BeApproximately(0, Tol);
            b.Segments[0].StationEndM.Should().BeApproximately(100, Tol);
            b.TotalLengthM.Should().BeApproximately(100, Tol);

            b.GeometryPoints.Should().HaveCount(2);
            b.GeometryPoints[0].Kind.Should().Be(GeometryPointKind.BP);
            b.GeometryPoints[0].StationM.Should().BeApproximately(0, Tol);
            b.GeometryPoints[1].Kind.Should().Be(GeometryPointKind.EP);
            b.GeometryPoints[1].StationM.Should().BeApproximately(100, Tol);
        }

        [Fact]
        public void StartStationOffset_PropagatesToAllStations()
        {
            var els = new[]
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(200, 0)),
            };

            var b = AlignmentStationBreakdown.Build(els, startStation: 1000);

            b.StartStationM.Should().BeApproximately(1000, Tol);
            b.EndStationM.Should().BeApproximately(1200, Tol);
            b.GeometryPoints[0].StationM.Should().BeApproximately(1000, Tol);
            b.GeometryPoints[1].StationM.Should().BeApproximately(1200, Tol);
            b.Segments[0].StationStartM.Should().BeApproximately(1000, Tol);
            b.Segments[0].StationEndM.Should().BeApproximately(1200, Tol);
        }

        [Fact]
        public void LeftTurn90_PureArc_SegmentsAndGeometryPoints()
        {
            // (0,0) → (100,0) → (100,100)；左转 90°，R=20
            // TS=(80,0)、ST=(100,20)
            // Line 80 + Arc R·(π/2)=31.4159 + Line 80 = 191.4159
            var els = new[]
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(100, 0), radius: 20),
                new PiElement(new Point2D(100, 100)),
            };

            var b = AlignmentStationBreakdown.Build(els);

            b.Segments.Should().HaveCount(3);
            b.Segments[0].Kind.Should().Be(SegmentKind.Line);
            b.Segments[0].LengthM.Should().BeApproximately(80, Tol);
            b.Segments[1].Kind.Should().Be(SegmentKind.Arc);
            b.Segments[1].LengthM.Should().BeApproximately(20 * Math.PI / 2, Tol);
            b.Segments[1].Radius.Should().BeApproximately(20, Tol);
            b.Segments[1].StartPoint.X.Should().BeApproximately(80, Tol);
            b.Segments[1].StartPoint.Y.Should().BeApproximately(0, Tol);
            b.Segments[1].EndPoint.X.Should().BeApproximately(100, Tol);
            b.Segments[1].EndPoint.Y.Should().BeApproximately(20, Tol);
            b.Segments[2].Kind.Should().Be(SegmentKind.Line);
            b.Segments[2].LengthM.Should().BeApproximately(80, Tol);

            b.TotalLengthM.Should().BeApproximately(160 + 20 * Math.PI / 2, Tol);

            // 几何点：BP / BC / EC / EP
            b.GeometryPoints.Select(p => p.Kind).Should().Equal(
                GeometryPointKind.BP,
                GeometryPointKind.BC,
                GeometryPointKind.EC,
                GeometryPointKind.EP);
            b.GeometryPoints[1].StationM.Should().BeApproximately(80, Tol);
            b.GeometryPoints[2].StationM.Should().BeApproximately(80 + 20 * Math.PI / 2, Tol);
            b.GeometryPoints[3].StationM.Should().BeApproximately(160 + 20 * Math.PI / 2, Tol);

            // Bearing：入向 0 rad（+X），出向 π/2（+Y）
            b.Segments[1].StartBearingRad.Should().BeApproximately(0, Tol);
            b.Segments[1].EndBearingRad.Should().BeApproximately(Math.PI / 2, Tol);
        }

        [Fact]
        public void RightTurn90_PureArc_HasNegativeTurnButPositiveArcLength()
        {
            // (0,0) → (100,0) → (100,-100)；右转 90°
            var els = new[]
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(100, 0), radius: 20),
                new PiElement(new Point2D(100, -100)),
            };

            var b = AlignmentStationBreakdown.Build(els);

            b.Segments.Should().HaveCount(3);
            b.Segments[1].Kind.Should().Be(SegmentKind.Arc);
            b.Segments[1].LengthM.Should().BeApproximately(20 * Math.PI / 2, Tol);
            b.Segments[1].EndPoint.X.Should().BeApproximately(100, Tol);
            b.Segments[1].EndPoint.Y.Should().BeApproximately(-20, Tol);
            // 右转：出向 -π/2
            b.Segments[1].EndBearingRad.Should().BeApproximately(-Math.PI / 2, Tol);
        }

        [Fact]
        public void SymmetricSpiral_ProducesFiveSegmentsAndSixGeometryPoints()
        {
            // 同上 90° 左转，Ls_in = Ls_out = 5、R = 20
            // pIn = 25/(480) = 0.0521
            // qIn = 2.5 - 125/(96000) ≈ 2.4987
            // tInTotal = (20 + 0.0521) * 1 + 2.4987 = 22.5508
            // TS=(100 - 22.5508, 0)=(77.4492, 0)
            // ST=(100, 22.5508)
            // Arc 段角度 = π/2 - (5+5)/(40) = π/2 - 0.25 = 1.3208 rad
            // Arc 长 = 20 * 1.3208 = 26.4159
            // Total = 77.4492 + 5 + 26.4159 + 5 + 77.4492 = 191.3143
            var els = new[]
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(100, 0), radius: 20, spiralIn: 5, spiralOut: 5),
                new PiElement(new Point2D(100, 100)),
            };

            var b = AlignmentStationBreakdown.Build(els);

            b.Segments.Should().HaveCount(5);
            b.Segments.Select(s => s.Kind).Should().Equal(
                SegmentKind.Line,
                SegmentKind.Spiral,
                SegmentKind.Arc,
                SegmentKind.Spiral,
                SegmentKind.Line);

            b.Segments[0].LengthM.Should().BeApproximately(77.4492, 1e-2);
            b.Segments[1].LengthM.Should().BeApproximately(5, Tol);
            b.Segments[1].SpiralLs.Should().BeApproximately(5, Tol);
            b.Segments[1].SpiralA.Should().BeApproximately(Math.Sqrt(20 * 5), Tol);
            b.Segments[2].LengthM.Should().BeApproximately(20 * (Math.PI / 2 - 0.25), 1e-2);
            b.Segments[2].Radius.Should().BeApproximately(20, Tol);
            b.Segments[3].LengthM.Should().BeApproximately(5, Tol);
            b.Segments[4].LengthM.Should().BeApproximately(77.4492, 1e-2);

            // TS @ (77.449, 0)
            b.Segments[1].StartPoint.X.Should().BeApproximately(77.4492, 1e-2);
            b.Segments[1].StartPoint.Y.Should().BeApproximately(0, Tol);
            // ST @ (100, 22.551)
            b.Segments[3].EndPoint.X.Should().BeApproximately(100, Tol);
            b.Segments[3].EndPoint.Y.Should().BeApproximately(22.5508, 1e-2);

            // 桩号链单调 + 连续
            for (int i = 1; i < b.Segments.Count; i++)
            {
                b.Segments[i].StationStartM.Should().BeApproximately(b.Segments[i - 1].StationEndM, 1e-6,
                    $"段[{i}] 起桩号应等于段[{i - 1}] 止桩号");
            }

            // 几何点：BP / TS / SC / CS / ST / EP
            b.GeometryPoints.Select(p => p.Kind).Should().Equal(
                GeometryPointKind.BP,
                GeometryPointKind.TS,
                GeometryPointKind.SC,
                GeometryPointKind.CS,
                GeometryPointKind.ST,
                GeometryPointKind.EP);

            b.TotalLengthM.Should().BeApproximately(191.3143, 1e-2);
        }

        [Fact]
        public void ZeroRadius_CollapsesToAllLineSegments_PiKeptAsGeometryPoint()
        {
            var els = new[]
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(100, 0), radius: 0),
                new PiElement(new Point2D(100, 100)),
            };

            var b = AlignmentStationBreakdown.Build(els);

            b.Segments.Should().HaveCount(2);
            b.Segments.Should().OnlyContain(s => s.Kind == SegmentKind.Line);
            b.GeometryPoints.Select(p => p.Kind).Should().Equal(
                GeometryPointKind.BP,
                GeometryPointKind.PI,
                GeometryPointKind.EP);
            b.TotalLengthM.Should().BeApproximately(200, Tol);
        }

        [Fact]
        public void CollinearPoint_PreservedAsPiGeometryPoint()
        {
            var els = new[]
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(50, 0), radius: 30),
                new PiElement(new Point2D(100, 0)),
            };

            var b = AlignmentStationBreakdown.Build(els);

            // 共线被降为 Straight；共产出 2 条直线段（首段 BP→PI，末段 PI→EP）
            b.Segments.Should().HaveCount(2);
            b.Segments.Should().OnlyContain(s => s.Kind == SegmentKind.Line);
            b.GeometryPoints[1].Kind.Should().Be(GeometryPointKind.PI);
        }

        [Fact]
        public void RadiusOverflow_FallsBackToPiStraight()
        {
            // 段长 5，R=100 切线长过大 → Skipped（按 Straight 处理保 PI）
            var els = new[]
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(5, 0), radius: 100),
                new PiElement(new Point2D(5, 5)),
            };

            var b = AlignmentStationBreakdown.Build(els);

            b.Segments.Should().OnlyContain(s => s.Kind == SegmentKind.Line);
            b.GeometryPoints.Select(p => p.Kind).Should().Equal(
                GeometryPointKind.BP,
                GeometryPointKind.PI,
                GeometryPointKind.EP);
        }

        [Fact]
        public void MultiPiChain_StationsAreMonotonicAndContinuous()
        {
            // 3 个转角 90°，R=20，无缓和
            var els = new[]
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(100, 0), radius: 20),
                new PiElement(new Point2D(100, 100), radius: 20),
                new PiElement(new Point2D(200, 100), radius: 20),
                new PiElement(new Point2D(200, 200)),
            };

            var b = AlignmentStationBreakdown.Build(els);

            // 段：L - A - L - A - L - A - L = 7
            b.Segments.Should().HaveCount(7);
            b.Segments.Where(s => s.Kind == SegmentKind.Arc).Should().HaveCount(3);

            // 桩号链单调 + 连续
            for (int i = 1; i < b.Segments.Count; i++)
            {
                b.Segments[i].StationStartM.Should().BeGreaterThan(b.Segments[i - 1].StationStartM);
                b.Segments[i].StationStartM.Should().BeApproximately(b.Segments[i - 1].StationEndM, 1e-6);
            }

            // 几何点链单调 + 与段数一致（段数 + 1 = 点数）
            for (int i = 1; i < b.GeometryPoints.Count; i++)
            {
                b.GeometryPoints[i].StationM.Should().BeGreaterOrEqualTo(b.GeometryPoints[i - 1].StationM);
            }

            // 末点 = 总长
            b.GeometryPoints.Last().StationM.Should().BeApproximately(b.EndStationM, Tol);
        }

        [Fact]
        public void FormatStation_MatchesCivil3DConvention()
        {
            AlignmentStationBreakdown.FormatStation(0).Should().Be("K0+000.000");
            AlignmentStationBreakdown.FormatStation(20).Should().Be("K0+020.000");
            AlignmentStationBreakdown.FormatStation(1234.567).Should().Be("K1+234.567");
            AlignmentStationBreakdown.FormatStation(-10).Should().Be("-K0+010.000");
        }

        [Fact]
        public void Build_TotalLength_WithoutSpiral_AgreesWithPiDesignerCenterline()
        {
            // 无缓和曲线时，T0 理论值 与 Designer Polyline3D.GetPlanarLength()（bulge 精确弧长）应几乎相等。
            // 带缓和曲线时不比较：Designer 把 TS→SC 按"弦+小 bulge"离散，总长会系统性短于 Ls，
            // 这是 Designer 的近似表示差异，不是 T0 的计算误差。
            var els = new[]
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(300, 0), radius: 60),
                new PiElement(new Point2D(300, 200), radius: 80),
                new PiElement(new Point2D(600, 200)),
            };

            var b = AlignmentStationBreakdown.Build(els);
            var piResult = AlignmentPiDesigner.Build(els);
            double centerlineLen = piResult.Polyline.GetPlanarLength();

            Math.Abs(b.TotalLengthM - centerlineLen).Should().BeLessThan(1e-3,
                $"T0 理论长度 {b.TotalLengthM:F6} 应与 Designer Centerline 长度 {centerlineLen:F6} 精确一致");
        }

        [Fact]
        public void Build_GeometryPointsCoordinates_MatchDesignerVertices()
        {
            // 关键几何点坐标（TS / ST / BC / EC）应与 Designer 落图顶点完全一致。
            // 通过这个测试锁定：T0 与 Designer 用同一套公式、同一套分支判定。
            var els = new[]
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(100, 0), radius: 20, spiralIn: 5, spiralOut: 5),
                new PiElement(new Point2D(100, 100)),
            };

            var b = AlignmentStationBreakdown.Build(els);
            var piResult = AlignmentPiDesigner.Build(els);

            // Designer 带缓和的顶点序列：BP / TS / {缓和离散点}* / SC / CS / {缓和离散点}* / ST / EP
            // 取首尾（BP / EP）与 TS / ST（缓和段的两端）验证
            var designerBP = piResult.Polyline.GetPointAt(0);
            var designerEP = piResult.Polyline.GetPointAt(piResult.Polyline.VertexCount - 1);

            var t0BP = b.GeometryPoints[0];
            var t0EP = b.GeometryPoints[b.GeometryPoints.Count - 1];

            t0BP.Point.X.Should().BeApproximately(designerBP.X, Tol);
            t0BP.Point.Y.Should().BeApproximately(designerBP.Y, Tol);
            t0EP.Point.X.Should().BeApproximately(designerEP.X, Tol);
            t0EP.Point.Y.Should().BeApproximately(designerEP.Y, Tol);

            // TS = Designer Polyline 的第二个顶点（带缓和时）
            var ts = b.GeometryPoints.First(p => p.Kind == GeometryPointKind.TS);
            var designerTs = piResult.Polyline.GetPointAt(1);
            ts.Point.X.Should().BeApproximately(designerTs.X, Tol);
            ts.Point.Y.Should().BeApproximately(designerTs.Y, Tol);
        }

        // ---------- T7：带桩号方程的 Build 重载 ----------

        [Fact]
        public void BuildWithEquations_NullOrEmpty_EquivalentToPlainBuild()
        {
            var els = new[]
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(100, 0)),
            };

            var plain = AlignmentStationBreakdown.Build(els, startStation: 1000);
            var withNull = AlignmentStationBreakdown.Build(els, 1000, null, null);
            var withEmpty = AlignmentStationBreakdown.Build(els, 1000, null, new List<StationEquation>());

            withNull.TotalLengthM.Should().BeApproximately(plain.TotalLengthM, Tol);
            withNull.Segments[0].StationStartM.Should().BeApproximately(plain.Segments[0].StationStartM, Tol);
            withNull.Segments[0].StationEndM.Should().BeApproximately(plain.Segments[0].StationEndM, Tol);
            withEmpty.GeometryPoints[1].StationM.Should().BeApproximately(plain.GeometryPoints[1].StationM, Tol);
        }

        [Fact]
        public void BuildWithEquations_AppliesJumpAtBoundary()
        {
            // 单段直线 100 m。StartStation=0。方程：raw=40 → ahead=1040（+1000 跳）。
            // 段记录只有一段，StationStart=0，StationEnd=0 + 1040 + (100-40)= 1100。
            // 段长保持 100，StationEnd - StationStart = 1100 ≠ 100，此即方程跳变量。
            var els = new[]
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(100, 0)),
            };
            var eqs = new List<StationEquation> { new StationEquation(40, 1040) };

            var b = AlignmentStationBreakdown.Build(els, startStation: 0, options: null, equations: eqs);

            b.TotalLengthM.Should().BeApproximately(100, Tol, "物理长度与方程无关");
            b.Segments.Should().HaveCount(1);
            b.Segments[0].LengthM.Should().BeApproximately(100, Tol);
            b.Segments[0].StationStartM.Should().BeApproximately(0, Tol);
            b.Segments[0].StationEndM.Should().BeApproximately(1100, Tol);

            b.GeometryPoints[0].StationM.Should().BeApproximately(0, Tol);
            b.GeometryPoints[1].StationM.Should().BeApproximately(1100, Tol);
        }

        [Fact]
        public void BuildWithEquations_StartStationIsUnchangedAtBP()
        {
            var els = new[]
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(200, 0)),
            };
            var eqs = new List<StationEquation> { new StationEquation(50, 9999) };

            var b = AlignmentStationBreakdown.Build(els, startStation: 500, options: null, equations: eqs);

            // BP 的 raw=0，小于 BeforeRaw=50，显示桩号 = StartStation + 0 = 500
            b.StartStationM.Should().BeApproximately(500, Tol);
            b.GeometryPoints[0].StationM.Should().BeApproximately(500, Tol);
            // EP 的 raw=200，方程后 display = 9999 + (200-50) = 10149
            b.GeometryPoints[1].StationM.Should().BeApproximately(10149, Tol);
        }
    }
}
