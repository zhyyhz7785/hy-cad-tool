using System.Collections.Generic;
using FluentAssertions;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.Services.Road
{
    /// <summary>M10.1 CorridorSweepService 测试。</summary>
    public class CorridorSweepServiceTests
    {
        private static Alignment MakeStraightAlignment(double length = 100)
        {
            var a = new Alignment
            {
                Name = "T",
                StartStation = 0,
                Centerline = new Polyline3D(
                    new[] { new Point3D(0, 0, 0), new Point3D(length, 0, 0) },
                    isClosed: false),
            };
            return a;
        }

        private static CrossSectionLayout MakeLayout()
        {
            var left = new List<CrossSectionBand>
            {
                CrossSectionBand.Lane(3.5, 1.5, BandSide.Left, "Lane"),
                CrossSectionBand.Sidewalk(2.0, 1.5, BandSide.Left, "SW"),
            };
            var right = new List<CrossSectionBand>
            {
                CrossSectionBand.Lane(3.5, 1.5, BandSide.Right, "Lane"),
                CrossSectionBand.Sidewalk(2.0, 1.5, BandSide.Right, "SW"),
            };
            return CrossSectionLayout.Create(left, right, 0, 50, 100, "T");
        }

        [Fact]
        public void Sweep_StraightAlignment_ProducesParallelRedLines()
        {
            var aln = MakeStraightAlignment(100);
            var layout = MakeLayout();

            var svc = new CorridorSweepService();
            var plan = svc.Sweep(aln, layout, samplingStepM: 10);

            plan.Should().NotBeNull();
            plan.AlignmentId.Should().Be(aln.Id);
            plan.CenterLine.Should().NotBeNull();
            plan.LeftRedLine.Should().NotBeNull();
            plan.RightRedLine.Should().NotBeNull();

            // 左红线应在 y = 5.5（3.5 + 2.0）
            double halfWidth = 3.5 + 2.0;
            plan.LeftRedLine.Vertices[0].Y.Should().BeApproximately(-halfWidth, 1e-6);
            plan.RightRedLine.Vertices[0].Y.Should().BeApproximately(halfWidth, 1e-6);

            // 分界线：1 个（左 1，右 1）
            plan.LeftBandDividers.Should().HaveCount(1);
            plan.RightBandDividers.Should().HaveCount(1);
            plan.LeftBandDividers[0].Vertices[0].Y.Should().BeApproximately(-3.5, 1e-6);
            plan.RightBandDividers[0].Vertices[0].Y.Should().BeApproximately(3.5, 1e-6);
        }

        [Fact]
        public void Sweep_NullAlignment_Throws()
        {
            var svc = new CorridorSweepService();
            System.Action act = () => svc.Sweep(null, MakeLayout());
            act.Should().Throw<System.ArgumentNullException>();
        }

        [Fact]
        public void Sweep_NullLayout_Throws()
        {
            var svc = new CorridorSweepService();
            System.Action act = () => svc.Sweep(MakeStraightAlignment(), null);
            act.Should().Throw<System.ArgumentNullException>();
        }

        [Fact]
        public void Sweep_TooFewVertices_Throws()
        {
            var aln = new Alignment
            {
                Centerline = new Polyline3D(new[] { new Point3D(0, 0, 0) }, isClosed: false),
            };
            var svc = new CorridorSweepService();
            System.Action act = () => svc.Sweep(aln, MakeLayout());
            act.Should().Throw<System.ArgumentException>();
        }

        [Fact]
        public void Sweep_NonPositiveStep_Throws()
        {
            var svc = new CorridorSweepService();
            System.Action act = () => svc.Sweep(MakeStraightAlignment(), MakeLayout(), samplingStepM: 0);
            act.Should().Throw<System.ArgumentOutOfRangeException>();
        }

        [Fact]
        public void SampleAlignmentUniformly_ReturnsStartAndEnd()
        {
            var p = new Polyline3D(new[] { new Point3D(0, 0, 0), new Point3D(20, 0, 0) }, isClosed: false);
            var samples = CorridorSweepService.SampleAlignmentUniformly(p, 5);
            samples.Should().HaveCountGreaterOrEqualTo(5); // 0, 5, 10, 15, 20 + 末端 补偿
            samples[0].Point.X.Should().BeApproximately(0, 1e-6);
            samples[samples.Count - 1].Point.X.Should().BeApproximately(20, 1e-6);
        }

        [Fact]
        public void SampleAlignmentUniformly_EmptyOrNullReturnsEmpty()
        {
            CorridorSweepService.SampleAlignmentUniformly(null, 5).Should().BeEmpty();
            var shortPoly = new Polyline3D(new[] { new Point3D(0, 0, 0) }, isClosed: false);
            CorridorSweepService.SampleAlignmentUniformly(shortPoly, 5).Should().BeEmpty();
        }

        [Fact]
        public void Sweep_WithMedianWidth_LeftRedLineOffset()
        {
            var aln = MakeStraightAlignment(50);
            var layout = CrossSectionLayout.Create(
                new List<CrossSectionBand> { CrossSectionBand.Lane(3.5, 1.5, BandSide.Left) }.AsReadOnly(),
                new List<CrossSectionBand> { CrossSectionBand.Lane(3.5, 1.5, BandSide.Right) }.AsReadOnly(),
                centerMedianWidth: 4.0,
                designSpeed: 50);

            var svc = new CorridorSweepService();
            var plan = svc.Sweep(aln, layout);
            // 分隔带 4m 半宽 2m，加 Lane 3.5m → 总 5.5m 偏移
            plan.LeftRedLine.Vertices[0].Y.Should().BeApproximately(-5.5, 1e-6);
            plan.RightRedLine.Vertices[0].Y.Should().BeApproximately(5.5, 1e-6);
        }
    }
}
