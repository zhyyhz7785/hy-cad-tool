using FluentAssertions;
using HyCAD.Tables;
using HyCAD.Tables.Samples;
using HyCADTool.Features.Tables.TableApp;
using Xunit;

namespace HyCADTool.Tests.Features.Tables
{
    public sealed class TableInferDiffTests
    {
        [Fact]
        public void Compare_identical_grids_scores_100_percent()
        {
            var grid = TableSamples.BuildPersonnelTable();
            var diff = TableInferDiff.Compare(grid, grid);

            diff.TopologyMatch.Should().BeTrue();
            diff.MergeMatch.Should().BeTrue();
            diff.TextMatchRate.Should().Be(1.0);
            diff.OverallScore.Should().Be(1.0);
        }

        [Fact]
        public void Compare_different_topology_scores_below_merge_weight()
        {
            var golden = TableGrid.CreateEmpty(3, 3);
            var inferred = TableGrid.CreateEmpty(2, 2);
            var diff = TableInferDiff.Compare(inferred, golden);

            diff.TopologyMatch.Should().BeFalse();
            diff.OverallScore.Should().BeLessThan(0.7);
        }
    }
}
