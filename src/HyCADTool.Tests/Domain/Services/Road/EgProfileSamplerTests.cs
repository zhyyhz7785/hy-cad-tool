using System;
using System.Linq;
using FluentAssertions;
using HyCADTool.Features.Road.PlanProfile.Domain;
using HyCAD.Geometry;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>
    /// <see cref="EgProfileSampler"/> 单元测试。
    ///
    /// 覆盖维度：
    /// - 水平直线 + 平面 / 倾斜 / 起伏地面 → 高程线性插值；
    /// - 中心线全部 / 部分超出地面线横向范围 → 截断行为；
    /// - 步长与 IncludeEnd 选项；
    /// - 参数级别异常（null / 顶点不足 / interval ≤ 0）。
    /// </summary>
    public class EgProfileSamplerTests
    {
        // ============================== 工具方法 ==============================

        /// <summary>沿 +X 方向的水平 centerline，从 (0,0,0) 到 (length,0,0)。</summary>
        private static Polyline3D BuildEastwardCenterline(double length)
        {
            var p = new Polyline3D();
            p.AddVertex(new Point3D(0, 0, 0));
            p.AddVertex(new Point3D(length, 0, 0));
            return p;
        }

        /// <summary>构造一条沿 +X 方向、Y=offset、Z 线性插值的地面线。</summary>
        private static Polyline3D BuildEastwardGround(double xStart, double xEnd, double y, double zStart, double zEnd)
        {
            var p = new Polyline3D();
            p.AddVertex(new Point3D(xStart, y, zStart));
            p.AddVertex(new Point3D(xEnd, y, zEnd));
            return p;
        }

        // ============================== 基础采样 ==============================

        [Fact]
        public void Sample_FlatGround_AllElevationsEqual()
        {
            var c = BuildEastwardCenterline(100);
            var g = BuildEastwardGround(-50, 150, y: 0, zStart: 12.34, zEnd: 12.34);

            var r = EgProfileSampler.Sample(c, g, new EgProfileSampler.Options { IntervalM = 25, IncludeEnd = true });

            r.Vertices.Should().NotBeEmpty();
            r.Vertices.Select(v => v.Elevation).Should().AllBeEquivalentTo(12.34);
            r.SkippedOutOfRange.Should().Be(0);
            r.MaxLateralOffsetSeen.Should().BeApproximately(0, 1e-9);
        }

        [Fact]
        public void Sample_LinearSlopedGround_LinearlyInterpolatedZ()
        {
            // 地面线：x ∈ [0, 100]，y=0，Z 从 0 升到 10 → 斜率 0.1
            var c = BuildEastwardCenterline(100);
            var g = BuildEastwardGround(0, 100, y: 0, zStart: 0, zEnd: 10);

            var r = EgProfileSampler.Sample(c, g, new EgProfileSampler.Options { IntervalM = 25, IncludeEnd = true });

            r.Vertices.Count.Should().Be(5); // K0+0, +25, +50, +75, +100（IncludeEnd 不重复 100，因为 100 已经是步长样点）
            r.Vertices[0].Station.Should().BeApproximately(0, 1e-9);
            r.Vertices[0].Elevation.Should().BeApproximately(0, 1e-9);
            r.Vertices[1].Elevation.Should().BeApproximately(2.5, 1e-9);
            r.Vertices[2].Elevation.Should().BeApproximately(5.0, 1e-9);
            r.Vertices[4].Elevation.Should().BeApproximately(10.0, 1e-9);
        }

        [Fact]
        public void Sample_RollingGround_TakesNearestSegmentZ()
        {
            // 起伏地面（V 形）：(0, 0, 0) → (50, 0, 10) → (100, 0, 0)
            var c = BuildEastwardCenterline(100);
            var g = new Polyline3D();
            g.AddVertex(new Point3D(0, 0, 0));
            g.AddVertex(new Point3D(50, 0, 10));
            g.AddVertex(new Point3D(100, 0, 0));

            var r = EgProfileSampler.Sample(c, g, new EgProfileSampler.Options { IntervalM = 25, IncludeEnd = true });

            r.Vertices.Count.Should().Be(5);
            r.Vertices[0].Elevation.Should().BeApproximately(0, 1e-9);   // K0+0
            r.Vertices[1].Elevation.Should().BeApproximately(5, 1e-9);   // K0+25 段 1 中部
            r.Vertices[2].Elevation.Should().BeApproximately(10, 1e-9);  // K0+50 顶
            r.Vertices[3].Elevation.Should().BeApproximately(5, 1e-9);   // K0+75 段 2 中部
            r.Vertices[4].Elevation.Should().BeApproximately(0, 1e-9);   // K0+100
        }

        [Fact]
        public void Sample_GroundOffsetLaterally_ZUnaffected_LateralOffsetMeasured()
        {
            // 地面线整体平移到 y=10（横向 10m 偏移）；centerline 仍在 y=0
            var c = BuildEastwardCenterline(100);
            var g = BuildEastwardGround(0, 100, y: 10, zStart: 5, zEnd: 5);

            var r = EgProfileSampler.Sample(c, g, new EgProfileSampler.Options { IntervalM = 50, IncludeEnd = true });

            r.Vertices.Should().NotBeEmpty();
            r.Vertices.Select(v => v.Elevation).Should().AllBeEquivalentTo(5);
            r.MaxLateralOffsetSeen.Should().BeApproximately(10, 1e-9);
            r.SkippedOutOfRange.Should().Be(0); // 默认 1000m 不会触发截断
        }

        // ============================== 截断 ==============================

        [Fact]
        public void Sample_GroundOutOfMaxLateralOffset_AllSkipped()
        {
            // 地面线 y=100，但允许偏移 5m → 全部跳过
            var c = BuildEastwardCenterline(100);
            var g = BuildEastwardGround(0, 100, y: 100, zStart: 5, zEnd: 5);

            var r = EgProfileSampler.Sample(c, g, new EgProfileSampler.Options { IntervalM = 25, MaxLateralOffsetM = 5 });

            r.Vertices.Should().BeEmpty();
            r.SkippedOutOfRange.Should().BeGreaterThan(0);
            r.MaxLateralOffsetSeen.Should().BeApproximately(100, 1e-9);
        }

        [Fact]
        public void Sample_PartialCoverage_OnlyInRangeKept()
        {
            // 地面线只覆盖 x ∈ [-50, 50]：超过 x=50 后，最近段终点在 (50, 0, 5)，距离按 (x-50, 0) 计算
            // centerline x ∈ [0, 100]，步长 25 → 5 个样点
            // x=0,25,50 在覆盖内（lateralOffset=0）；x=75,100 距离最近端点 (50,0) 分别为 25 和 50
            var c = BuildEastwardCenterline(100);
            var g = BuildEastwardGround(-50, 50, y: 0, zStart: 0, zEnd: 5);

            var r = EgProfileSampler.Sample(c, g, new EgProfileSampler.Options
            {
                IntervalM = 25,
                MaxLateralOffsetM = 30, // 25 ≤ 30 但 50 > 30 → 跳过最后 1 个
            });

            r.Vertices.Count.Should().Be(4);
            r.SkippedOutOfRange.Should().Be(1);
            // 最后一个保留点是 K0+75，最近端 (50,0,5)，所以 elevation = 5
            r.Vertices.Last().Station.Should().BeApproximately(75, 1e-9);
            r.Vertices.Last().Elevation.Should().BeApproximately(5, 1e-9);
        }

        // ============================== 步长 / IncludeEnd ==============================

        [Fact]
        public void Sample_IncludeEndFalse_SkipsExtraEndSample()
        {
            // 长度 100m，步长 30 → 桩号 0, 30, 60, 90；IncludeEnd=false 不追加 100
            var c = BuildEastwardCenterline(100);
            var g = BuildEastwardGround(-50, 150, y: 0, zStart: 0, zEnd: 200); // 平均斜率 1

            var r = EgProfileSampler.Sample(c, g, new EgProfileSampler.Options { IntervalM = 30, IncludeEnd = false });

            r.Vertices.Select(v => v.Station).Should().BeEquivalentTo(new[] { 0.0, 30.0, 60.0, 90.0 });
        }

        [Fact]
        public void Sample_IncludeEndTrue_AppendsExactEndSample()
        {
            var c = BuildEastwardCenterline(100);
            var g = BuildEastwardGround(-50, 150, y: 0, zStart: 0, zEnd: 200);

            var r = EgProfileSampler.Sample(c, g, new EgProfileSampler.Options { IntervalM = 30, IncludeEnd = true });

            // IncludeEnd=true 在末尾追加 station=100
            r.Vertices.Last().Station.Should().BeApproximately(100, 1e-9);
        }

        [Fact]
        public void Sample_AllVertices_HaveCurveRadiusZero()
        {
            // EG 不含竖曲线
            var c = BuildEastwardCenterline(100);
            var g = BuildEastwardGround(0, 100, y: 0, zStart: 0, zEnd: 10);

            var r = EgProfileSampler.Sample(c, g, new EgProfileSampler.Options { IntervalM = 25 });

            r.Vertices.Select(v => v.CurveRadius).Should().AllBeEquivalentTo(0.0);
        }

        // ============================== 参数校验 ==============================

        [Fact]
        public void Sample_NullCenterline_Throws()
        {
            var g = BuildEastwardGround(0, 100, 0, 0, 10);
            Action act = () => EgProfileSampler.Sample(null, g);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Sample_NullGroundLine_Throws()
        {
            var c = BuildEastwardCenterline(100);
            Action act = () => EgProfileSampler.Sample(c, null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Sample_CenterlineSinglePoint_Throws()
        {
            var c = new Polyline3D();
            c.AddVertex(new Point3D(0, 0, 0));
            var g = BuildEastwardGround(0, 100, 0, 0, 10);

            Action act = () => EgProfileSampler.Sample(c, g);
            act.Should().Throw<ArgumentException>().WithMessage("*centerline*");
        }

        [Fact]
        public void Sample_GroundSinglePoint_Throws()
        {
            var c = BuildEastwardCenterline(100);
            var g = new Polyline3D();
            g.AddVertex(new Point3D(0, 0, 0));

            Action act = () => EgProfileSampler.Sample(c, g);
            act.Should().Throw<ArgumentException>().WithMessage("*groundLine*");
        }

        [Fact]
        public void Sample_NonPositiveInterval_Throws()
        {
            var c = BuildEastwardCenterline(100);
            var g = BuildEastwardGround(0, 100, 0, 0, 10);

            Action act = () => EgProfileSampler.Sample(c, g, new EgProfileSampler.Options { IntervalM = 0 });
            act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}
