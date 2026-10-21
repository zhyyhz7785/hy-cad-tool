using System;
using System.Collections.Generic;
using System.Linq;
using HyCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain;
using HyCADTool.Features.Reinforcement.Domain.Components;
using Xunit;

namespace HyCADTool.LinuxTests
{
    /// <summary>
    /// RegionPartitioner 四阶段割线的黄金结果。几何单位 mm。
    /// 只锁当前分解，不改算法。
    /// </summary>
    public class RegionPartitionerTests
    {
        [Theory]
        [InlineData(0, PartitionEdgeClass.HorizontalA)]
        [InlineData(4, PartitionEdgeClass.HorizontalA)]
        [InlineData(6, PartitionEdgeClass.SlabCa)]
        [InlineData(44, PartitionEdgeClass.SlabCa)]
        [InlineData(84, PartitionEdgeClass.WallCb)]
        [InlineData(86, PartitionEdgeClass.VerticalB)]
        [InlineData(90, PartitionEdgeClass.VerticalB)]
        [InlineData(180, PartitionEdgeClass.HorizontalA)]
        public void Edge_class_follows_angle_bands(double degrees, PartitionEdgeClass expected)
        {
            Assert.Equal(expected, RegionPartitioner.ClassifyEdge(LineAt(degrees)));
        }

        [Fact]
        public void Exact_45_degree_diagonal_is_a_wall_edge()
        {
            // 分界是 angle < 45° 归板。dx=dy 时 Atan2 得到 45°，归斜墙。
            var seg = new Line2D(new Point2D(0, 0), new Point2D(100, 100));
            Assert.Equal(PartitionEdgeClass.WallCb, RegionPartitioner.ClassifyEdge(seg));
        }

        [Fact]
        public void Zero_length_edge_is_slab()
        {
            var seg = new Line2D(new Point2D(1, 1), new Point2D(1, 1));
            Assert.Equal(PartitionEdgeClass.SlabCa, RegionPartitioner.ClassifyEdge(seg));
        }

        [Fact]
        public void Null_short_and_hairline_regions_partition_to_nothing()
        {
            Assert.Empty(RegionPartitioner.Partition(null));

            var twoPoints = Region((0, 0), (10, 0));
            Assert.Empty(RegionPartitioner.Partition(twoPoints));

            // 条带宽 0.4mm < MinStripWidthMm 0.5，条带被跳过。
            var hairline = Region((0, 0), (0.4, 0), (0.4, 10), (0, 10));
            Assert.Empty(RegionPartitioner.Partition(hairline));
        }

        [Fact]
        public void Grounded_thin_rectangle_is_one_bottom_slab()
        {
            var region = Region((0, 0), (4000, 0), (4000, 200), (0, 200));

            var parts = RegionPartitioner.Partition(region);

            AssertTypes(parts, ComponentType.BottomSlab);
            AssertBox(parts[0], 0, 0, 4000, 200);
            AssertNear(parts[0].ThicknessMm, 200);
            AssertNear(parts[0].Polygon.GetArea(), 4000 * 200, 1);
        }

        [Fact]
        public void Same_thin_rectangle_above_ground_is_a_slab()
        {
            var region = Region((0, 0), (4000, 0), (4000, 200), (0, 200));

            var parts = RegionPartitioner.Partition(region, groundY: -10000);

            AssertTypes(parts, ComponentType.Slab);
            AssertBox(parts[0], 0, 0, 4000, 200);
            AssertNear(parts[0].ThicknessMm, 200);
            AssertNear(parts[0].Polygon.GetArea(), 4000 * 200, 1);
        }

        [Theory]
        [InlineData(0, ComponentType.BottomSlab)]
        [InlineData(1500, ComponentType.BottomSlab)]
        [InlineData(1600, ComponentType.Slab)]
        public void Ground_gate_compares_region_bottom_with_ground_plus_1500(double bottomY, ComponentType expected)
        {
            // 落地：区域最低 Y ≤ groundY + 底板厚度上限。groundY=0 时，底在 1500 仍落地，1600 起悬空。
            var region = Region(
                (0, bottomY),
                (2000, bottomY),
                (2000, bottomY + 200),
                (0, bottomY + 200));
            var parts = RegionPartitioner.Partition(region, groundY: 0);

            AssertTypes(parts, expected);
        }

