using System;
using System.Linq;
using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>
    /// ProfileCodeChecker 单测：覆盖 4 项规范的合格 / 超限 / 端到端、speed clamp、空数据兜底。
    /// </summary>
    public class ProfileCodeCheckerTests
    {
        private static ProfileVertex Pvi(double s, double h, double r = 0)
            => new ProfileVertex { Station = s, Elevation = h, CurveRadius = r };

        // === Case 1: 全部合格（60 km/h，3% 坡度，凸/凹各一）===
        [Fact]
        public void Check_HappyPath_AllPassed()
        {
            // 0→200 +3%（h: 100→106），200→400 -3%（h: 106→100），R=2000 凸；
            // 400→600 +3%，R=2000 凹
            var pvis = new[]
            {
                Pvi(0, 100),
                Pvi(200, 106, r: 2000),  // 凸（ω = -0.06 < 0），R=2000 ≥ 1400 ✓
                Pvi(400, 100, r: 1100),  // 凹（ω = +0.06 > 0），R=1100 ≥ 1050 ✓
                Pvi(600, 106),
            };
            var fg = ProfileFgDesigner.Build(pvis);
            var report = ProfileCodeChecker.Check(60, fg);

            report.Items.Should().HaveCount(4);
            report.AllPassed.Should().BeTrue();
            report.FailedCount.Should().Be(0);
        }

        // === Case 2: 最大纵坡超限 ===
        [Fact]
        public void Check_MaxGradeExceeded_FirstItemFails()
        {
            // 60 km/h 限值 6%；这里 +8%
            var pvis = new[]
            {
                Pvi(0, 100),
                Pvi(100, 108),
            };
            var fg = ProfileFgDesigner.Build(pvis);
            var report = ProfileCodeChecker.Check(60, fg);

            report.Items[0].Passed.Should().BeFalse();
            report.Items[0].Message.Should().Contain("超过");
            report.Items[0].HasSuggestion.Should().BeTrue();
        }

        // === Case 3: 最小纵坡过缓（< 0.3%）===
        [Fact]
        public void Check_MinGradeUnder_SecondItemFails()
        {
            // 0.1% 坡度
            var pvis = new[]
            {
                Pvi(0, 100),
                Pvi(1000, 100.1),
            };
            var fg = ProfileFgDesigner.Build(pvis);
            var report = ProfileCodeChecker.Check(60, fg);

            report.Items[1].Passed.Should().BeFalse();
            report.Items[1].Message.Should().Contain("低于");
        }

        // === Case 4: 凸曲线半径不足 ===
        [Fact]
        public void Check_CrestRadiusUnder_ThirdItemFails()
        {
            // 60 km/h 凸最小 1400；这里给 800
            var pvis = new[]
            {
                Pvi(0, 100),
                Pvi(300, 109, r: 800), // 凸：+3% → -3%，但 R=800 < 1400
                Pvi(600, 100),
            };
            var fg = ProfileFgDesigner.Build(pvis);
            var report = ProfileCodeChecker.Check(60, fg);

            report.Items[2].Passed.Should().BeFalse();
            report.Items[2].Message.Should().Contain("1400");
        }

        // === Case 5: 凹曲线半径不足 ===
        [Fact]
        public void Check_SagRadiusUnder_FourthItemFails()
        {
            // 60 km/h 凹最小 1050；这里给 600
            var pvis = new[]
            {
                Pvi(0, 100),
                Pvi(300, 91, r: 600), // 凹：-3% → +3%，R=600 < 1050
                Pvi(600, 100),
            };
            var fg = ProfileFgDesigner.Build(pvis);
            var report = ProfileCodeChecker.Check(60, fg);

            report.Items[3].Passed.Should().BeFalse();
            report.Items[3].Message.Should().Contain("1050");
        }

        // === Case 6: 凸曲线 PVI 但未设半径 → 凸项 NG ===
        [Fact]
        public void Check_CrestPviWithoutRadius_FlaggedAsMissing()
        {
            var pvis = new[]
            {
                Pvi(0, 100),
                Pvi(200, 106, r: 0), // 凸但 R=0
                Pvi(400, 100),
            };
            var fg = ProfileFgDesigner.Build(pvis);
            var report = ProfileCodeChecker.Check(60, fg);

            report.Items[2].Passed.Should().BeFalse();
            report.Items[2].Message.Should().Contain("未设半径");
        }

        // === Case 7: PVI 数 < 2 → 全部项 Passed (无可校核) ===
        [Fact]
        public void Check_NotEnoughPvis_AllPass_WithGracefulMessage()
        {
            var fg = ProfileFgDesigner.Build(new[] { Pvi(0, 100) });
            var report = ProfileCodeChecker.Check(60, fg);

            report.Items.Should().HaveCount(4);
            report.AllPassed.Should().BeTrue();
            report.Items.All(i => i.Message.Contains("PVI") || i.Message.Contains("无")).Should().BeTrue();
        }

        // === Case 8: 速度 clamp（70 → 60，500 → 80）===
        [Fact]
        public void ClampSpeed_SelectsNearestSupported()
        {
            ProfileCodeChecker.ClampSpeed(40).Should().Be(40);
            ProfileCodeChecker.ClampSpeed(55).Should().Be(50);
            ProfileCodeChecker.ClampSpeed(70).Should().Be(60); // 70 距 60/80 相等，先命中 60
            ProfileCodeChecker.ClampSpeed(500).Should().Be(80);
            ProfileCodeChecker.ClampSpeed(0).Should().Be(40);
        }

        // === Case 9: 速度档影响半径阈值（80 km/h 凸最小 3000）===
        [Fact]
        public void Check_HigherSpeed_RaisesRadiusThreshold()
        {
            // 凸 R = 2000：60 km/h 通过（≥1400），80 km/h 不过（< 3000）
            var pvis = new[]
            {
                Pvi(0, 100),
                Pvi(300, 109, r: 2000),
                Pvi(600, 100),
            };
            var fg = ProfileFgDesigner.Build(pvis);

            ProfileCodeChecker.Check(60, fg).Items[2].Passed.Should().BeTrue();
            ProfileCodeChecker.Check(80, fg).Items[2].Passed.Should().BeFalse();
        }

        // === Case 10: Profile 重载读取 DesignSpeed ===
        [Fact]
        public void Check_OverloadByProfile_UsesProfileDesignSpeed()
        {
            var profile = new Profile { DesignSpeed = 80 };
            profile.Vertices.Add(Pvi(0, 100));
            profile.Vertices.Add(Pvi(300, 109, r: 2000));
            profile.Vertices.Add(Pvi(600, 100));
            var fg = ProfileFgDesigner.Build(profile.Vertices);

            var report = ProfileCodeChecker.Check(profile, fg);

            report.DesignSpeed.Should().Be(80);
            report.Items[2].Passed.Should().BeFalse(); // 80 km/h 凸 R=2000 < 3000
        }

        // === Case 11: AllPassed 与 FailedCount 一致 ===
        [Fact]
        public void AllPassedAndFailedCount_AreConsistent()
        {
            // 制造 2 项失败：最大纵坡 + 凸 R 不足
            var pvis = new[]
            {
                Pvi(0, 100),
                Pvi(100, 110, r: 500), // +10% 超限 + 凸 R=500 < 1400
                Pvi(200, 100),
            };
            var fg = ProfileFgDesigner.Build(pvis);
            var report = ProfileCodeChecker.Check(60, fg);

            report.AllPassed.Should().BeFalse();
            report.FailedCount.Should().Be(2);
            report.Items.Count(i => !i.Passed).Should().Be(2);
        }

        // === Case 12: Check(profile=null) → 抛异常 ===
        [Fact]
        public void Check_NullProfile_Throws()
        {
            Action act = () => ProfileCodeChecker.Check((Profile)null, null);
            act.Should().Throw<ArgumentNullException>();
        }
    }
}
