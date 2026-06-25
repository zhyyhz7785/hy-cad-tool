using System;
using FluentAssertions;
using HyCAD.Tables;
using HyCAD.Tables.Data;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Samples;
using HyCAD.Tables.Structure;
using HyCADTool.Features.Tables.TableApp;
using Xunit;

namespace HyCADTool.Tests.Features.Tables
{
    public sealed class TableSummaryBuilderTests
    {
        [Fact]
        public void Build_personnel_table_has_title_and_field_key()
        {
            var summary = TableSummaryBuilder.Build(TableSamples.BuildPersonnelTable());

            summary.DisplayTitle.Should().Be("人员基本情况表");
            summary.RowCount.Should().Be(7);
            summary.ColCount.Should().Be(7);
            summary.FieldKeyCount.Should().Be(1);
            summary.FieldKeys.Should().Contain("name");
            summary.InvariantsValid.Should().BeTrue();
        }

        [Fact]
        public void Build_family_table_has_field_keys_and_merges()
        {
            var summary = TableSummaryBuilder.Build(TableSamples.BuildFamilyTable());

            summary.FieldKeyCount.Should().Be(6);
            summary.MergeRegionCount.Should().BeGreaterOrEqualTo(1);
            summary.RowCount.Should().Be(7);
            summary.ColCount.Should().Be(6);
            summary.InvariantsValid.Should().BeTrue();
        }

        [Fact]
        public void Build_counts_formula_cells()
        {
            var grid = TableGrid.CreateEmpty(2, 2);
            grid = GridEditor.SetValue(
                grid,
                new CellAddr(0, 0),
                new CellValue("42", Kind: CellValueKind.Formula, Formula: "=SUM(A1)"));

            var summary = TableSummaryBuilder.Build(grid);

            summary.FormulaCellCount.Should().Be(1);
        }

        [Fact]
        public void Build_invalid_merge_reports_invariant_failure()
        {
            var topology = new GridTopology(
                Array.Empty<GridTrack>(),
                new[] { new GridTrack(10) });
            var structure = GridStructure.CreateEmpty(topology);
            var grid = TableGrid.FromStructure(structure);

            var summary = TableSummaryBuilder.Build(grid);

            summary.InvariantsValid.Should().BeFalse();
            summary.FirstInvariantMessage.Should().NotBeNullOrEmpty();
        }
    }
}