        [Theory]
        [InlineData(-1, ComponentType.Slab)]
        [InlineData(0, ComponentType.BottomSlab)]
        public void Soil_cut_overrides_the_ground_gate(double soilCutY, ComponentType expected)
        {
            var region = Region((0, 0), (2000, 0), (2000, 200), (0, 200));
            var parts = RegionPartitioner.Partition(region, groundY: -10000, soilCutY: soilCutY);

            AssertTypes(parts, expected);
        }

        [Fact]
        public void Narrow_stem_on_a_footing_anchors_the_wall_down_to_the_column_bottom()
        {
            // 底板 y=0..400 全宽；墙 x=1800..2200（宽 400）升到 y=3000。
            // N8 量宽：墙带下方夹层 < 1000，墙多边形延伸到列底 y=0，与底板重叠。
            var region = Region(
                (0, 0), (4000, 0), (4000, 400),
                (2200, 400), (2200, 3000), (1800, 3000), (1800, 400),
                (0, 400));

            var parts = RegionPartitioner.Partition(region);

            AssertTypes(parts, ComponentType.Wall, ComponentType.BottomSlab);

            var wall = parts[0];
            var footing = parts[1];
            AssertBox(wall, 1800, 0, 2200, 3000);
            AssertNear(wall.ThicknessMm, 400);
            AssertNear(wall.Polygon.GetArea(), 400 * 3000, 1);
            AssertBox(footing, 0, 0, 4000, 400);
            AssertNear(footing.ThicknessMm, 400);
            AssertNear(footing.Polygon.GetArea(), 4000 * 400, 1);

            AssertCovered(parts, ComponentType.Wall, 2000, 2000);
            AssertCovered(parts, ComponentType.Wall, 2000, 100);
            AssertCovered(parts, ComponentType.BottomSlab, 2000, 100);
            AssertCovered(parts, ComponentType.BottomSlab, 200, 200);
            AssertNotCovered(parts, ComponentType.Wall, 200, 200);
            AssertOutside(parts, 2000, 3100);
        }

        [Fact]
        public void Vertical_edge_walls_keep_the_stem_above_the_footing()
        {
            var region = Region(
                (0, 0), (4000, 0), (4000, 400),
                (2200, 400), (2200, 3000), (1800, 3000), (1800, 400),
                (0, 400));
            var detected = new List<WallColumnRect>();

            var parts = RegionPartitioner.Partition(
                region,
                useVerticalEdgeWalls: true,
                detectedWallColumns: detected);

            AssertTypes(parts, ComponentType.Wall, ComponentType.BottomSlab);
            Assert.Single(detected);
            AssertNear(detected[0].X0, 1800);
            AssertNear(detected[0].X1, 2200);
            AssertNear(detected[0].YBot, 400);
            AssertNear(detected[0].YTop, 3000);
            AssertNear(detected[0].WidthMm, 400);
            AssertNear(detected[0].HeightMm, 2600);

            AssertBox(parts[0], 1800, 400, 2200, 3000);
            AssertNear(parts[0].ThicknessMm, 400);
            AssertCovered(parts, ComponentType.Wall, 2000, 2000);
            AssertNotCovered(parts, ComponentType.Wall, 2000, 100);
            AssertCovered(parts, ComponentType.BottomSlab, 2000, 100);
        }

        [Fact]
        public void Wide_block_on_a_footing_is_mass_concrete_above_the_cut()
        {
            // 底板 y=0..400；宽块 x=1000..4000（宽 3000）升到 y=2500。
            var region = Region(
                (0, 0), (6000, 0), (6000, 400),
                (4000, 400), (4000, 2500), (1000, 2500), (1000, 400),
                (0, 400));

            var parts = RegionPartitioner.Partition(region);

            AssertTypes(parts, ComponentType.MassConcrete, ComponentType.BottomSlab);
            AssertBox(parts[0], 1000, 400, 4000, 2500);
            AssertNear(parts[0].ThicknessMm, 2100);
            AssertNear(parts[0].Polygon.GetArea(), 3000 * 2100, 1);
            AssertBox(parts[1], 0, 0, 6000, 400);
            AssertNear(parts[1].ThicknessMm, 400);

            AssertCovered(parts, ComponentType.MassConcrete, 2500, 1000);
            AssertNotCovered(parts, ComponentType.MassConcrete, 2500, 200);
            AssertCovered(parts, ComponentType.BottomSlab, 500, 200);
            AssertOutside(parts, 500, 1000);
        }

