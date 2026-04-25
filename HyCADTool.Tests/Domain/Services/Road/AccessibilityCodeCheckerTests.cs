using FluentAssertions;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using System.Collections.Generic;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>
    /// P3-I1-C：<see cref="AccessibilityCodeChecker"/> 单测 —— 覆盖 GB 50763 §3.2 / §3.3 核心条款。
    /// </summary>
    public class AccessibilityCodeCheckerTests
    {
        private static CurbRamp MakeRamp(
            CurbRampKind kind = CurbRampKind.SingleFace,
            double width = CurbRamp.DefaultWidth,
            double depth = CurbRamp.DefaultDepth,
            double slope = CurbRamp.DefaultSlope)
            => new CurbRamp(
                cornerArcIndex: 0,
                kind: kind,
                frontCenter: new Point2D(0, 0),
                tangent: new Vector2D(1, 0),
                outwardNormal: new Vector2D(0, 1),
                width: width,
                depth: depth,
                slope: slope);

        // ============================== CurbRamp Width ==============================

        [Theory]
        [InlineData(CurbRampKind.SingleFace, 1.5, true)]
        [InlineData(CurbRampKind.SingleFace, 1.49, false)]
        [InlineData(CurbRampKind.ThreeFace, 1.2, true)]
        [InlineData(CurbRampKind.ThreeFace, 1.19, false)]
        [InlineData(CurbRampKind.Fan, 1.5, true)]
        [InlineData(CurbRampKind.Fan, 1.0, false)]
        public void CheckCurbRampWidth_RespectsMinPerKind(CurbRampKind kind, double width, bool expectedPass)
        {
            var ramp = MakeRamp(kind: kind, width: width);
            AccessibilityCodeChecker.CheckCurbRampWidth(ramp).Pass.Should().Be(expectedPass);
        }

        // ============================== CurbRamp Slope ==============================

        [Theory]
        [InlineData(1.0 / 12.0, true)]      // 边界等于上限：通过
        [InlineData(1.0 / 20.0, true)]      // 1:20 更缓：通过
        [InlineData(1.0 / 10.0, false)]     // 1:10 比 1:12 陡：失败
        [InlineData(1.0 / 8.0, false)]
        public void CheckCurbRampSlope_MaxOneOverTwelve(double slope, bool expectedPass)
        {
            var ramp = MakeRamp(slope: slope);
            AccessibilityCodeChecker.CheckCurbRampSlope(ramp).Pass.Should().Be(expectedPass);
        }

        // ============================== TactilePaving Width ==============================

        [Theory]
        [InlineData(TactilePavingKind.Advance, 0.30, true)]
        [InlineData(TactilePavingKind.Advance, 0.25, true)]
        [InlineData(TactilePavingKind.Advance, 0.50, true)]
        [InlineData(TactilePavingKind.Advance, 0.20, false)]  // 下限之外
        [InlineData(TactilePavingKind.Advance, 0.60, false)]  // 上限之外
        [InlineData(TactilePavingKind.Stop, 0.60, true)]
        [InlineData(TactilePavingKind.Stop, 0.30, true)]
        [InlineData(TactilePavingKind.Stop, 0.25, false)]
        [InlineData(TactilePavingKind.Stop, 0.70, false)]
        public void CheckTactilePavingWidth_RespectsKindRange(TactilePavingKind kind, double width, bool expectedPass)
        {
            var paving = new TactilePaving
            {
                Kind = kind,
                Width = width,
                Centerline = new List<Point2D>
                {
                    new Point2D(0, 0),
                    new Point2D(5, 0),
                },
            };
            AccessibilityCodeChecker.CheckTactilePavingWidth(paving).Pass.Should().Be(expectedPass);
        }

        // ============================== 消息格式 ==============================

        [Fact]
        public void FailResult_CarriesRuleTagAndExpectedValue()
        {
            var ramp = MakeRamp(kind: CurbRampKind.SingleFace, width: 1.0);
            var r = AccessibilityCodeChecker.CheckCurbRampWidth(ramp);
            r.Pass.Should().BeFalse();
            r.RuleTag.Should().Contain("GB50763");
            r.Expected.Should().Be(AccessibilityCodeChecker.MinSingleFaceWidth);
            r.Message.Should().Contain("1.50");
        }
    }
}
