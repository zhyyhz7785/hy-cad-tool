using System;
using System.Collections.Generic;
using System.Linq;
using HyCAD.Geometry;
using HyCAD.Geometry.Algorithms;
using Xunit;

namespace HyCADTool.LinuxTests
{
    public class GeometryReviewFixTests
    {
        [Fact]
        public void OffsetPolygon_square_uses_both_edge_normals()
        {
            var square = new[]
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(10, 10),
                new Point2D(0, 10)
            };

            var offset = PolygonAlgorithms.OffsetPolygon(square, 1.0);

            Assert.Equal(4, offset.Count);
            foreach (var p in offset)
            {
                Assert.InRange(p.X, 0.2, 9.8);
                Assert.InRange(p.Y, 0.2, 9.8);
            }

            Assert.True(offset[0].IsEqualTo(new Point2D(Math.Sqrt(2) / 2, Math.Sqrt(2) / 2), 1e-6));
            Assert.True(offset[1].IsEqualTo(new Point2D(10 - Math.Sqrt(2) / 2, Math.Sqrt(2) / 2), 1e-6));
        }

        [Fact]
        public void OffsetPolygon_l_shape_keeps_six_vertices()
        {
            var l = new[]
            {
                new Point2D(0, 0),
                new Point2D(6, 0),
                new Point2D(6, 2),
                new Point2D(2, 2),
                new Point2D(2, 6),
                new Point2D(0, 6)
            };

            var offset = PolygonAlgorithms.OffsetPolygon(l, 0.5);

            Assert.Equal(6, offset.Count);
            Assert.All(offset, p => Assert.False(double.IsNaN(p.X) || double.IsNaN(p.Y)));
        }

        [Fact]
        public void Intersection_of_overlapping_squares_has_area_one()
        {
            var a = new Polygon2D(new[]
            {
                new Point2D(0, 0), new Point2D(2, 0), new Point2D(2, 2), new Point2D(0, 2)
            });
            var b = new Polygon2D(new[]
            {
                new Point2D(1, 1), new Point2D(3, 1), new Point2D(3, 3), new Point2D(1, 3)
            });

            var service = new PolygonAlgorithmService(new LineAlgorithmService());
            var parts = service.CalculateIntersection(a, b, Tolerance.Default);

            Assert.NotEmpty(parts);
            double area = parts.Sum(p => Math.Abs(p.GetSignedArea()));
            Assert.InRange(area, 0.99, 1.01);
        }

        [Fact]
        public void Union_of_overlapping_squares_is_not_the_bounding_box()
        {
            var a = new Polygon2D(new[]
            {
                new Point2D(0, 0), new Point2D(2, 0), new Point2D(2, 2), new Point2D(0, 2)
            });
            var b = new Polygon2D(new[]
            {
                new Point2D(1, 1), new Point2D(3, 1), new Point2D(3, 3), new Point2D(1, 3)
            });

            var service = new PolygonAlgorithmService(new LineAlgorithmService());
            var parts = service.CalculateUnion(a, b, Tolerance.Default);

            Assert.NotEmpty(parts);
            double area = parts.Sum(p => Math.Abs(p.GetSignedArea()));
            Assert.InRange(area, 6.9, 7.1);
        }

        [Fact]
        public void QuickHull_matches_graham_scan_when_farthest_is_on_the_other_side()
        {
            var points = new[]
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(5, 1),
                new Point2D(5, -5),
                new Point2D(4, 0.2)
            };

            var quick = ConvexHullAlgorithm.QuickHull(points);
            var graham = ConvexHullAlgorithm.GrahamScan(points);

