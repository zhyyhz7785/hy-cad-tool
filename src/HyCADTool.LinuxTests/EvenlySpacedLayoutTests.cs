using System;
using System.Linq;
using HyCADTool.Features.Reinforcement.Domain;
using Xunit;

namespace HyCADTool.LinuxTests
{
    public class EvenlySpacedLayoutTests
    {
        [Fact]
        public void Shorter_than_spacing_is_only_end_points()
        {
            var ratios = EvenlySpacedLayout.GetRatios(50, 100);

            Assert.Equal(new[] { 0.0, 1.0 }, ratios);
        }

        [Fact]
        public void Between_one_and_two_spacings_has_no_duplicate()
        {
            var ratios = EvenlySpacedLayout.GetRatios(150, 100);

            AssertInRangeAndUnique(ratios);
            Assert.Equal(0, ratios[0], 9);
            Assert.Equal(1, ratios[ratios.Length - 1], 9);
            Assert.True(ratios.Length <= 3);
        }

        [Fact]
        public void Exact_two_spacings_are_halves()
        {
            var ratios = EvenlySpacedLayout.GetRatios(200, 100);

            Assert.Equal(new[] { 0.0, 0.5, 1.0 }, ratios);
        }

        [Fact]
        public void Exact_three_spacings_are_thirds()
        {
            var ratios = EvenlySpacedLayout.GetRatios(300, 100);

            Assert.Equal(4, ratios.Length);
            Assert.Equal(0, ratios[0], 9);
            Assert.Equal(1.0 / 3.0, ratios[1], 9);
            Assert.Equal(2.0 / 3.0, ratios[2], 9);
            Assert.Equal(1, ratios[3], 9);
        }

        [Theory]
        [InlineData(0, 100)]
        [InlineData(100, 0)]
        [InlineData(-1, 10)]
        public void Non_positive_inputs_collapse_to_end_points(double length, double separation)
        {
            var ratios = EvenlySpacedLayout.GetRatios(length, separation);
            Assert.Equal(new[] { 0.0, 1.0 }, ratios);
        }

        private static void AssertInRangeAndUnique(double[] ratios)
        {
            Assert.NotEmpty(ratios);
            for (int i = 0; i < ratios.Length; i++)
            {
                Assert.InRange(ratios[i], 0, 1);
                if (i > 0)
                    Assert.True(ratios[i] > ratios[i - 1] + 1e-9);
            }
        }
    }
}