        [Fact]
        public void Hanging_beam_between_footing_and_slab_splits_into_four_roles()
        {
            // 左墙 x=0..300 把底板和顶板连成一块；梁 x=2500..3300（宽 800）悬在 y=1800..3000，梁下是空腔。
            var region = SectionWithDrop(2500, 3300);
            Assert.True(region.IsValidRebarPoint(new Point2D(2900, 2400)));
            Assert.False(region.IsValidRebarPoint(new Point2D(2900, 1000)));

            var parts = RegionPartitioner.Partition(region);

            AssertTypes(parts, ComponentType.Wall, ComponentType.Slab, ComponentType.Beam, ComponentType.BottomSlab);

            AssertBox(Find(parts, ComponentType.Wall), 0, 0, 300, 3300);
            AssertNear(Find(parts, ComponentType.Wall).ThicknessMm, 300);
            AssertNear(Find(parts, ComponentType.Wall).Polygon.GetArea(), 300 * 3300, 1);

            AssertBox(Find(parts, ComponentType.Slab), 0, 3000, 6000, 3300);
            AssertNear(Find(parts, ComponentType.Slab).ThicknessMm, 300);
            AssertNear(Find(parts, ComponentType.Slab).Polygon.GetArea(), 6000 * 300, 1);

            AssertBox(Find(parts, ComponentType.Beam), 2500, 1800, 3300, 3000);
            // 梁的 ThicknessMm 记 X 向宽度，不记梁高 1200。
            AssertNear(Find(parts, ComponentType.Beam).ThicknessMm, 800);
            AssertNear(Find(parts, ComponentType.Beam).Polygon.GetArea(), 800 * 1200, 1);

            AssertBox(Find(parts, ComponentType.BottomSlab), 0, 0, 6000, 400);
            AssertNear(Find(parts, ComponentType.BottomSlab).ThicknessMm, 400);

            AssertOutside(parts, 2900, 1000);
            AssertCovered(parts, ComponentType.Beam, 2900, 2400);
            AssertNotCovered(parts, ComponentType.MassConcrete, 2900, 2400);
        }

        [Fact]
        public void Drop_wider_than_beam_max_becomes_two_mass_pieces_and_anchor_tongues()
        {
            // 下凸宽 1200 > 梁宽上限 800。板带中段归大体积，左右各留 500mm 锚固舌片仍是板。
            var region = SectionWithDrop(2400, 3600);

            var parts = RegionPartitioner.Partition(region);

            Assert.Equal(1, parts.Count(p => p.Type == ComponentType.Wall));
            Assert.Equal(2, parts.Count(p => p.Type == ComponentType.Slab));
            Assert.Equal(2, parts.Count(p => p.Type == ComponentType.MassConcrete));
            Assert.Equal(1, parts.Count(p => p.Type == ComponentType.BottomSlab));
            Assert.DoesNotContain(parts, p => p.Type == ComponentType.Beam);

            AssertBox(FindCovering(parts, ComponentType.MassConcrete, 3000, 2400), 2400, 1800, 3600, 3000);
            AssertBox(FindCovering(parts, ComponentType.MassConcrete, 3000, 3150), 2400, 3000, 3600, 3300);
            AssertNotCovered(parts, ComponentType.Slab, 3000, 3150);
            AssertCovered(parts, ComponentType.Slab, 2600, 3150);
            AssertCovered(parts, ComponentType.Slab, 3400, 3150);
            AssertCovered(parts, ComponentType.Slab, 1000, 3150);
            AssertCovered(parts, ComponentType.Wall, 150, 2000);
            AssertCovered(parts, ComponentType.BottomSlab, 3000, 200);
            AssertOutside(parts, 3000, 1000);
        }

        [Fact]
        public void Hole_stays_empty_and_concrete_area_is_preserved()
        {
            var outer = new[] { (0d, 0d), (4000d, 0d), (4000d, 200d), (0d, 200d) };
            var hole = new[] { (1500d, 50d), (2500d, 50d), (2500d, 150d), (1500d, 150d) };
            var region = RegionWithHole(outer, hole);
            Assert.False(region.IsValidRebarPoint(new Point2D(2000, 100)));

            var parts = RegionPartitioner.Partition(region);

            AssertTypes(parts, ComponentType.Slab, ComponentType.BottomSlab);
            AssertBox(parts[0], 1500, 150, 2500, 200);
            AssertNear(parts[0].ThicknessMm, 50);
            AssertNear(parts[0].Polygon.GetArea(), 1000 * 50, 1);

            AssertBox(parts[1], 0, 0, 4000, 200);
            AssertNear(parts[1].ThicknessMm, 150);
            AssertNear(parts[1].Polygon.GetArea(), 1500 * 200 + 1000 * 50 + 1500 * 200, 1);
            AssertOutside(parts, 2000, 100);
            AssertCovered(parts, ComponentType.Slab, 2000, 175);
            AssertCovered(parts, ComponentType.BottomSlab, 2000, 25);
            AssertCovered(parts, ComponentType.BottomSlab, 500, 100);

            double concrete = parts.Sum(p => p.Polygon.GetArea());
            AssertNear(concrete, 4000 * 200 - 1000 * 100, 1);
        }

