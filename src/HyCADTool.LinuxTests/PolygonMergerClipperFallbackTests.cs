using HyCAD.Geometry;
using HyCADTool.Features.BaseRein.Domain.Services;
using Xunit;

namespace HyCADTool.LinuxTests
{
    public class PolygonMergerClipperFallbackTests
    {
        private readonly PolygonMerger _merger = new PolygonMerger(1e-6);

        [Fact]
        public void PrimaryPathFailure_EdgeTouchingL_IsClipperUnionNotBoundingRect()
        {
            // 两矩形只共边，Weiler-Atherton 找不到交点后抛错。
            var bottom = Rect(0, 0, 4, 2);
            var upright = Rect(2, 2, 4, 6);

            var union = _merger.UnionPolygons(new[] { bottom, upright });

            Assert.Single(union.Regions);
            Assert.Empty(union.Regions[0].Holes);
            Assert.Equal(16, union.Regions[0].NetArea, 6);
            AssertRing(union.Regions[0].Outer,
                new Point2D(0, 0),
                new Point2D(4, 0),
                new Point2D(4, 6),
                new Point2D(2, 6),
                new Point2D(2, 2),
                new Point2D(0, 2));
        }

        [Fact]
        public void Fallback_DisjointSquares_ReturnsBothComponents()
        {
            var left = Rect(0, 0, 1, 1);
            var right = Rect(3, 3, 4, 4);

            var union = _merger.UnionPolygons(new[] { left, right });

            Assert.Equal(2, union.Regions.Count);
            Assert.Equal(2, union.Regions[0].NetArea + union.Regions[1].NetArea, 6);
            Assert.Empty(union.Regions[0].Holes);
            Assert.Empty(union.Regions[1].Holes);
            AssertRing(union.Regions[0].Outer,
                new Point2D(0, 0),
                new Point2D(1, 0),
                new Point2D(1, 1),
                new Point2D(0, 1));
            AssertRing(union.Regions[1].Outer,
                new Point2D(3, 3),
                new Point2D(4, 3),
                new Point2D(4, 4),
                new Point2D(3, 4));
            Assert.False(union.Regions[0].Outer.ContainsPoint(new Point2D(2, 2)));
            Assert.False(union.Regions[1].Outer.ContainsPoint(new Point2D(2, 2)));
        }

        [Fact]
        public void Fallback_FrameWithTwoHoles_KeepsBothHoles()
        {
            // 五块实心矩形围出两个孔。底条和顶条不相交，主路径抛错后走 Clipper2。
            var pieces = new[]
            {
                Rect(0, 0, 12, 1),
                Rect(0, 5, 12, 6),
                Rect(0, 1, 1, 5),
                Rect(11, 1, 12, 5),
                Rect(5, 1, 7, 5)
            };

            var union = _merger.UnionPolygons(pieces);

            Assert.Single(union.Regions);
            var region = union.Regions[0];
            Assert.Equal(2, region.Holes.Count);
            Assert.Equal(72, region.Outer.GetArea(), 6);
            Assert.Equal(40, region.NetArea, 6);
            AssertRing(region.Outer,
                new Point2D(0, 0),
                new Point2D(12, 0),
                new Point2D(12, 6),
                new Point2D(0, 6));
            AssertRing(region.Holes[0],
                new Point2D(1, 1),
                new Point2D(1, 5),
                new Point2D(5, 5),
                new Point2D(5, 1));
            AssertRing(region.Holes[1],
                new Point2D(7, 1),
                new Point2D(7, 5),
                new Point2D(11, 5),
                new Point2D(11, 1));
        }

        private static Polygon2D Rect(double x0, double y0, double x1, double y1)
        {
            return new Polygon2D(new[]
            {
                new Point2D(x0, y0),
                new Point2D(x1, y0),
                new Point2D(x1, y1),
                new Point2D(x0, y1)
            });
        }

        private static void AssertRing(Polygon2D polygon, params Point2D[] expected)
        {
            Assert.Equal(expected.Length, polygon.Vertices.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(
                    polygon.Vertices[i].IsEqualTo(expected[i], 1e-6),
                    "vertex " + i + " expected " + expected[i].X + "," + expected[i].Y
                    + " actual " + polygon.Vertices[i].X + "," + polygon.Vertices[i].Y);
            }
        }
    }
}
