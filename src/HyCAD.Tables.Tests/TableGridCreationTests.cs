using FluentAssertions;
using HyCAD.Tables;
using HyCAD.Tables.Structure;
using Xunit;

namespace HyCAD.Tables.Tests;

public sealed class TableGridCreationTests
{
    [Fact]
    public void CreateEmpty_builds_uniform_topology_with_empty_overlays()
    {
        var grid = TableGrid.CreateEmpty(3, 3);

        grid.Id.Should().NotBe(Guid.Empty);
        grid.Kind.Should().Be(NodeKind.Table);
        grid.Structure.Topology.RowCount.Should().Be(3);
        grid.Structure.Topology.ColCount.Should().Be(3);
        grid.Structure.Topology.Rows.Should().HaveCount(3);
        grid.Structure.Topology.Cols.Should().HaveCount(3);
        grid.Structure.Topology.Rows.Should().OnlyContain(t => t.Size == TableConstants.DefaultRowHeight);
        grid.Structure.Topology.Cols.Should().OnlyContain(t => t.Size == TableConstants.DefaultColWidth);
        grid.Structure.Merges.Should().BeEmpty();
        grid.Structure.Diagonals.Should().BeEmpty();
        grid.Structure.Styles.Should().BeEmpty();
        grid.Structure.Roles.Should().BeEmpty();
        grid.Structure.FieldIndex.Should().BeEmpty();
        grid.Data.Cells.Should().BeEmpty();
        grid.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void CellAddr_is_value_equal_by_row_and_col()
    {
        var a = new CellAddr(1, 2);
        var b = new CellAddr(1, 2);
        var c = new CellAddr(2, 1);

        a.Should().Be(b);
        a.Should().NotBe(c);
    }

    [Fact]
    public void CreateEmpty_rejects_non_positive_dimensions()
    {
        var actRows = () => TableGrid.CreateEmpty(0, 3);
        var actCols = () => TableGrid.CreateEmpty(3, 0);

        actRows.Should().Throw<ArgumentOutOfRangeException>();
        actCols.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TableDocument_CreateWithEmptyTable_contains_one_grid()
    {
        var doc = TableDocument.CreateWithEmptyTable("test", 2, 4);

        doc.Name.Should().Be("test");
        doc.Tables.Should().ContainSingle();
        doc.Tables[0].Structure.Topology.RowCount.Should().Be(2);
        doc.Tables[0].Structure.Topology.ColCount.Should().Be(4);
    }
}