        [Fact]
        public void Short_bump_merges_into_the_bottom_slab_or_splits_off()
        {
            // 高 1000，底板切到 400，上面 600 < 局部阈值 1000。
            var region = Region((0, 0), (3000, 0), (3000, 1000), (0, 1000));

            var merged = RegionPartitioner.Partition(
                region,
                bottomSlabMaxHeightMm: 400,
                mergeBumpsIntoBottomSlab: true);
            AssertTypes(merged, ComponentType.BottomSlab);
            AssertBox(merged[0], 0, 0, 3000, 1000);
            AssertNear(merged[0].ThicknessMm, 1000);

            var split = RegionPartitioner.Partition(
                region,
                bottomSlabMaxHeightMm: 400,
                mergeBumpsIntoBottomSlab: false);
            AssertTypes(split, ComponentType.LocalConcrete, ComponentType.BottomSlab);
            AssertBox(split[0], 0, 400, 3000, 1000);
            AssertNear(split[0].ThicknessMm, 600);
            AssertBox(split[1], 0, 0, 3000, 400);
            AssertNear(split[1].ThicknessMm, 400);
        }

        [Fact]
        public void Sub_millimetre_thresholds_fall_back_to_defaults()
        {
            var region = Region((0, 0), (3000, 0), (3000, 1000), (0, 1000));

            var resetCut = RegionPartitioner.Partition(region, bottomSlabMaxHeightMm: 0);
            var defaultCut = RegionPartitioner.Partition(region, bottomSlabMaxHeightMm: 1500);
            AssertTypes(resetCut, ComponentType.BottomSlab);
            AssertTypes(defaultCut, ComponentType.BottomSlab);
            AssertNear(resetCut[0].ThicknessMm, defaultCut[0].ThicknessMm);
            AssertBox(resetCut[0], 0, 0, 3000, 1000);

            var resetLocal = RegionPartitioner.Partition(
                region,
                bottomSlabMaxHeightMm: 400,
                localConcreteMaxHeightMm: 0,
                mergeBumpsIntoBottomSlab: false);
            var explicitLocal = RegionPartitioner.Partition(
                region,
                bottomSlabMaxHeightMm: 400,
                localConcreteMaxHeightMm: 1000,
                mergeBumpsIntoBottomSlab: false);
            AssertTypes(resetLocal, ComponentType.LocalConcrete, ComponentType.BottomSlab);
            AssertNear(resetLocal[0].ThicknessMm, explicitLocal[0].ThicknessMm);
            AssertNear(resetLocal[0].ThicknessMm, 600);
        }

        [Fact]
        public void Floating_shapes_become_wall_mass_or_local_concrete()
        {
            var wall = RegionPartitioner.Partition(
                Region((0, 0), (300, 0), (300, 3000), (0, 3000)),
                groundY: -10000);
            AssertTypes(wall, ComponentType.Wall);
            AssertBox(wall[0], 0, 0, 300, 3000);
            AssertNear(wall[0].ThicknessMm, 300);

            var mass = RegionPartitioner.Partition(
                Region((0, 0), (2000, 0), (2000, 2000), (0, 2000)),
                groundY: -10000);
            AssertTypes(mass, ComponentType.MassConcrete);
            AssertBox(mass[0], 0, 0, 2000, 2000);
            AssertNear(mass[0].ThicknessMm, 2000);

            // 厚 600 > 板厚上限 300，高 600 < 局部阈值 1000 → 局部混凝土，不进墙。
            var local = RegionPartitioner.Partition(
                Region((0, 0), (800, 0), (800, 600), (0, 600)),
                groundY: -10000);
            AssertTypes(local, ComponentType.LocalConcrete);
            AssertBox(local[0], 0, 0, 800, 600);
            AssertNear(local[0].ThicknessMm, 600);
        }