            Assert.Equal(NormalizeHull(graham), NormalizeHull(quick));
            Assert.True(quick.GetSignedArea() > 0);
        }

        [Fact]
        public void Subtracting_holes_does_not_return_hole_rings_as_solids()
        {
            var outer = Square(0, 0, 10, 10);
            var holes = new[]
            {
                Ring(1, 1, 3, 3),
                Ring(6, 6, 8, 8)
            };

            var parts = PolygonBoolean.IntersectPolygonWithRegionAll(outer, Ring(0, 0, 10, 10), holes);

            Assert.Single(parts);
            Assert.InRange(Math.Abs(parts[0].GetSignedArea()), 90, 110);
        }

        [Fact]
        public void Union_keeps_island_inside_a_hole_and_drops_the_hole_ring()
        {
            var outer = Square(0, 0, 10, 10);
            var hole = Square(8, 2, 2, 8);
            var island = Square(4, 4, 6, 6);

            var parts = PolygonBoolean.UnionAll(new[] { outer, hole, island });

            Assert.Equal(2, parts.Count);
            Assert.InRange(Math.Abs(parts[0].GetSignedArea()), 90, 110);
            Assert.InRange(Math.Abs(parts[1].GetSignedArea()), 3.9, 4.1);
        }

        [Fact]
        public void Union_of_two_disjoint_squares_returns_both()
        {
            var parts = PolygonBoolean.UnionAll(new[]
            {
                Square(0, 0, 1, 1),
                Square(3, 3, 4, 4)
            });

            Assert.Equal(2, parts.Count);
            double area = parts.Sum(p => Math.Abs(p.GetSignedArea()));
            Assert.InRange(area, 1.99, 2.01);
        }

        [Fact]
        public void NormalizeAngle_wraps_finite_values_and_rejects_non_finite()
        {
            double twoPi = 2 * Math.PI;
            Assert.Equal(0, HyCAD.Geometry.Math.AngleCalculator.NormalizeAngle(0), 6);
            Assert.Equal(0, HyCAD.Geometry.Math.AngleCalculator.NormalizeAngle(twoPi), 8);
            Assert.Equal(3 * Math.PI / 2, HyCAD.Geometry.Math.AngleCalculator.NormalizeAngle(-Math.PI / 2), 8);
            Assert.Equal(1, HyCAD.Geometry.Math.AngleCalculator.NormalizeAngle(10000 * twoPi + 1), 6);
            Assert.True(double.IsNaN(HyCAD.Geometry.Math.AngleCalculator.NormalizeAngle(double.PositiveInfinity)));
            Assert.True(double.IsNaN(HyCAD.Geometry.Math.AngleCalculator.NormalizeAngle(double.NegativeInfinity)));
            Assert.True(double.IsNaN(HyCAD.Geometry.Math.AngleCalculator.NormalizeAngle(double.NaN)));
        }

        [Fact]
        public void Point2D_equals_is_exact_and_matches_hash_code()
        {
            var a = new Point2D(0, 0);
            var b = new Point2D(1e-10, 0);

            Assert.False(a.Equals(b));
            Assert.True(a.IsEqualTo(b, 1e-9));
            Assert.NotEqual(a.GetHashCode(), b.GetHashCode());

            var set = new HashSet<Point2D> { a, b };
            Assert.Equal(2, set.Count);
            Assert.Contains(a, set);
            Assert.Contains(b, set);
        }

        private static Polygon2D Square(double x0, double y0, double x1, double y1)
        {
            return new Polygon2D(new[]
            {
                new Point2D(x0, y0),
                new Point2D(x1, y0),
                new Point2D(x1, y1),
                new Point2D(x0, y1)
            });
        }

        private static Polyline2D Ring(double x0, double y0, double x1, double y1)
        {
            return new Polyline2D(new[]
            {
                new Point2D(x0, y0),
                new Point2D(x1, y0),
                new Point2D(x1, y1),
                new Point2D(x0, y1)
            }, true);
        }

        private static string NormalizeHull(Polygon2D hull)
        {
            var pts = hull.Vertices
                .Select(p => (Math.Round(p.X, 6), Math.Round(p.Y, 6)))
                .ToList();
            int start = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                if (pts[i].Item2 < pts[start].Item2
                    || (pts[i].Item2 == pts[start].Item2 && pts[i].Item1 < pts[start].Item1))
                    start = i;
            }

            var ordered = new List<(double, double)>();
            for (int i = 0; i < pts.Count; i++)
                ordered.Add(pts[(start + i) % pts.Count]);
            return string.Join(";", ordered.Select(p => p.Item1.ToString("F6") + "," + p.Item2.ToString("F6")));
        }
    }
}
