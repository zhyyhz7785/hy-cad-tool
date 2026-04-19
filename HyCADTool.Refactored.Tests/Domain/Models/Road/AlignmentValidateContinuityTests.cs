using FluentAssertions;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.Models.Road
{
    /// <summary>
    /// <see cref="Alignment.ValidateContinuity"/> 的扩展自检测试（A4 看板）。
    ///
    /// 覆盖维度：
    /// - 默认参数下直线通过；
    /// - PI 偏转角：背折 / 几乎共线 PI；
    /// - <see cref="AlignmentElement"/>：长度 / 圆曲线半径 / 缓和曲线 A 参数；
    /// - <see cref="Alignment.Elements"/> 桩号链：倒序 / 断链 / 末端与中心线一致性；
    /// - 旧 <see cref="Alignment.Validate"/> 入口向后兼容（转发到 ValidateContinuity）。
    /// </summary>
    public class AlignmentValidateContinuityTests
    {
        private static Alignment BuildStraight(double length = 1000)
        {
            var a = new Alignment { Name = "测试线", StartStation = 0 };
            a.Centerline.AddVertex(new Point3D(0, 0, 0));
            a.Centerline.AddVertex(new Point3D(length, 0, 0));
            return a;
        }

        // ============================== PI 偏转角 ==============================

        [Fact]
        public void ValidateContinuity_StraightLine_DefaultOptions_Passes()
        {
            var a = BuildStraight(500);

            var r = a.ValidateContinuity();

            r.Ok.Should().BeTrue();
            r.Errors.Should().BeEmpty();
        }

        [Fact]
        public void ValidateContinuity_PiBackFold_ReportsBackFoldError()
        {
            // PI[1] 完全反向折回：(0,0) → (100,0) → (0,0)，偏转角 ≈ 180°
            var a = new Alignment { Name = "背折测试" };
            a.Centerline.AddVertex(new Point3D(0, 0, 0));
            a.Centerline.AddVertex(new Point3D(100, 0, 0));
            a.Centerline.AddVertex(new Point3D(0, 0, 0));

            var r = a.ValidateContinuity();

            r.Ok.Should().BeFalse();
            r.Errors.Should().Contain(e => e.Contains("PI[1]") && e.Contains("超过最大允许"));
        }

        [Fact]
        public void ValidateContinuity_PiAlmostCollinear_ReportsCollinearWarning()
        {
            // PI[1] 偏转角 ≈ 0.1°（远小于默认 1°）
            // 第二段 (100,0)→(200, 0.001)：偏转角 = atan(0.001/100) * 180 / π ≈ 0.000573°
            var a = new Alignment { Name = "共线 PI 测试" };
            a.Centerline.AddVertex(new Point3D(0, 0, 0));
            a.Centerline.AddVertex(new Point3D(100, 0, 0));
            a.Centerline.AddVertex(new Point3D(200, 0.001, 0));

            var r = a.ValidateContinuity();

            r.Ok.Should().BeFalse();
            r.Errors.Should().Contain(e => e.Contains("PI[1]") && e.Contains("可考虑省略"));
        }

        [Fact]
        public void ValidateContinuity_PiAt45Degrees_DoesNotReportPiAngleError()
        {
            // 标准 45° PI（折角不背折也不共线）
            var a = new Alignment { Name = "标准 PI" };
            a.Centerline.AddVertex(new Point3D(0, 0, 0));
            a.Centerline.AddVertex(new Point3D(100, 0, 0));
            a.Centerline.AddVertex(new Point3D(200, 100, 0)); // 45° 偏转

            var r = a.ValidateContinuity();

            r.Ok.Should().BeTrue();
            r.Errors.Should().BeEmpty();
        }

        // ============================== Elements 字段 ==============================

        [Fact]
        public void ValidateContinuity_ElementWithZeroLength_ReportsLengthError()
        {
            var a = BuildStraight();
            a.Elements.Add(new AlignmentElement
            {
                Kind = AlignmentElementKind.Line,
                StartStation = 0,
                Length = 0, // 非法
            });

            var r = a.ValidateContinuity();

            r.Ok.Should().BeFalse();
            r.Errors.Should().Contain(e => e.Contains("Element[0]") && e.Contains("Length"));
        }

        [Fact]
        public void ValidateContinuity_SpiralWithoutAParameter_ReportsSpiralError()
        {
            var a = BuildStraight();
            a.Elements.Add(new AlignmentElement
            {
                Kind = AlignmentElementKind.Spiral,
                StartStation = 0,
                Length = 1000,
                SpiralParameterA = double.NaN, // 缓和曲线必须有有效 A
            });

            var r = a.ValidateContinuity();

            r.Ok.Should().BeFalse();
            r.Errors.Should().Contain(e => e.Contains("Spiral") && e.Contains("SpiralParameterA"));
        }

        [Fact]
        public void ValidateContinuity_ArcWithoutRadius_ReportsArcError()
        {
            var a = BuildStraight();
            a.Elements.Add(new AlignmentElement
            {
                Kind = AlignmentElementKind.CircularArc,
                StartStation = 0,
                Length = 1000,
                Radius = 0, // 非法
            });

            var r = a.ValidateContinuity();

            r.Ok.Should().BeFalse();
            r.Errors.Should().Contain(e => e.Contains("CircularArc") && e.Contains("Radius"));
        }

        // ============================== Elements 桩号链 ==============================

        [Fact]
        public void ValidateContinuity_ElementsStationOutOfOrder_ReportsOrderError()
        {
            // 第 1 段 [1000, 1500]，第 2 段 StartStation=500（< prev.StartStation=1000）→ 倒序
            var a = BuildStraight(2000);
            a.Elements.Add(new AlignmentElement { Kind = AlignmentElementKind.Line, StartStation = 1000, Length = 500 });
            a.Elements.Add(new AlignmentElement { Kind = AlignmentElementKind.Line, StartStation = 500, Length = 500 });

            var r = a.ValidateContinuity();

            r.Ok.Should().BeFalse();
            r.Errors.Should().Contain(e => e.Contains("Element[1]") && e.Contains("单调升序"));
        }

        [Fact]
        public void ValidateContinuity_ElementsStationDiscontinuous_ReportsContinuityError()
        {
            // 第 1 段 EndStation = 500，第 2 段 StartStation = 600（断 100m）
            var a = BuildStraight(2000);
            a.Elements.Add(new AlignmentElement { Kind = AlignmentElementKind.Line, StartStation = 0, Length = 500 });
            a.Elements.Add(new AlignmentElement { Kind = AlignmentElementKind.Line, StartStation = 600, Length = 1400 });

            var r = a.ValidateContinuity();

            r.Ok.Should().BeFalse();
            r.Errors.Should().Contain(e => e.Contains("Element[1]") && e.Contains("不连续"));
        }

        [Fact]
        public void ValidateContinuity_ElementsAccumulatedEndMismatchCenterline_ReportsTotalMismatchError()
        {
            // Centerline 1000 m，但 Elements 累计只到 800 m
            var a = BuildStraight(1000);
            a.Elements.Add(new AlignmentElement { Kind = AlignmentElementKind.Line, StartStation = 0, Length = 500 });
            a.Elements.Add(new AlignmentElement { Kind = AlignmentElementKind.Line, StartStation = 500, Length = 300 });

            var r = a.ValidateContinuity();

            r.Ok.Should().BeFalse();
            r.Errors.Should().Contain(e => e.Contains("Elements 累计末端桩号") && e.Contains("不一致"));
        }

        [Fact]
        public void ValidateContinuity_ElementsConsistentWithCenterline_Passes()
        {
            // Centerline 1000 m，Elements 累计正好 1000 m，无桩号错误
            var a = BuildStraight(1000);
            a.Elements.Add(new AlignmentElement { Kind = AlignmentElementKind.Line, StartStation = 0, Length = 400 });
            a.Elements.Add(new AlignmentElement { Kind = AlignmentElementKind.Line, StartStation = 400, Length = 600 });

            var r = a.ValidateContinuity();

            r.Ok.Should().BeTrue();
            r.Errors.Should().BeEmpty();
        }

        // ============================== 自定义 Options ==============================

        [Fact]
        public void ValidateContinuity_CustomMaxPiDeflection_ReportsErrorWhenExceeded()
        {
            // 90° PI（默认 175° 容许，自定义 60° 容许 → 应报错）
            var a = new Alignment { Name = "自定义阈值" };
            a.Centerline.AddVertex(new Point3D(0, 0, 0));
            a.Centerline.AddVertex(new Point3D(100, 0, 0));
            a.Centerline.AddVertex(new Point3D(100, 100, 0)); // 90° 偏转

            var defaultOk = a.ValidateContinuity();
            defaultOk.Ok.Should().BeTrue("90° 偏转默认允许");

            var strict = a.ValidateContinuity(new AlignmentValidationOptions { MaxPiDeflectionDeg = 60 });
            strict.Ok.Should().BeFalse();
            strict.Errors.Should().Contain(e => e.Contains("超过最大允许 60.00°"));
        }

        // ============================== 旧 Validate API 兼容 ==============================

        [Fact]
        public void Validate_LegacySignature_DelegatesToValidateContinuity()
        {
            // 直线场景：旧 API 与新 API 应都通过
            var a = BuildStraight();
            a.Validate().Ok.Should().BeTrue();
            a.ValidateContinuity().Ok.Should().BeTrue();
        }

        [Fact]
        public void Validate_LegacySignature_StillReportsBasicErrors()
        {
            // 单顶点场景：保持旧 API 的"中心线顶点不足"错误信息不变
            var a = new Alignment { Name = "退化" };
            a.Centerline.AddVertex(new Point3D(0, 0, 0));

            var r = a.Validate();

            r.Ok.Should().BeFalse();
            r.Errors.Should().Contain(e => e.Contains("中心线顶点不足 2 个"));
        }
    }
}