        private static ReinRegion SectionWithDrop(double dropX0, double dropX1)
        {
            return Region(
                (0, 0),
                (6000, 0),
                (6000, 400),
                (300, 400),
                (300, 3000),
                (dropX0, 3000),
                (dropX0, 1800),
                (dropX1, 1800),
                (dropX1, 3000),
                (6000, 3000),
                (6000, 3300),
                (0, 3300));
        }

        private static ReinRegion Region(params (double X, double Y)[] points)
        {
            return new ReinRegion(new Polyline2D(ToPoints(points), true), null);
        }

        private static ReinRegion RegionWithHole(
            (double X, double Y)[] outer,
            (double X, double Y)[] hole)
        {
            return new ReinRegion(
                new Polyline2D(ToPoints(outer), true),
                new[] { new Polyline2D(ToPoints(hole), true) });
        }

        private static IEnumerable<Point2D> ToPoints(IEnumerable<(double X, double Y)> points)
        {
            return points.Select(p => new Point2D(p.X, p.Y));
        }

        private static Line2D LineAt(double degrees)
        {
            double rad = degrees * Math.PI / 180.0;
            return new Line2D(
                new Point2D(0, 0),
                new Point2D(100 * Math.Cos(rad), 100 * Math.Sin(rad)));
        }

        private static void AssertTypes(IReadOnlyList<PartitionRegion> parts, params ComponentType[] expected)
        {
            var actual = parts.Select(p => p.Type).ToArray();
            Assert.True(
                expected.SequenceEqual(actual),
                "期望 [" + string.Join(", ", expected) + "] 实际 " + Summary(parts));
        }

        private static PartitionRegion Find(IReadOnlyList<PartitionRegion> parts, ComponentType type)
        {
            var hits = parts.Where(p => p.Type == type).ToList();
            Assert.True(hits.Count == 1, type + " 应恰好一块；实际 " + Summary(parts));
            return hits[0];
        }

        private static PartitionRegion FindCovering(
            IReadOnlyList<PartitionRegion> parts,
            ComponentType type,
            double x,
            double y)
        {
            var hit = parts.FirstOrDefault(p => p.Type == type && p.Polygon.ContainsPoint(new Point2D(x, y)));
            Assert.True(hit != null, type + " 应盖住 (" + x + "," + y + ")；实际 " + Summary(parts));
            return hit;
        }

        private static void AssertCovered(
            IReadOnlyList<PartitionRegion> parts,
            ComponentType type,
            double x,
            double y)
        {
            FindCovering(parts, type, x, y);
        }

        private static void AssertNotCovered(
            IReadOnlyList<PartitionRegion> parts,
            ComponentType type,
            double x,
            double y)
        {
            bool hit = parts.Any(p => p.Type == type && p.Polygon.ContainsPoint(new Point2D(x, y)));
            Assert.False(hit, type + " 不应盖住 (" + x + "," + y + ")；实际 " + Summary(parts));
        }

        private static void AssertOutside(IReadOnlyList<PartitionRegion> parts, double x, double y)
        {
            var point = new Point2D(x, y);
            bool hit = parts.Any(p => p.Polygon.ContainsPoint(point));
            Assert.False(hit, "(" + x + "," + y + ") 应在分区外；实际 " + Summary(parts));
        }

        private static void AssertBox(
            PartitionRegion region,
            double x0,
            double y0,
            double x1,
            double y1)
        {
            var box = region.Polygon.GetBoundingBox();
            AssertNear(box.MinPoint.X, x0);
            AssertNear(box.MinPoint.Y, y0);
            AssertNear(box.MaxPoint.X, x1);
            AssertNear(box.MaxPoint.Y, y1);
        }

        private static void AssertNear(double actual, double expected, double tolerance = 0.5)
        {
            Assert.True(
                Math.Abs(actual - expected) <= tolerance,
                "期望 " + expected + " ± " + tolerance + "，实际 " + actual);
        }

        private static string Summary(IReadOnlyList<PartitionRegion> parts)
        {
            if (parts.Count == 0)
                return "(空)";

            return string.Join(" | ", parts.Select(p =>
            {
                var box = p.Polygon.GetBoundingBox();
                return p.Type
                    + " t=" + p.ThicknessMm.ToString("0.##")
                    + " [" + box.MinPoint.X.ToString("0.##") + "," + box.MinPoint.Y.ToString("0.##")
                    + "]-[" + box.MaxPoint.X.ToString("0.##") + "," + box.MaxPoint.Y.ToString("0.##") + "]"
                    + " a=" + p.Polygon.GetArea().ToString("0.##");
            }));
        }
    }
}
