using System.Linq;
using HyCAD.Geometry;
using HyCAD.Geometry.Algorithms;
using Xunit;

namespace HyCADTool.LinuxTests
{
    public class LineOverlapTests
    {
        [Fact]
        public void Diagonal_overlap_lies_on_the_line()
        {
            var a = new Line2D(new Point2D(0, 0), new Point2D(2, 2));
            var b = new Line2D(new Point2D(1, 1), new Point2D(3, 3));

            var overlap = LineAlgorithms.CheckOverlap(a, b);

            Assert.True(SameSegment(overlap, new Point2D(1, 1), new Point2D(2, 2)));
        }

        [Fact]
        public void Shallow_diagonal_overlap_is_not_flattened_onto_y()
        {
            var a = new Line2D(new Point2D(0, 0), new Point2D(4, 1));
            var b = new Line2D(new Point2D(2, 0.5), new Point2D(6, 1.5));

            var overlap = LineAlgorithms.CheckOverlap(a, b);

            Assert.True(SameSegment(overlap, new Point2D(2, 0.5), new Point2D(4, 1)));
        }

        [Fact]
        public void Horizontal_overlap_is_unchanged()
        {
            var a = new Line2D(new Point2D(0, 0), new Point2D(4, 0));
            var b = new Line2D(new Point2D(2, 0), new Point2D(6, 0));

            var overlap = LineAlgorithms.CheckOverlap(a, b);

            Assert.True(SameSegment(overlap, new Point2D(2, 0), new Point2D(4, 0)));
        }

        [Fact]
        public void Gap_and_endpoint_touch_are_not_overlap()
        {
            var gap = LineAlgorithms.CheckOverlap(
                new Line2D(new Point2D(0, 0), new Point2D(1, 1)),
                new Line2D(new Point2D(2, 2), new Point2D(3, 3)));
            var touch = LineAlgorithms.CheckOverlap(
                new Line2D(new Point2D(0, 0), new Point2D(1, 0)),
                new Line2D(new Point2D(1, 0), new Point2D(2, 0)));

            Assert.Equal(default(Line2D), gap);
            Assert.Equal(default(Line2D), touch);
        }

        [Fact]
        public void Crossing_lines_do_not_overlap()
        {
            var overlap = LineAlgorithms.CheckOverlap(
                new Line2D(new Point2D(0, 0), new Point2D(2, 2)),
                new Line2D(new Point2D(0, 2), new Point2D(2, 0)));

            Assert.Equal(default(Line2D), overlap);
        }

        [Fact]
        public void Collinear_diagonals_merge_to_the_outer_ends()
        {
            var merged = LineAlgorithms.MergeCollinearLines(new[]
            {
                new Line2D(new Point2D(0, 0), new Point2D(2, 2)),
                new Line2D(new Point2D(1, 1), new Point2D(3, 3))
            }).ToList();

            Assert.Single(merged);
            Assert.True(SameSegment(merged[0], new Point2D(0, 0), new Point2D(3, 3)));
        }

        private static bool SameSegment(Line2D line, Point2D a, Point2D b)
        {
            return (line.StartPoint.IsEqualTo(a) && line.EndPoint.IsEqualTo(b))
                || (line.StartPoint.IsEqualTo(b) && line.EndPoint.IsEqualTo(a));
        }
    }
}
