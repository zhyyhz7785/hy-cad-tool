using System;
using FluentAssertions;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.ValueObjects.Geometry
{
    public class Polyline3DTests
    {
        [Fact]
        public void EmptyPolyline_HasZeroSegmentsAndZeroLength()
        {
            var p = new Polyline3D();
            p.VertexCount.Should().Be(0);
            p.SegmentCount.Should().Be(0);
            p.GetTotalLength().Should().Be(0);
            p.GetPlanarLength().Should().Be(0);
            p.IsClosed.Should().BeFalse();
        }

        [Fact]
        public void OpenPolyline_SegmentCount_EqualsVerticesMinusOne()
        {
            var p = new Polyline3D(new[]
            {
                new Point3D(0, 0, 0),
                new Point3D(3, 0, 0),
                new Point3D(3, 4, 0)
            });

            p.VertexCount.Should().Be(3);
            p.SegmentCount.Should().Be(2);
            p.GetTotalLength().Should().BeApproximately(3 + 4, 1e-9);
            p.GetPlanarLength().Should().BeApproximately(3 + 4, 1e-9);
        }

        [Fact]
        public void ClosedPolyline_SegmentCount_EqualsVertices()
        {
            var p = new Polyline3D(new[]
            {
                new Point3D(0, 0, 0),
                new Point3D(3, 0, 0),
                new Point3D(3, 4, 0)
            }, isClosed: true);

            p.VertexCount.Should().Be(3);
            p.SegmentCount.Should().Be(3);
            p.GetTotalLength().Should().BeApproximately(3 + 4 + 5, 1e-9);
        }

        [Fact]
        public void AddVertex_AppendsAtEnd()
        {
            var p = new Polyline3D();
            p.AddVertex(new Point3D(0, 0, 0));
            p.AddVertex(new Point3D(1, 0, 0));

            p.VertexCount.Should().Be(2);
            p.GetPointAt(1).Should().Be(new Point3D(1, 0, 0));
        }

        [Fact]
        public void AddVertexAt_OutOfRange_Throws()
        {
            var p = new Polyline3D();
            p.AddVertex(new Point3D(0, 0, 0));

            Action act = () => p.AddVertexAt(-1, new Point3D(1, 0, 0));
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Clone_ReturnsIndependentCopy()
        {
            var p = new Polyline3D(new[] { new Point3D(0, 0, 0), new Point3D(1, 0, 0) });
            var clone = p.Clone();

            clone.VertexCount.Should().Be(2);
            clone.AddVertex(new Point3D(2, 0, 0));

            p.VertexCount.Should().Be(2, "原对象不应受 Clone 影响");
            clone.VertexCount.Should().Be(3);
        }

        [Fact]
        public void PlanarLength_IgnoresZComponent()
        {
            var p = new Polyline3D(new[]
            {
                new Point3D(0, 0, 0),
                new Point3D(0, 0, 10)
            });

            p.GetTotalLength().Should().BeApproximately(10, 1e-9);
            p.GetPlanarLength().Should().Be(0, "仅 Z 变化 → 投影长度为 0");
        }

        [Fact]
        public void Ctor_WithNullVertices_Throws()
        {
            Action act = () => new Polyline3D(null, true);
            act.Should().Throw<ArgumentNullException>();
        }

        // ===== P1.b：弧段 / bulge 相关 =====

        [Fact]
        public void NewPolyline_WithoutExplicitBulges_HasAllZeroBulgesAndNoArcs()
        {
            var p = new Polyline3D(new[]
            {
                new Point3D(0, 0, 0),
                new Point3D(10, 0, 0),
                new Point3D(20, 0, 0),
            });

            p.Bulges.Should().HaveCount(3, "Bulges 长度恒等于 VertexCount");
            p.Bulges.Should().AllBeEquivalentTo(0.0);
            p.HasArcs.Should().BeFalse();
        }

        [Fact]
        public void Ctor_MismatchedBulgeCount_IsPaddedOrTrimmed()
        {
            // 少了：自动补 0
            var padded = new Polyline3D(
                vertices: new[] { new Point3D(0, 0, 0), new Point3D(1, 0, 0), new Point3D(2, 0, 0) },
                isClosed: false,
                bulges: new[] { 0.5 });
            padded.Bulges.Should().Equal(new[] { 0.5, 0.0, 0.0 });

            // 多了：截断到 VertexCount
            var trimmed = new Polyline3D(
                vertices: new[] { new Point3D(0, 0, 0), new Point3D(1, 0, 0) },
                isClosed: false,
                bulges: new[] { 0.3, 0.4, 0.5, 0.6 });
            trimmed.Bulges.Should().Equal(new[] { 0.3, 0.4 });
        }

        [Fact]
        public void AddVertex_WithBulge_GrowsBothArrays()
        {
            var p = new Polyline3D();
            p.AddVertex(new Point3D(0, 0, 0), bulge: 1.0);
            p.AddVertex(new Point3D(10, 0, 0));

            p.VertexCount.Should().Be(2);
            p.Bulges.Should().Equal(new[] { 1.0, 0.0 });
            p.HasArcs.Should().BeTrue();
        }

        [Fact]
        public void GetPlanarLength_WithSemicircleBulge_EqualsPiR()
        {
            // bulge = 1 ⇒ θ = 4·atan(1) = π；弦长 = 2R；弧长 = πR
            // 取 R = 5，弦端点 (0,0)-(10,0)，期望弧长 = 5π ≈ 15.707963
            var p = new Polyline3D(
                vertices: new[] { new Point3D(0, 0, 0), new Point3D(10, 0, 0) },
                isClosed: false,
                bulges: new[] { 1.0, 0.0 });

            p.GetPlanarLength().Should().BeApproximately(Math.PI * 5.0, 1e-9);
        }

        [Fact]
        public void GetPlanarLength_WithQuarterCircleBulge_EqualsQuarterArcLength()
        {
            // 四分之一圆：θ = π/2 ⇒ bulge = tan(π/8) ≈ 0.41421356
            // 弦端点 (0,0)-(10,0)，弦长 = 2R·sin(π/4) = R·√2 ⇒ R = 10/√2 = 5√2
            // 弧长 = R·θ = 5√2·π/2 ≈ 11.107
            double bulge = Math.Tan(Math.PI / 8.0);
            double expectedR = 10.0 / Math.Sqrt(2.0);
            double expectedArc = expectedR * (Math.PI / 2.0);

            var p = new Polyline3D(
                vertices: new[] { new Point3D(0, 0, 0), new Point3D(10, 0, 0) },
                isClosed: false,
                bulges: new[] { bulge, 0.0 });

            p.GetPlanarLength().Should().BeApproximately(expectedArc, 1e-9);
        }

        [Fact]
        public void GetTotalLength_WithArcAndElevation_UsesApproximation()
        {
            // bulge = 1（半圆），弦 XY = 10，ΔZ = 3
            // arcXY = 5π；期望 L ≈ √((5π)² + 9)
            double bulge = 1.0;
            double expected = Math.Sqrt(Math.Pow(Math.PI * 5.0, 2) + 9.0);

            var p = new Polyline3D(
                vertices: new[] { new Point3D(0, 0, 0), new Point3D(10, 0, 3) },
                isClosed: false,
                bulges: new[] { bulge, 0.0 });

            p.GetTotalLength().Should().BeApproximately(expected, 1e-9);
        }

        [Fact]
        public void Clone_PreservesBulges()
        {
            var p = new Polyline3D(
                vertices: new[] { new Point3D(0, 0, 0), new Point3D(10, 0, 0) },
                isClosed: false,
                bulges: new[] { 0.5, 0.0 });

            var clone = p.Clone();
            clone.Bulges.Should().Equal(p.Bulges);
            clone.HasArcs.Should().BeTrue();

            // 独立性：改 clone 不影响原对象
            clone.AddVertex(new Point3D(20, 0, 0), bulge: 0.3);
            p.VertexCount.Should().Be(2);
            p.Bulges.Should().HaveCount(2);
        }

        [Fact]
        public void ShouldSerializeBulges_OnlyTrueWhenArcsExist()
        {
            var straight = new Polyline3D(new[] { new Point3D(0, 0, 0), new Point3D(1, 0, 0) });
            straight.ShouldSerializeBulges().Should().BeFalse();

            var curved = new Polyline3D(
                vertices: new[] { new Point3D(0, 0, 0), new Point3D(1, 0, 0) },
                isClosed: false,
                bulges: new[] { 0.25, 0.0 });
            curved.ShouldSerializeBulges().Should().BeTrue();
        }
    }
}
