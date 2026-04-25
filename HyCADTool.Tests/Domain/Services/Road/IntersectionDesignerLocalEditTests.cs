using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>
    /// v1.1：<see cref="IntersectionDesigner.TryRebuildCornerArc"/> +
    /// <see cref="IntersectionDesigner.UpdateLegHalfWidth"/> 的局部编辑单测。
    ///
    /// <para><b>验收要点</b></para>
    /// <list type="bullet">
    /// <item>修改单个 CornerArc 半径 → 只改该弧，其他弧位置 / 半径不变；</item>
    /// <item>修改单 Leg 半宽 → 该 Leg 相邻的 2 条弧重算，其他弧 <b>Center + Radius</b> 不变；</item>
    /// <item>非法索引 / 非法值 → 抛 <see cref="ArgumentOutOfRangeException"/> 或 <see cref="ArgumentException"/>。</item>
    /// </list>
    /// </summary>
    public class IntersectionDesignerLocalEditTests
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

        private static Intersection MakeCross(double r = 20, double hw = 7.5)
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

        // ============================== TryRebuildCornerArc ==============================

        [Fact]
        public void TryRebuildCornerArc_UpdatesOnlyTargetedIndex()
        {
            var ix = MakeCross(r: 20);
            ix.CornerArcs.Should().HaveCountGreaterOrEqualTo(2);

            // 快照：拷其他弧用于比对
            var before = ix.CornerArcs.Select(a => (a.Center, a.Radius, a.StartPoint, a.EndPoint)).ToList();

            var ok = IntersectionDesigner.TryRebuildCornerArc(ix, 0, newRadius: 30);
            ok.Should().BeTrue();

            ix.CornerArcs[0].Radius.Should().BeApproximately(30, 1e-9);

            for (int i = 1; i < ix.CornerArcs.Count; i++)
            {
                ix.CornerArcs[i].Radius.Should().BeApproximately(before[i].Radius, 1e-9);
                ix.CornerArcs[i].Center.IsEqualTo(before[i].Center, 1e-9).Should().BeTrue();
            }
        }

        [Fact]
        public void TryRebuildCornerArc_PreservesLegMapping()
        {
            var ix = MakeCross();
            var oldArc = ix.CornerArcs[1];

            IntersectionDesigner.TryRebuildCornerArc(ix, 1, 25);

            ix.CornerArcs[1].LegIndexA.Should().Be(oldArc.LegIndexA);
            ix.CornerArcs[1].LegIndexB.Should().Be(oldArc.LegIndexB);
        }

        [Fact]
        public void TryRebuildCornerArc_Throws_OnOutOfRangeIndex()
        {
            var ix = MakeCross();
            ((Action)(() => IntersectionDesigner.TryRebuildCornerArc(ix, -1, 20)))
                .Should().Throw<ArgumentOutOfRangeException>();
            ((Action)(() => IntersectionDesigner.TryRebuildCornerArc(ix, 999, 20)))
                .Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void TryRebuildCornerArc_Throws_OnNonPositiveRadius()
        {
            var ix = MakeCross();
            ((Action)(() => IntersectionDesigner.TryRebuildCornerArc(ix, 0, 0)))
                .Should().Throw<ArgumentOutOfRangeException>();
            ((Action)(() => IntersectionDesigner.TryRebuildCornerArc(ix, 0, -5)))
                .Should().Throw<ArgumentOutOfRangeException>();
        }

        // ============================== UpdateLegHalfWidth ==============================

        [Fact]
        public void UpdateLegHalfWidth_UpdatesLegStructAndReturnsTouchedArcIndices()
        {
            var ix = MakeCross(hw: 7.5);
            ix.Legs[0].HalfWidth.Should().BeApproximately(7.5, 1e-9);

            var touched = IntersectionDesigner.UpdateLegHalfWidth(ix, 0, 10.0);

            ix.Legs[0].HalfWidth.Should().BeApproximately(10.0, 1e-9);
            touched.Count.Should().Be(2, "十字口中每条 Leg 都被两条 CornerArc 共享");

            foreach (var idx in touched)
            {
                var arc = ix.CornerArcs[idx];
                (arc.LegIndexA == 0 || arc.LegIndexB == 0).Should().BeTrue();
            }
        }

        [Fact]
        public void UpdateLegHalfWidth_DoesNotChangeUnrelatedArcs()
        {
            var ix = MakeCross(hw: 7.5);

            var before = ix.CornerArcs.Select(a => (a.Center, a.Radius)).ToList();
            var touched = IntersectionDesigner.UpdateLegHalfWidth(ix, 0, 10.0);

            for (int i = 0; i < ix.CornerArcs.Count; i++)
            {
                if (touched.Contains(i)) continue;
                ix.CornerArcs[i].Radius.Should().BeApproximately(before[i].Radius, 1e-9);
                ix.CornerArcs[i].Center.IsEqualTo(before[i].Center, 1e-9).Should().BeTrue();
            }
        }

        [Fact]
        public void UpdateLegHalfWidth_PreservesRadii_ForTouchedArcs()
        {
            var ix = MakeCross(r: 20, hw: 7.5);
            var touched = IntersectionDesigner.UpdateLegHalfWidth(ix, 0, 10.0);

            // 半宽改变影响圆心位置，但半径应保持（局部重建时用原 Radius）
            foreach (var idx in touched)
            {
                ix.CornerArcs[idx].Radius.Should().BeApproximately(20, 1e-9);
            }
        }

        [Fact]
        public void UpdateLegHalfWidth_Throws_OnOutOfRange()
        {
            var ix = MakeCross();
            ((Action)(() => IntersectionDesigner.UpdateLegHalfWidth(ix, -1, 7.5)))
                .Should().Throw<ArgumentOutOfRangeException>();
            ((Action)(() => IntersectionDesigner.UpdateLegHalfWidth(ix, 99, 7.5)))
                .Should().Throw<ArgumentOutOfRangeException>();
            ((Action)(() => IntersectionDesigner.UpdateLegHalfWidth(ix, 0, 0)))
                .Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Null_Intersection_Throws()
        {
            ((Action)(() => IntersectionDesigner.TryRebuildCornerArc(null, 0, 10)))
                .Should().Throw<ArgumentNullException>();
            ((Action)(() => IntersectionDesigner.UpdateLegHalfWidth(null, 0, 10)))
                .Should().Throw<ArgumentNullException>();
        }
    }
}
