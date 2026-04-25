using System;
using Autodesk.AutoCAD.Geometry;
using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using Xunit;

namespace HyCADTool.Tests.Infrastructure.AutoCAD.Services.Road
{
    /// <summary>
    /// <see cref="ProfileLabelDrawService"/> 的纯逻辑测试。
    /// AutoCAD <c>Transaction</c> / <c>Database</c> 必须运行在 acad.exe 内才能构造，
    /// 因此 Draw / Clear 主路径只能在 AutoCAD 联调，本测试聚焦：
    ///   - <see cref="ProfileLabelDrawOptions.Validate"/> 抛异常的边界
    ///   - <see cref="ProfileLabelDrawResult"/> 累计 / Total / ToString
    ///   - <see cref="ProfileLabelDrawService.FormatStation"/> 桩号格式
    ///   - <see cref="ProfileLabelDrawService.ResolveBounds"/> 包络与高程基准
    ///   - <see cref="ProfileLabelDrawService.ToDrawing"/> 坐标变换
    /// 这些方法为 <c>internal static</c>，通过 <c>InternalsVisibleTo</c> 暴露给测试程序集。
    /// </summary>
    public sealed class ProfileLabelDrawServiceTests
    {
        // ============================== Options ==============================

        [Fact]
        public void Options_Default_HasReasonableValues()
        {
            var o = ProfileLabelDrawOptions.Default;

            o.XScale.Should().Be(1.0);
            o.YScale.Should().Be(10.0);
            o.StationGridIntervalM.Should().Be(20.0);
            o.ElevationGridIntervalM.Should().Be(1.0);
            o.VerticalCurveSegments.Should().Be(30);
            o.TextHeight.Should().BeGreaterThan(0);
            o.PviCircleRadius.Should().BeGreaterThan(0);
            o.DrawGrid.Should().BeTrue();
            o.DrawEg.Should().BeTrue();
            o.DrawPviAnnotations.Should().BeTrue();
            o.RotateStationText.Should().BeFalse();
        }

        [Fact]
        public void Options_Validate_DefaultPasses()
        {
            Action act = () => ProfileLabelDrawOptions.Default.Validate();
            act.Should().NotThrow();
        }

