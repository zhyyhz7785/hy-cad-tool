using System.Linq;
using HyCAD.Geometry;
using HyCAD.Geometry.Algorithms;
using HyCAD.Geometry.Math;
using Xunit;

namespace HyCADTool.LinuxTests
{
    public class LineIntersectionTests
    {
        [Fact]
        public void Crossing_at_origin_is_a_hit()
        {
            var a = new Line2D(new Point2D(-1, 0), new Point2D(1, 0));
            var b = new Line2D(new Point2D(0, -1), new Point2D(0, 1));

            Assert.True(a.TryGetIntersection(b, out var hit));
            Assert.True(hit.IsEqualTo(new Point2D(0, 0)));
            Assert.Equal(0, DistanceCalculator.LineSegmentToLineSegmentDistance(
                a.StartPoint, a.EndPoint, b.StartPoint, b.EndPoint), 9);
        }

        [Fact]
        public void Parallel_segments_are_not_a_hit()
        {
            var a = new Line2D(new Point2D(0, 0), new Point2D(1, 0));
            var b = new Line2D(new Point2D(0, 1), new Point2D(1, 1));

            Assert.False(a.TryGetIntersection(b, out _));
            Assert.False(a.TryGetIntersectionWithInfiniteLine(b, out _));
        }

        [Fact]
        public void Infinite_lines_can_meet_at_the_origin()
        {
            var a = new Line2D(new Point2D(1, 1), new Point2D(2, 2));
            var b = new Line2D(new Point2D(-1, 1), new Point2D(-2, 2));

            Assert.True(a.TryGetIntersectionWithInfiniteLine(b, out var hit));
            Assert.True(hit.IsEqualTo(new Point2D(0, 0)));
            Assert.False(a.TryGetIntersection(b, out _));
        }

        [Fact]
        public void Far_line_does_not_intersect_square()
        {
            var square = new Polygon2D(new[]
            {
                new Point2D(1, 1), new Point2D(2, 1), new Point2D(2, 2), new Point2D(1, 2)
            });
            var far = new Line2D(new Point2D(10, 10), new Point2D(11, 10));

            Assert.False(square.IntersectsWith(far));
        }

        [Fact]
        public void Bowtie_self_intersects_at_origin()
        {
            var bowtie = new Polygon2D(new[]
            {
                new Point2D(-1, -1), new Point2D(1, 1), new Point2D(1, -1), new Point2D(-1, 1)
            });

            Assert.True(PolygonAlgorithms.HasSelfIntersection(bowtie));
        }

        [Fact]
        public void Long_cross_is_split_at_the_intersection()
        {
            var lines = new[]
            {
                new Line2D(new Point2D(0, 0), new Point2D(100, 0)),
                new Line2D(new Point2D(50, -50), new Point2D(50, 50))
            };

            var pieces = new CurveBreakService().BreakLinesAtIntersections(lines, minLength: 0.1);

            Assert.Equal(4, pieces.Count);
            Assert.Contains(pieces, p => p.StartPoint.IsEqualTo(new Point2D(0, 0)) && p.EndPoint.IsEqualTo(new Point2D(50, 0))
                || p.StartPoint.IsEqualTo(new Point2D(50, 0)) && p.EndPoint.IsEqualTo(new Point2D(0, 0)));
            Assert.Contains(pieces, p => p.StartPoint.IsEqualTo(new Point2D(50, 0)) && p.EndPoint.IsEqualTo(new Point2D(100, 0))
                || p.StartPoint.IsEqualTo(new Point2D(100, 0)) && p.EndPoint.IsEqualTo(new Point2D(50, 0)));
        }
    }
}
