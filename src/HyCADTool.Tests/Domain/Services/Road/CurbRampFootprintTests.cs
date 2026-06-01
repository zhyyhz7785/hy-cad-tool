using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCAD.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>
    /// v1.1 —— <see cref="CurbRampDesigner.BuildFootprint"/> 纯几何分类单测。
    ///
    /// <para>覆盖三种 <see cref="CurbRampKind"/>：
    /// <list type="bullet">
    /// <item><see cref="CurbRampKind.SingleFace"/>：1 条闭合矩形（4 顶点）；</item>
    /// <item><see cref="CurbRampKind.ThreeFace"/>：3 条闭合多边形（主坡矩形 + 左/右侧三角坡各 3 顶点）；</item>
    /// <item><see cref="CurbRampKind.Fan"/>：1 条闭合扇环（外弧 + 内弧镶嵌 N 段直线）。</item>
    /// </list></para>
    ///
    /// <para>所有断言均为<b>几何不变量</b>（顶点数 / 面积 / 在圆上 / 对称性），不绑定具体坐标，避免随 `IntersectionDesigner`
    /// 未来小幅调整而脆化。</para>
    /// </summary>
    public class CurbRampFootprintTests
    {
        private static Alignment MakeStraight(Point2D start, Point2D end, string name = null)
        {
            var cl = new Polyline3D(
                new[] { new Point3D(start.X, start.Y, 0), new Point3D(end.X, end.Y, 0) },
                isClosed: false,
                bulges: new[] { 0.0, 0.0 });
            return new Alignment
            {
                Name = name ?? $"{start}->{end}",
                StartStation = 0,
                Centerline = cl,
            };
        }

        private static Intersection MakeCross(double r = 8, double hw = 7.5)
        {
            var als = new List<Alignment>
            {
                MakeStraight(new Point2D(-100, 0), new Point2D(0, 0), "W"),
                MakeStraight(new Point2D(100, 0), new Point2D(0, 0), "E"),
                MakeStraight(new Point2D(0, 100), new Point2D(0, 0), "N"),
                MakeStraight(new Point2D(0, -100), new Point2D(0, 0), "S"),
            };
            return IntersectionDesigner.ComputeFromAlignments(als, new Point2D(0, 0), r, hw);
        }

        private static CurbRamp MakeRamp(Intersection ix, int arcIndex, CurbRampKind kind,
            double width = CurbRamp.DefaultWidth, double depth = CurbRamp.DefaultDepth)
        {
            CurbRampDesigner.TryBuildRampOnArc(ix.CornerArcs[arcIndex], arcIndex, kind, width, depth,
                CurbRamp.DefaultSlope, out var ramp).Should().BeTrue();
            return ramp;
        }

        // =================================== SingleFace ===================================

        [Fact]
        public void SingleFace_Produces_OneClosedRectangle_With_FourVertices()
        {
            var ix = MakeCross();
            var ramp = MakeRamp(ix, 0, CurbRampKind.SingleFace);
            var footprints = CurbRampDesigner.BuildFootprint(ramp);

            footprints.Should().HaveCount(1);
            footprints[0].IsClosed.Should().BeTrue();
            footprints[0].VertexCount.Should().Be(4);
        }

        [Fact]
        public void SingleFace_Area_Equals_WidthTimesDepth()
        {
            var ix = MakeCross();
            var ramp = MakeRamp(ix, 0, CurbRampKind.SingleFace, width: 1.5, depth: 1.8);
            var footprint = CurbRampDesigner.BuildFootprint(ramp)[0];
            Math.Abs(footprint.GetSignedArea()).Should().BeApproximately(1.5 * 1.8, 1e-6);
        }

        [Fact]
        public void SingleFace_Vertices_AreInOrder_FrontLeft_FrontRight_BackRight_BackLeft()
        {
            var ix = MakeCross();
            var ramp = MakeRamp(ix, 0, CurbRampKind.SingleFace);
            var pl = CurbRampDesigner.BuildFootprint(ramp)[0];

            pl.GetPointAt(0).IsEqualTo(ramp.FrontLeft, 1e-9).Should().BeTrue();
            pl.GetPointAt(1).IsEqualTo(ramp.FrontRight, 1e-9).Should().BeTrue();
            pl.GetPointAt(2).IsEqualTo(ramp.BackRight, 1e-9).Should().BeTrue();
            pl.GetPointAt(3).IsEqualTo(ramp.BackLeft, 1e-9).Should().BeTrue();
        }

        // =================================== ThreeFace ===================================

        [Fact]
        public void ThreeFace_Produces_ThreeClosedPolygons()
        {
            var ix = MakeCross();
            var ramp = MakeRamp(ix, 0, CurbRampKind.ThreeFace);
            var footprints = CurbRampDesigner.BuildFootprint(ramp);

            footprints.Should().HaveCount(3);
            footprints.All(p => p.IsClosed).Should().BeTrue();
        }

        [Fact]
        public void ThreeFace_FirstIsMainRect_OthersAreTriangles()
        {
            var ix = MakeCross();
            var ramp = MakeRamp(ix, 0, CurbRampKind.ThreeFace);
            var fs = CurbRampDesigner.BuildFootprint(ramp);

            fs[0].VertexCount.Should().Be(4, "第一块是主坡矩形");
            fs[1].VertexCount.Should().Be(3, "第二块是左侧三角坡");
            fs[2].VertexCount.Should().Be(3, "第三块是右侧三角坡");
        }

        [Fact]
        public void ThreeFace_LeftTriangle_SharesEdge_With_MainRect_Left()
        {
            var ix = MakeCross();
            var ramp = MakeRamp(ix, 0, CurbRampKind.ThreeFace);
            var fs = CurbRampDesigner.BuildFootprint(ramp);

            // 左侧三角包含 FrontLeft 与 BackLeft 两个顶点
            fs[1].Vertices.Any(v => v.IsEqualTo(ramp.FrontLeft, 1e-9)).Should().BeTrue();
            fs[1].Vertices.Any(v => v.IsEqualTo(ramp.BackLeft, 1e-9)).Should().BeTrue();
        }

        [Fact]
        public void ThreeFace_RightTriangle_SharesEdge_With_MainRect_Right()
        {
            var ix = MakeCross();
            var ramp = MakeRamp(ix, 0, CurbRampKind.ThreeFace);
            var fs = CurbRampDesigner.BuildFootprint(ramp);

            fs[2].Vertices.Any(v => v.IsEqualTo(ramp.FrontRight, 1e-9)).Should().BeTrue();
            fs[2].Vertices.Any(v => v.IsEqualTo(ramp.BackRight, 1e-9)).Should().BeTrue();
        }

        [Fact]
        public void ThreeFace_SideTriangle_ApexLiesAlongTangent_AtSideLength()
        {
            var ix = MakeCross();
            var ramp = MakeRamp(ix, 0, CurbRampKind.ThreeFace);
            double sl = 1.2;
            var fs = CurbRampDesigner.BuildFootprint(ramp, sideLength: sl);

            // 左侧三角的"外顶点"应位于 FrontLeft - Tangent·sl
            var expectedLeftApex = ramp.FrontLeft.Add(ramp.Tangent * -sl);
            fs[1].Vertices.Any(v => v.IsEqualTo(expectedLeftApex, 1e-9)).Should().BeTrue();

            var expectedRightApex = ramp.FrontRight.Add(ramp.Tangent * sl);
            fs[2].Vertices.Any(v => v.IsEqualTo(expectedRightApex, 1e-9)).Should().BeTrue();
        }

        // =================================== Fan ===================================

        [Fact]
        public void Fan_Produces_OneClosedFootprint()
        {
            var ix = MakeCross();
            var ramp = MakeRamp(ix, 0, CurbRampKind.Fan, depth: 1.5);
            var fs = CurbRampDesigner.BuildFootprint(ramp, ix.CornerArcs[0]);

            fs.Should().HaveCount(1);
            fs[0].IsClosed.Should().BeTrue();
        }

        [Fact]
        public void Fan_VertexCount_Equals_2_Times_Segments_Plus_2()
        {
            var ix = MakeCross();
            var ramp = MakeRamp(ix, 0, CurbRampKind.Fan);
            int N = 16;
            var fs = CurbRampDesigner.BuildFootprint(ramp, ix.CornerArcs[0], tesselationSegments: N);

            fs[0].VertexCount.Should().Be(2 * (N + 1),
                "扇环 = (N+1) 外弧顶点 + (N+1) 内弧顶点（反向），共 2(N+1)");
        }

        [Fact]
        public void Fan_OuterArc_Vertices_LieOn_CornerArcCircle()
        {
            var ix = MakeCross();
            var arc = ix.CornerArcs[0];
            var ramp = MakeRamp(ix, 0, CurbRampKind.Fan);
            int N = 8;
            var pl = CurbRampDesigner.BuildFootprint(ramp, arc, tesselationSegments: N)[0];

            for (int i = 0; i <= N; i++)
            {
                pl.GetPointAt(i).DistanceTo(arc.Center)
                    .Should().BeApproximately(arc.Radius, 1e-6,
                        $"外弧第 {i} 顶点应在 CornerArc 圆周上");
            }
        }

        [Fact]
        public void Fan_InnerArc_Vertices_LieOn_InnerCircle_RadiusMinusDepth()
        {
            var ix = MakeCross();
            var arc = ix.CornerArcs[0];
            double depth = 2.0;
            var ramp = MakeRamp(ix, 0, CurbRampKind.Fan, depth: depth);
            int N = 8;
            var pl = CurbRampDesigner.BuildFootprint(ramp, arc, tesselationSegments: N)[0];

            for (int i = N + 1; i < pl.VertexCount; i++)
            {
                pl.GetPointAt(i).DistanceTo(arc.Center)
                    .Should().BeApproximately(arc.Radius - depth, 1e-6,
                        $"内弧第 {i - (N + 1)} 顶点应在 R-Depth 同心圆上");
            }
        }

        [Fact]
        public void Fan_DepthExceedsRadius_FallsBackTo_SingleFaceRectangle()
        {
            var ix = MakeCross(r: 2);  // 小 R
            var arc = ix.CornerArcs[0];
            var ramp = MakeRamp(ix, 0, CurbRampKind.Fan, depth: 5.0);  // Depth > R
            var fs = CurbRampDesigner.BuildFootprint(ramp, arc);

            fs.Should().HaveCount(1);
            fs[0].VertexCount.Should().Be(4, "扇环退化为 SingleFace 矩形（4 顶点）");
        }

        [Fact]
        public void Fan_WithoutArc_FallsBackTo_SingleFaceRectangle()
        {
            var ix = MakeCross();
            var ramp = MakeRamp(ix, 0, CurbRampKind.Fan);
            var fs = CurbRampDesigner.BuildFootprint(ramp, arc: null);

            fs.Should().HaveCount(1);
            fs[0].VertexCount.Should().Be(4);
        }

        // =================================== 边界 / 合约 ===================================

        [Fact]
        public void BuildFootprint_ThrowsOn_NonPositive_SideLength()
        {
            var ix = MakeCross();
            var ramp = MakeRamp(ix, 0, CurbRampKind.ThreeFace);
            ((Action)(() => CurbRampDesigner.BuildFootprint(ramp, sideLength: 0)))
                .Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void BuildFootprint_ThrowsOn_Tesselation_LessThan2()
        {
            var ix = MakeCross();
            var ramp = MakeRamp(ix, 0, CurbRampKind.Fan);
            ((Action)(() => CurbRampDesigner.BuildFootprint(ramp, ix.CornerArcs[0], tesselationSegments: 1)))
                .Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}
