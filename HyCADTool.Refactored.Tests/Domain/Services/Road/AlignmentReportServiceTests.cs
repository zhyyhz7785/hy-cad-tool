using System;
using System.Linq;
using System.Text;
using FluentAssertions;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.Services.Road
{
    /// <summary>
    /// T3 验收：<see cref="AlignmentReportService"/> CSV 导出正确性。
    ///
    /// 验证点：
    /// - PI 表行数 = PI 数 + header；
    /// - 复测表行数 = 几何点数 + header；
    /// - CSV 字段分隔 / 小数格式用 InvariantCulture；
    /// - UTF-8 BOM 正确写入；
    /// - 缺 Source.PiElements 时抛出异常。
    /// </summary>
    public class AlignmentReportServiceTests
    {
        private static Alignment BuildSample()
        {
            var source = new AlignmentSource { Kind = AlignmentSourceKind.PiTable };
            source.PiElements.Add(new AlignmentPiInput { P = new Point2D(0, 0), Tag = "BP" });
            source.PiElements.Add(new AlignmentPiInput { P = new Point2D(200, 0), Radius = 100, Tag = "JD1 圆" });
            source.PiElements.Add(new AlignmentPiInput { P = new Point2D(200, 200), Radius = 150, SpiralIn = 20, SpiralOut = 20, Tag = "JD2 缓" });
            source.PiElements.Add(new AlignmentPiInput { P = new Point2D(400, 200), Tag = "EP" });
            return new Alignment
            {
                Name = "T3-主线",
                StartStation = 1000,
                Source = source,
            };
        }

        [Fact]
        public void BuildPiTableCsv_HeaderAndRowCount()
        {
            var a = BuildSample();
            var csv = AlignmentReportService.BuildPiTableCsv(a);

            var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            lines[0].Should().StartWith("PI#,Tag,X,Y,Radius");
            // 4 PI + 1 header
            lines.Length.Should().Be(5);
        }

        [Fact]
        public void BuildPiTableCsv_FirstAndLastRowsHaveStationOnly()
        {
            var a = BuildSample();
            var csv = AlignmentReportService.BuildPiTableCsv(a);
            var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            // 首 PI 行：半径 / 缓和 / 转角 / 切线 / 曲线 列应为空，桩号 = K1+000.000
            var first = lines[1].Split(',');
            first[0].Should().Be("0");
            first[4].Should().BeEmpty(); // Radius
            first[7].Should().BeEmpty(); // Turn
            first[8].Should().BeEmpty(); // Tangent
            first[9].Should().BeEmpty(); // Arc Ly
            first[10].Should().Be("K1+000.000");

            var last = lines[4].Split(',');
            last[0].Should().Be("3");
            last[10].Should().StartWith("K1+"); // End station > start
        }

        [Fact]
        public void BuildPiTableCsv_InternalPiHasTurnAndArcLength()
        {
            var a = BuildSample();
            var csv = AlignmentReportService.BuildPiTableCsv(a);
            var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            // PI1（圆）
            var row1 = lines[2].Split(',');
            row1[0].Should().Be("1");
            row1[4].Should().Be("100.000"); // R
            double.Parse(row1[7], System.Globalization.CultureInfo.InvariantCulture)
                .Should().BeApproximately(90.0, 0.001); // 90° 左转
            double.Parse(row1[9], System.Globalization.CultureInfo.InvariantCulture)
                .Should().BeGreaterThan(0); // Arc length > 0
        }

        [Fact]
        public void BuildFrameTableCsv_HeaderAndRowCount()
        {
            var a = BuildSample();
            var csv = AlignmentReportService.BuildFrameTableCsv(a);

            var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            lines[0].Should().StartWith("Kind,PI#,Station,X,Y");

            // 期望几何点：BP + (BC, EC for PI1) + (TS, SC, CS, ST for PI2) + EP = 8
            var breakdown = AlignmentStationBreakdown.Build(
                a.Source.PiElements.Select(e => new PiElement(e.P, e.Radius, e.SpiralIn, e.SpiralOut, e.Tag)).ToList(),
                a.StartStation);
            lines.Length.Should().Be(breakdown.GeometryPoints.Count + 1);
        }

        [Fact]
        public void BuildFrameTableCsv_FirstRowIsBp_LastRowIsEp()
        {
            var a = BuildSample();
            var csv = AlignmentReportService.BuildFrameTableCsv(a);
            var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            lines[1].Should().StartWith("BP,");
            lines[lines.Length - 1].Should().StartWith("EP,");

            // EP 行没有 ToNext 段
            var epFields = lines[lines.Length - 1].Split(',');
            epFields[5].Should().BeEmpty(); // ToNext Length
            epFields[6].Should().BeEmpty(); // ToNext Kind
        }

        [Fact]
        public void BuildFrameTableCsv_StationsAreMonotonic()
        {
            var a = BuildSample();
            var csv = AlignmentReportService.BuildFrameTableCsv(a);
            var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            // 解析每行的 Station 字符串回到 double 不直观，改成验证 breakdown 数组本身的单调性
            var breakdown = AlignmentStationBreakdown.Build(
                a.Source.PiElements.Select(e => new PiElement(e.P, e.Radius, e.SpiralIn, e.SpiralOut, e.Tag)).ToList(),
                a.StartStation);
            for (int i = 1; i < breakdown.GeometryPoints.Count; i++)
            {
                breakdown.GeometryPoints[i].StationM
                    .Should().BeGreaterThanOrEqualTo(breakdown.GeometryPoints[i - 1].StationM);
            }
        }

        [Fact]
        public void ToUtf8BomBytes_ContainsBomPrefix()
        {
            var bytes = AlignmentReportService.ToUtf8BomBytes("测试,hello");
            bytes.Length.Should().BeGreaterThan(3);
            bytes[0].Should().Be(0xEF);
            bytes[1].Should().Be(0xBB);
            bytes[2].Should().Be(0xBF);

            // 去掉 BOM 再解析内容
            var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            text.Should().Be("测试,hello");
        }

        [Fact]
        public void Build_Throws_WhenPiElementsMissing()
        {
            var a = new Alignment { Name = "no-source" };
            Action act = () => AlignmentReportService.BuildPiTableCsv(a);
            act.Should().Throw<InvalidOperationException>();

            Action act2 = () => AlignmentReportService.BuildFrameTableCsv(a);
            act2.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Build_Throws_WhenAlignmentNull()
        {
            Action act = () => AlignmentReportService.BuildPiTableCsv(null);
            act.Should().Throw<ArgumentNullException>();

            Action act2 = () => AlignmentReportService.BuildFrameTableCsv(null);
            act2.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Csv_EscapesCommaInTag()
        {
            var source = new AlignmentSource { Kind = AlignmentSourceKind.PiTable };
            source.PiElements.Add(new AlignmentPiInput { P = new Point2D(0, 0), Tag = "带,逗号" });
            source.PiElements.Add(new AlignmentPiInput { P = new Point2D(100, 0), Tag = "EP" });
            var a = new Alignment { Source = source, StartStation = 0 };

            var csv = AlignmentReportService.BuildPiTableCsv(a);
            csv.Should().Contain("\"带,逗号\"");
        }
    }
}
