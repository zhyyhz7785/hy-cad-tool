using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>
    /// ProfileFgDesigner 单测：覆盖 PVI 派生量、竖曲线几何（凸 / 凹）、ElevationAt 端点 clamp、
    /// 桩号倒序错误、相邻竖曲线重叠告警等典型路径。
    /// </summary>
    public class ProfileFgDesignerTests
    {
        private const double Eps = 1e-6;

        private static ProfileVertex Pvi(double s, double h, double r = 0)
            => new ProfileVertex { Station = s, Elevation = h, CurveRadius = r };

        // === Case 1: 输入校验 ===
        [Fact]
        public void Build_Throws_WhenVerticesIsNull()
        {
            Action act = () => ProfileFgDesigner.Build(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Build_Empty_ReturnsErrorResult()
        {
            var r = ProfileFgDesigner.Build(new List<ProfileVertex>());

            r.IsValid.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Should().Contain("空");
            r.Pvis.Should().BeEmpty();
            r.Segments.Should().BeEmpty();
        }

        // === Case 2: 单 PVI ===
        [Fact]
        public void Build_SinglePvi_ReturnsNoSegments_ButElevationAtClampsToThatPoint()
        {
            var r = ProfileFgDesigner.Build(new[] { Pvi(0, 100) });

            r.IsValid.Should().BeTrue();
            r.Pvis.Should().HaveCount(1);
            r.Segments.Should().BeEmpty();
            r.TotalLength.Should().Be(0);

            r.ElevationAt(-50).Should().BeApproximately(100, Eps);
            r.ElevationAt(0).Should().BeApproximately(100, Eps);
            r.ElevationAt(500).Should().BeApproximately(100, Eps);
        }

        // === Case 3: 双 PVI（纯直坡）===
        [Fact]
        public void Build_TwoPvis_StraightSlope_OneTangentSegment()
        {
            var r = ProfileFgDesigner.Build(new[] { Pvi(0, 100), Pvi(100, 105) });

            r.IsValid.Should().BeTrue();
            r.Pvis.Should().HaveCount(2);
            r.Segments.Should().HaveCount(1);

            var seg = r.Segments[0];
            seg.Type.Should().Be(ProfileSegmentType.Tangent);
            seg.GradeIn.Should().BeApproximately(0.05, Eps);
            seg.Length.Should().BeApproximately(100, Eps);
            seg.StartElevation.Should().Be(100);
            seg.EndElevation.Should().Be(105);

            r.ElevationAt(50).Should().BeApproximately(102.5, Eps);
            r.TotalLength.Should().BeApproximately(100, Eps);
        }

        // === Case 4: 三 PVI，中间 R=0（折线）===
        [Fact]
        public void Build_ThreePvis_ZeroRadius_TwoTangentSegmentsNoCurve()
        {
            var r = ProfileFgDesigner.Build(new[]
            {
                Pvi(0, 100),
                Pvi(100, 105, r: 0),
                Pvi(200, 100),
            });

            r.IsValid.Should().BeTrue();
            r.Segments.Should().HaveCount(2);
            r.Segments.All(s => s.Type == ProfileSegmentType.Tangent).Should().BeTrue();

            r.Pvis[1].GradeIn.Should().BeApproximately(0.05, Eps);
            r.Pvis[1].GradeOut.Should().BeApproximately(-0.05, Eps);
            r.Pvis[1].Omega.Should().BeApproximately(-0.10, Eps);
            r.Pvis[1].CurveLength.Should().Be(0); // R = 0 ⇒ L = 0
        }

        // === Case 5: 三 PVI，中间 R>0，凸曲线（先上后下）===
        [Fact]
        public void Build_ThreePvis_CrestCurve_BuildsThreeSegmentsWithCorrectGeometry()
        {
            // PVI1 = (200, 110)，前坡 +5%，后坡 -5%，R=2000 → L=200, BCV=K100, ECV=K300
            // h_BCV = 110 - 0.05·100 = 105，h_ECV = 110 + (-0.05)·100 = 105
            // 顶点（PVI 桩号处）：x = L/2 = 100，y = 105 + 0.05·100 + (-0.10)·10000/(2·200) = 105 + 5 - 2.5 = 107.5
            var pvis = new[]
            {
                Pvi(0, 100),
                Pvi(200, 110, r: 2000),
                Pvi(400, 100),
            };
            var r = ProfileFgDesigner.Build(pvis);

            r.IsValid.Should().BeTrue();
            r.Pvis[1].Omega.Should().BeApproximately(-0.10, Eps);
            r.Pvis[1].CurveLength.Should().BeApproximately(200, Eps);
            r.Pvis[1].IsCrest.Should().BeTrue();
            r.Pvis[1].IsSag.Should().BeFalse();
            r.Pvis[1].BcvStation.Should().BeApproximately(100, Eps);
            r.Pvis[1].EcvStation.Should().BeApproximately(300, Eps);
            r.Pvis[1].BcvElevation.Should().BeApproximately(105, Eps);
            r.Pvis[1].EcvElevation.Should().BeApproximately(105, Eps);

            r.Segments.Should().HaveCount(3);
            r.Segments[0].Type.Should().Be(ProfileSegmentType.Tangent);
            r.Segments[1].Type.Should().Be(ProfileSegmentType.VerticalCurve);
            r.Segments[2].Type.Should().Be(ProfileSegmentType.Tangent);

            // 在抛物线顶点（PVI 桩号 200）应高于 PVI 高程 110？错——凸曲线"低于"两侧切线交点。
            // 凸顶点高程 = h_PVI - |ω|·L/8 = 110 - 0.1·200/8 = 110 - 2.5 = 107.5
            r.ElevationAt(200).Should().BeApproximately(107.5, 1e-3);

            // BCV 处与切线连续
            r.ElevationAt(100).Should().BeApproximately(105, Eps);
            r.ElevationAt(300).Should().BeApproximately(105, Eps);
        }

        // === Case 6: 三 PVI，中间 R>0，凹曲线（先下后上）===
        [Fact]
        public void Build_ThreePvis_SagCurve_OmegaPositiveAndIsSagTrue()
        {
            // 前坡 -3%，后坡 +3%，R=1500 → ω = +6%, L = 90
            var r = ProfileFgDesigner.Build(new[]
            {
                Pvi(0, 100),
                Pvi(300, 91, r: 1500),
                Pvi(600, 100),
            });

            r.Pvis[1].GradeIn.Should().BeApproximately(-0.03, Eps);
            r.Pvis[1].GradeOut.Should().BeApproximately(0.03, Eps);
            r.Pvis[1].Omega.Should().BeApproximately(0.06, Eps);
            r.Pvis[1].CurveLength.Should().BeApproximately(90, Eps);
            r.Pvis[1].IsSag.Should().BeTrue();
            r.Pvis[1].IsCrest.Should().BeFalse();

            // 凹曲线最低点高程 = h_PVI + |ω|·L/8 = 91 + 0.06·90/8 = 91 + 0.675 = 91.675
            r.ElevationAt(300).Should().BeApproximately(91.675, 1e-3);
        }

        // === Case 7: 桩号倒序 → Error ===
        [Fact]
        public void Build_StationsOutOfOrder_ReportsError()
        {
            var r = ProfileFgDesigner.Build(new[]
            {
                Pvi(0, 100),
                Pvi(200, 105),
                Pvi(150, 110), // 倒序
            });

            r.IsValid.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Should().Contain("倒序");
            r.Segments.Should().BeEmpty();
        }

        // === Case 8: 桩号重合 → Error ===
        [Fact]
        public void Build_DuplicateStations_ReportsError()
        {
            var r = ProfileFgDesigner.Build(new[]
            {
                Pvi(0, 100),
                Pvi(100, 105),
                Pvi(100, 110),
            });

            r.IsValid.Should().BeFalse();
            r.Errors.Should().HaveCount(1);
        }

        // === Case 9: 相邻竖曲线重叠 → Warning ===
        [Fact]
        public void Build_AdjacentCurvesOverlap_ProducesWarning()
        {
            // PVI 间距 100m，但两条竖曲线 L 都是 80m，半长 40+40 = 80 ≤ 100 → 不重叠
            // 把 R 调大到让 L > 100
            var r = ProfileFgDesigner.Build(new[]
            {
                Pvi(0, 100),
                Pvi(100, 110, r: 2000),  // ω ≈ -0.2 → L = 400
                Pvi(200, 100, r: 2000),  // ω ≈ +0.2 → L = 400
                Pvi(300, 110),
            });

            r.HasWarnings.Should().BeTrue();
            r.Warnings.Should().NotBeEmpty();
            r.Warnings.Should().Contain(w => w.Contains("重叠") || w.Contains("超出"));
        }

        // === Case 10: ElevationAt 端点 clamp ===
        [Fact]
        public void ElevationAt_OutsideRange_ClampsToEndpoints()
        {
            var r = ProfileFgDesigner.Build(new[]
            {
                Pvi(100, 50),
                Pvi(200, 60),
            });

            r.ElevationAt(0).Should().BeApproximately(50, Eps);     // 左外 clamp
            r.ElevationAt(100).Should().BeApproximately(50, Eps);
            r.ElevationAt(150).Should().BeApproximately(55, Eps);
            r.ElevationAt(200).Should().BeApproximately(60, Eps);
            r.ElevationAt(300).Should().BeApproximately(60, Eps);   // 右外 clamp
        }

        // === Case 11: 端点 PVI 强制 R=0（即便用户给了 R）===
        [Fact]
        public void Build_EndpointsAlwaysHaveZeroDerivedCurveLength_EvenIfRadiusGiven()
        {
            var r = ProfileFgDesigner.Build(new[]
            {
                Pvi(0, 100, r: 5000),  // 端点：忽略 R
                Pvi(200, 110, r: 0),
                Pvi(400, 100, r: 5000), // 端点：忽略 R
            });

            r.IsValid.Should().BeTrue();
            r.Pvis[0].CurveLength.Should().Be(0);
            r.Pvis[2].CurveLength.Should().Be(0);
            r.Pvis[0].Omega.Should().Be(0);
            r.Pvis[2].Omega.Should().Be(0);
        }

        // === Case 12: TotalLength 与 PVI 桩号边界一致 ===
        [Fact]
        public void TotalLength_EqualsLastMinusFirstStation()
        {
            var r = ProfileFgDesigner.Build(new[]
            {
                Pvi(100, 50),
                Pvi(300, 60),
                Pvi(700, 55),
            });

            r.TotalLength.Should().BeApproximately(600, Eps);
        }
    }
}