        [Theory]
        [InlineData("XScale")]
        [InlineData("YScale")]
        [InlineData("StationGridIntervalM")]
        [InlineData("ElevationGridIntervalM")]
        [InlineData("TextHeight")]
        [InlineData("PviCircleRadius")]
        public void Options_Validate_ZeroOrNegative_Throws(string field)
        {
            var o = new ProfileLabelDrawOptions();
            switch (field)
            {
                case "XScale": o.XScale = 0; break;
                case "YScale": o.YScale = -1; break;
                case "StationGridIntervalM": o.StationGridIntervalM = 0; break;
                case "ElevationGridIntervalM": o.ElevationGridIntervalM = 0; break;
                case "TextHeight": o.TextHeight = -0.1; break;
                case "PviCircleRadius": o.PviCircleRadius = 0; break;
            }
            Action act = () => o.Validate();
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Options_Validate_VerticalCurveSegmentsLessThan2_Throws()
        {
            var o = new ProfileLabelDrawOptions { VerticalCurveSegments = 1 };
            Action act = () => o.Validate();
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        // ============================== Result ==============================

        [Fact]
        public void Result_Total_SumsAllCategories()
        {
            var r = new ProfileLabelDrawResult
            {
                LinesDrawn = 3,
                PolylinesDrawn = 2,
                TextsDrawn = 5,
                CirclesDrawn = 1
            };
            r.Total.Should().Be(11);
        }

        [Fact]
        public void Result_ToString_ContainsCounts()
        {
            var r = new ProfileLabelDrawResult { LinesDrawn = 1, TextsDrawn = 2 };
            r.ToString().Should().Contain("Lines=1").And.Contain("Texts=2").And.Contain("Total=3");
        }

        // ============================== FormatStation ==============================

        [Theory]
        [InlineData(0.0, "K0+000.00")]
        [InlineData(20.0, "K0+020.00")]
        [InlineData(199.5, "K0+199.50")]
        [InlineData(1000.0, "K1+000.00")]
        [InlineData(1234.56, "K1+234.56")]
        [InlineData(12_345.0, "K12+345.00")]
        public void FormatStation_PositiveStations_FormatsAsHongyeStyle(double stationM, string expected)
        {
            ProfileLabelDrawService.FormatStation(stationM).Should().Be(expected);
        }

        [Fact]
        public void FormatStation_NegativeStation_PrefixesMinus()
        {
            ProfileLabelDrawService.FormatStation(-50.0).Should().Be("-K0+050.00");
        }

        // ============================== ResolveBounds ==============================

        [Fact]
        public void ResolveBounds_FgOnly_BoundsCoverFgVertices()
        {
            var fg = MakeProfile("FG", isDesign: true,
                (0, 100), (200, 102), (500, 95));

            ProfileLabelDrawService.ResolveBounds(fg, eg: null,
                ProfileLabelDrawOptions.Default,
                out double sMin, out double sMax, out double hRef, out double hMax);

            sMin.Should().Be(0);
            sMax.Should().Be(500);
            hRef.Should().Be(95); // floor(95 / 1) * 1
            hMax.Should().Be(102); // ceil(102 / 1) * 1
        }

        [Fact]
        public void ResolveBounds_FgAndEg_TakesUnionEnvelope()
        {
            var fg = MakeProfile("FG", isDesign: true, (0, 100), (300, 105));
            var eg = MakeProfile("EG", isDesign: false, (0, 99.3), (300, 96.7));

            ProfileLabelDrawService.ResolveBounds(fg, eg,
                ProfileLabelDrawOptions.Default,
                out double sMin, out double sMax, out double hRef, out double hMax);

            sMin.Should().Be(0);
            sMax.Should().Be(300);
            hRef.Should().Be(96); // floor(96.7 / 1)
            hMax.Should().Be(105); // ceil(105 / 1)
        }

        [Fact]
        public void ResolveBounds_RoundsHrefDownAndHmaxUpToGrid()
        {
            var fg = MakeProfile("FG", isDesign: true, (0, 100.4), (100, 102.7));
            var options = new ProfileLabelDrawOptions { ElevationGridIntervalM = 0.5 };

            ProfileLabelDrawService.ResolveBounds(fg, eg: null, options,
                out _, out _, out double hRef, out double hMax);

            hRef.Should().BeApproximately(100.0, 1e-9); // floor(100.4 / 0.5) * 0.5 = 100.0
            hMax.Should().BeApproximately(103.0, 1e-9); // ceil(102.7 / 0.5) * 0.5 = 103.0
        }

        [Fact]
        public void ResolveBounds_FlatProfile_LeavesAtLeastOneGridRow()
        {
            // 所有顶点高程相同 → ceil 后 hMax == hRef，需要 +step 至少留 1 行
            var fg = MakeProfile("FG", isDesign: true, (0, 100), (100, 100));

            ProfileLabelDrawService.ResolveBounds(fg, eg: null,
                ProfileLabelDrawOptions.Default,
                out _, out _, out double hRef, out double hMax);

            (hMax - hRef).Should().BeGreaterOrEqualTo(1.0);
        }

        [Fact]
        public void ResolveBounds_EmptyProfiles_ReturnsZeros()
        {
            var fg = new Profile { Name = "FG", IsDesignProfile = true };

            ProfileLabelDrawService.ResolveBounds(fg, eg: null,
                ProfileLabelDrawOptions.Default,
                out double sMin, out double sMax, out double hRef, out double hMax);

            sMin.Should().Be(0);
            sMax.Should().Be(0);
            hRef.Should().Be(0);
            hMax.Should().Be(0);
        }

        // ============================== ToDrawing ==============================
        //
        // Autodesk.AutoCAD.Geometry.Point3d 的 X/Y/Z 访问器走 native acmgd 桥，
        // 脱离 acad.exe 进程时 .NET 4.8 host 会抛 InvalidProgramException。
        // 因此 ToDrawing 的端到端验证只能放到 AutoCAD 联调里完成；下面两个测试
        // 标记 Skip，仅保留为契约文档（输入/输出公式的可读断言）。

        [Fact(Skip = "Point3d.get_X 需要 AutoCAD runtime；契约用作文档保留。")]
        public void ToDrawing_OriginAtZero_PlacesPointByScale()
        {
            var origin = new Point3d(0, 0, 0);
            var p = ProfileLabelDrawService.ToDrawing(
                station: 100, elevation: 105,
                sMin: 0, hRef: 100,
                xScale: 1, yScale: 10,
                origin: origin);

            // X = 0 + (100-0)*1 = 100
            // Y = 0 + (105-100)*10 = 50
            p.X.Should().BeApproximately(100, 1e-9);
            p.Y.Should().BeApproximately(50, 1e-9);
            p.Z.Should().Be(0);
        }

        [Fact(Skip = "Point3d.get_X 需要 AutoCAD runtime；契约用作文档保留。")]
        public void ToDrawing_OriginShifted_PreservesOffset()
        {
            var origin = new Point3d(1000, 200, 5);
            var p = ProfileLabelDrawService.ToDrawing(
                station: 50, elevation: 102,
                sMin: 50, hRef: 100,
                xScale: 2, yScale: 10,
                origin: origin);

            // X = 1000 + (50-50)*2 = 1000
            // Y = 200 + (102-100)*10 = 220
            p.X.Should().BeApproximately(1000, 1e-9);
            p.Y.Should().BeApproximately(220, 1e-9);
            p.Z.Should().Be(5);
        }

        // ============================== helpers ==============================

        private static Profile MakeProfile(string name, bool isDesign,
            params (double station, double elevation)[] verts)
        {
            var p = new Profile { Name = name, IsDesignProfile = isDesign };
            foreach (var (s, h) in verts)
            {
                p.Vertices.Add(new ProfileVertex { Station = s, Elevation = h });
            }
            return p;
        }
    }
}
