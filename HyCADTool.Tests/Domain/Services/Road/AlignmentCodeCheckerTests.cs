using System;
using System.Linq;
using FluentAssertions;
using HyCADTool.Domain.Services.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    public class AlignmentCodeCheckerTests
    {
        private const double PI = Math.PI;

        private static double Deg(double deg) => deg * PI / 180.0;

        [Fact]
        public void ClampSpeed_SelectsNearestSupported()
        {
            AlignmentCodeChecker.ClampSpeed(40).Should().Be(40);
            AlignmentCodeChecker.ClampSpeed(55).Should().Be(50); // 距 50 更近
            AlignmentCodeChecker.ClampSpeed(70).Should().Be(60); // 70 距 60/80 相等，先命中 60
            AlignmentCodeChecker.ClampSpeed(500).Should().Be(80);
            AlignmentCodeChecker.ClampSpeed(0).Should().Be(40);
        }

        [Fact]
        public void Check_HappyPath_AllItemsPass()
        {
            // 60 km/h：R=300(≥200), Ls=80(≥50), θ=30°, 前/后直线 300m → 全合格
            var report = AlignmentCodeChecker.Check(
                designSpeed: 60,
                turnRad: Deg(30),
                radius: 300,
                lsIn: 80,
                lsOut: 80,
                prevTangentLen: 300,
                nextTangentLen: 300);

            report.Items.Should().HaveCount(6);
            report.AllPassed.Should().BeTrue();
            report.Items.All(i => i.Passed).Should().BeTrue();
        }

        [Fact]
        public void Check_RadiusBelowMinimum_RItemFails()
        {
            var report = AlignmentCodeChecker.Check(
                designSpeed: 60,
                turnRad: Deg(20),
                radius: 150, // 60 km/h 最小 200
                lsIn: 70,
                lsOut: 70,
                prevTangentLen: 300,
                nextTangentLen: 300);

            var rItem = report.Items.First(i => i.Name.Contains("R"));
            rItem.Passed.Should().BeFalse();
            rItem.Message.Should().Contain("≥ 200");
        }

        [Fact]
        public void Check_LsBelowMinimum_LsItemFails()
        {
            var report = AlignmentCodeChecker.Check(
                designSpeed: 60,
                turnRad: Deg(20),
                radius: 300,
                lsIn: 30, // 60 km/h 最小 50
                lsOut: 30,
                prevTangentLen: 200,
                nextTangentLen: 200);

            var lsItem = report.Items.First(i => i.Name.Contains("Ls"));
            lsItem.Passed.Should().BeFalse();
        }

        [Fact]
        public void Check_LyBelowMinimum_LyItemFails()
        {
            // R=200, θ=10°, Ls=70,70 → Ly = 200*(10π/180) - 70 ≈ -35 m → 0，远小于 50 → 不合格
            var report = AlignmentCodeChecker.Check(
                designSpeed: 60,
                turnRad: Deg(10),
                radius: 200,
                lsIn: 70,
                lsOut: 70,
                prevTangentLen: 300,
                nextTangentLen: 300);

            var lyItem = report.Items.First(i => i.Name.Contains("Ly"));
            lyItem.Passed.Should().BeFalse();
        }

        [Fact]
        public void Check_TangentLongerThanStraight_TItemFails()
        {
            // θ=60°, R=200, Ls=0 → T = 200 * tan(30°) ≈ 115.47m；前直线仅 80m → 不合格
            var report = AlignmentCodeChecker.Check(
                designSpeed: 50,
                turnRad: Deg(60),
                radius: 200,
                lsIn: 0,
                lsOut: 0,
                prevTangentLen: 80,
                nextTangentLen: 300);

            var tItem = report.Items.First(i => i.Name.Contains("邻段"));
            tItem.Passed.Should().BeFalse();
            tItem.Message.Should().Contain("T1");
        }

        [Fact]
        public void Check_AsymmetricBeyondTolerance_SymItemFails()
        {
            var report = AlignmentCodeChecker.Check(
                designSpeed: 60,
                turnRad: Deg(30),
                radius: 300,
                lsIn: 60,
                lsOut: 80, // 差 20 m > 1 m
                prevTangentLen: 300,
                nextTangentLen: 300);

            var symItem = report.Items.First(i => i.Name.Contains("对称"));
            symItem.Passed.Should().BeFalse();
            symItem.Message.Should().Contain("非对称");
        }

        [Fact]
        public void Check_WithinSymTolerance_SymItemPasses()
        {
            var report = AlignmentCodeChecker.Check(
                designSpeed: 60,
                turnRad: Deg(30),
                radius: 300,
                lsIn: 79.6,
                lsOut: 80.3, // 差 0.7 m ≤ 1 m
                prevTangentLen: 300,
                nextTangentLen: 300);

            var symItem = report.Items.First(i => i.Name.Contains("对称"));
            symItem.Passed.Should().BeTrue();
        }

        [Fact]
        public void Check_SmallTurn_AllowsNoSpiral()
        {
            // |θ| = 5° < 7°，不强制缓和曲线，即使 Ls=0 也该通过转角分类项
            var report = AlignmentCodeChecker.Check(
                designSpeed: 60,
                turnRad: Deg(5),
                radius: 500,
                lsIn: 0,
                lsOut: 0,
                prevTangentLen: 300,
                nextTangentLen: 300);

            var turnItem = report.Items.First(i => i.Name.Contains("转角"));
            turnItem.Passed.Should().BeTrue();
            turnItem.Message.Should().Contain("小偏角");

            var lsItem = report.Items.First(i => i.Name.Contains("Ls"));
            lsItem.Passed.Should().BeTrue();
        }

        [Fact]
        public void Check_GeneralCurveWithoutSpiral_TurnItemFails()
        {
            // |θ|=30° ≥ 7°，Ls=0 → 必须设缓和曲线但未设
            var report = AlignmentCodeChecker.Check(
                designSpeed: 60,
                turnRad: Deg(30),
                radius: 300,
                lsIn: 0,
                lsOut: 0,
                prevTangentLen: 300,
                nextTangentLen: 300);

            var turnItem = report.Items.First(i => i.Name.Contains("转角"));
            turnItem.Passed.Should().BeFalse();
            turnItem.Message.Should().Contain("缺缓和曲线");
        }

        [Fact]
        public void Check_ReportExposesDerivedValues()
        {
            var report = AlignmentCodeChecker.Check(
                designSpeed: 60,
                turnRad: Deg(41.187),
                radius: 200,
                lsIn: 70,
                lsOut: 70,
                prevTangentLen: 300,
                nextTangentLen: 300);

            report.DesignSpeed.Should().Be(60);
            report.Radius.Should().Be(200);
            report.T1.Should().BeApproximately(110.4958, 0.1);
            report.T2.Should().BeApproximately(110.4958, 0.1);
            report.Ly.Should().BeApproximately(73.77, 0.05);
        }

        // ============================ Suggestion: 修复建议文本 ============================

        [Fact]
        public void PassedItem_HasNoSuggestion()
        {
            var report = AlignmentCodeChecker.Check(
                designSpeed: 60, turnRad: Deg(30), radius: 300, lsIn: 80, lsOut: 80,
                prevTangentLen: 300, nextTangentLen: 300);

            report.Items.All(i => i.Passed).Should().BeTrue();
            report.Items.All(i => !i.HasSuggestion).Should().BeTrue();
        }

        [Fact]
        public void FailedR_Suggests_RaiseRadius_OrLowerSpeed()
        {
            // R=150 < 60km/h R_min(200)；当前 R 在 50km/h R_min(150) 档上刚好及格
            var report = AlignmentCodeChecker.Check(
                designSpeed: 60, turnRad: Deg(20), radius: 150, lsIn: 70, lsOut: 70,
                prevTangentLen: 300, nextTangentLen: 300);

            var rItem = report.Items.First(i => i.Name.Contains("R"));
            rItem.Passed.Should().BeFalse();
            rItem.HasSuggestion.Should().BeTrue();
            rItem.Suggestion.Should().Contain("200");       // 目标半径值
            rItem.Suggestion.Should().Contain("50 km/h");    // 降速方案
        }

        [Fact]
        public void FailedLs_Suggests_RaiseLs()
        {
            var report = AlignmentCodeChecker.Check(
                designSpeed: 60, turnRad: Deg(20), radius: 300, lsIn: 30, lsOut: 30,
                prevTangentLen: 200, nextTangentLen: 200);

            var lsItem = report.Items.First(i => i.Name.Contains("Ls"));
            lsItem.Passed.Should().BeFalse();
            lsItem.HasSuggestion.Should().BeTrue();
            lsItem.Suggestion.Should().Contain("50"); // 60 km/h Ls_min = 50
        }

        [Fact]
        public void FailedT_Suggests_LowerRadius_OrLowerLs_OrMovePi()
        {
            // θ=60°, R=200, Ls=0 → T≈115.47，前直线 80 不够
            var report = AlignmentCodeChecker.Check(
                designSpeed: 50, turnRad: Deg(60), radius: 200, lsIn: 0, lsOut: 0,
                prevTangentLen: 80, nextTangentLen: 300);

            var tItem = report.Items.First(i => i.Name.Contains("邻段"));
            tItem.Passed.Should().BeFalse();
            tItem.HasSuggestion.Should().BeTrue();
            // 至少一条"降 R"、一条"挪 PI"建议
            tItem.Suggestion.Should().Contain("R");
            tItem.Suggestion.Should().Contain("挪");
        }

        [Fact]
        public void FailedLy_Suggests_LowerLs_OrRaiseRadius()
        {
            // R=200, θ=10°, Ls=70 → Ly 很小 / 负
            var report = AlignmentCodeChecker.Check(
                designSpeed: 60, turnRad: Deg(10), radius: 200, lsIn: 70, lsOut: 70,
                prevTangentLen: 300, nextTangentLen: 300);

            var lyItem = report.Items.First(i => i.Name.Contains("Ly"));
            lyItem.Passed.Should().BeFalse();
            lyItem.HasSuggestion.Should().BeTrue();
            lyItem.Suggestion.Should().MatchRegex("Ls|R"); // 不限具体方案，但必须给其一
        }

        [Fact]
        public void FailedSymmetry_Suggests_TogglingLockOrAverage()
        {
            var report = AlignmentCodeChecker.Check(
                designSpeed: 60, turnRad: Deg(30), radius: 300, lsIn: 60, lsOut: 80,
                prevTangentLen: 300, nextTangentLen: 300);

            var symItem = report.Items.First(i => i.Name.Contains("对称"));
            symItem.Passed.Should().BeFalse();
            symItem.HasSuggestion.Should().BeTrue();
            symItem.Suggestion.Should().Contain("70.0"); // 平均值
        }

        [Fact]
        public void ToString_IncludesSuggestion_WhenFailed()
        {
            var report = AlignmentCodeChecker.Check(
                designSpeed: 60, turnRad: Deg(20), radius: 150, lsIn: 70, lsOut: 70,
                prevTangentLen: 300, nextTangentLen: 300);

            var rItem = report.Items.First(i => i.Name.Contains("R"));
            rItem.ToString().Should().Contain("→").And.Contain("建议");
        }
    }
}
