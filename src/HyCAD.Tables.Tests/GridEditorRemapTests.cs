using FluentAssertions;
using HyCAD.Tables.Data;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;
using Xunit;

namespace HyCAD.Tables.Tests;

public sealed class GridEditorRemapTests
{
    private static TableGrid CreateGridWithMerge(int top, int left, int rowSpan, int colSpan, int rows = 5, int cols = 5)
    {
        var grid = TableGrid.CreateEmpty(rows, cols);
        return GridEditor.Merge(grid, new CellAddr(top, left), rowSpan, colSpan);
    }

    private static MergeRegion SingleMerge(TableGrid grid) =>
        grid.Structure.Merges.Should().ContainSingle().Subject;

    [Fact]
    public void InsertRow_above_merge_shifts_top_down()
    {
        var grid = CreateGridWithMerge(top: 2, left: 1, rowSpan: 2, colSpan: 2);
        var result = GridEditor.InsertRow(grid, rowIndex: 1);

        var merge = SingleMerge(result);
        merge.TopLeft.Should().Be(new CellAddr(3, 1));
        merge.RowSpan.Should().Be(2);
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void InsertRow_inside_merge_expands_row_span()
    {
        var grid = CreateGridWithMerge(top: 1, left: 1, rowSpan: 2, colSpan: 2);
        var result = GridEditor.InsertRow(grid, rowIndex: 2);

        var merge = SingleMerge(result);
        merge.TopLeft.Should().Be(new CellAddr(1, 1));
        merge.RowSpan.Should().Be(3);
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void InsertRow_below_merge_leaves_merge_unchanged()
    {
        var grid = CreateGridWithMerge(top: 1, left: 1, rowSpan: 2, colSpan: 2);
        var result = GridEditor.InsertRow(grid, rowIndex: 3);

        var merge = SingleMerge(result);
        merge.TopLeft.Should().Be(new CellAddr(1, 1));
        merge.RowSpan.Should().Be(2);
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void InsertColumn_above_merge_shifts_left_right()
    {
        var grid = CreateGridWithMerge(top: 1, left: 2, rowSpan: 2, colSpan: 2);
        var result = GridEditor.InsertColumn(grid, colIndex: 1);

        var merge = SingleMerge(result);
        merge.TopLeft.Should().Be(new CellAddr(1, 3));
        merge.ColSpan.Should().Be(2);
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void InsertColumn_inside_merge_expands_col_span()
    {
        var grid = CreateGridWithMerge(top: 1, left: 1, rowSpan: 2, colSpan: 2);
        var result = GridEditor.InsertColumn(grid, colIndex: 2);

        var merge = SingleMerge(result);
        merge.TopLeft.Should().Be(new CellAddr(1, 1));
        merge.ColSpan.Should().Be(3);
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void DeleteRow_above_merge_shifts_top_up()
    {
        var grid = CreateGridWithMerge(top: 2, left: 1, rowSpan: 2, colSpan: 2);
        var result = GridEditor.DeleteRow(grid, rowIndex: 1);

        var merge = SingleMerge(result);
        merge.TopLeft.Should().Be(new CellAddr(1, 1));
        merge.RowSpan.Should().Be(2);
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void DeleteRow_inside_merge_shrinks_row_span()
    {
        var grid = CreateGridWithMerge(top: 1, left: 1, rowSpan: 3, colSpan: 2);
        var result = GridEditor.DeleteRow(grid, rowIndex: 2);

        var merge = SingleMerge(result);
        merge.TopLeft.Should().Be(new CellAddr(1, 1));
        merge.RowSpan.Should().Be(2);
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void DeleteColumn_inside_merge_shrinks_col_span()
    {
        var grid = CreateGridWithMerge(top: 1, left: 1, rowSpan: 2, colSpan: 3);
        var result = GridEditor.DeleteColumn(grid, colIndex: 2);

        var merge = SingleMerge(result);
        merge.TopLeft.Should().Be(new CellAddr(1, 1));
        merge.ColSpan.Should().Be(2);
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void DeleteRow_below_merge_leaves_merge_unchanged()
    {
        var grid = CreateGridWithMerge(top: 1, left: 1, rowSpan: 2, colSpan: 2);
        var result = GridEditor.DeleteRow(grid, rowIndex: 3);

        var merge = SingleMerge(result);
        merge.TopLeft.Should().Be(new CellAddr(1, 1));
        merge.RowSpan.Should().Be(2);
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void DeleteRow_on_anchor_row_shifts_anchor_and_discards_old_anchor_data()
    {
        var anchor = new CellAddr(1, 1);
        var member = new CellAddr(2, 1);
        var grid = GridEditor.Merge(
            TableGrid.CreateEmpty(5, 4) with
            {
                Data = new GridData(new Dictionary<CellAddr, CellValue>
                {
                    [anchor] = new CellValue("anchor"),
                    [member] = new CellValue("member")
                })
            },
            anchor,
            rowSpan: 3,
            colSpan: 1);

        var result = GridEditor.DeleteRow(grid, rowIndex: 1);

        var merge = SingleMerge(result);
        merge.TopLeft.Should().Be(new CellAddr(1, 1));
        merge.RowSpan.Should().Be(2);
        result.Data.Cells[merge.Anchor].Text.Should().Be("member");
        result.Data.Cells.Values.Should().NotContain(v => v.Text == "anchor");
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void DeleteRow_shrinking_merge_to_1x1_removes_merge()
    {
        var grid = CreateGridWithMerge(top: 1, left: 1, rowSpan: 2, colSpan: 1);
        var result = GridEditor.DeleteRow(grid, rowIndex: 2);

        result.Structure.Merges.Should().BeEmpty();
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void InsertRow_preserves_field_key_resolution_and_data_value()
    {
        var fieldAddr = new CellAddr(2, 1);
        var grid = TableGrid.CreateEmpty(4, 4) with
        {
            Structure = GridStructure.CreateEmpty(GridTopology.CreateUniform(4, 4, 10, 25)) with
            {
                FieldIndex = new Dictionary<string, CellAddr> { ["name"] = fieldAddr }
            },
            Data = new GridData(new Dictionary<CellAddr, CellValue>
            {
                [fieldAddr] = new CellValue("张三")
            })
        };

        var result = GridEditor.InsertRow(grid, rowIndex: 0);

        result.Structure.FieldIndex["name"].Should().Be(new CellAddr(3, 1));
        result.Data.Cells[result.Structure.FieldIndex["name"]].Text.Should().Be("张三");
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void DeleteRow_discards_overlay_and_data_on_deleted_band_and_shifts_others()
    {
        var deletedAddr = new CellAddr(1, 0);
        var keptAddr = new CellAddr(2, 0);
        var grid = TableGrid.CreateEmpty(4, 3) with
        {
            Structure = GridStructure.CreateEmpty(GridTopology.CreateUniform(4, 3, 10, 25)) with
            {
                Diagonals = new Dictionary<CellAddr, DiagonalSplit>
                {
                    [deletedAddr] = new(
                        DiagonalDirection.BackSlashTLBR,
                        new[] { new SubCell(new CellValue("a")), new SubCell(new CellValue("b")) }),
                    [keptAddr] = new(
                        DiagonalDirection.SlashBLTR,
                        new[] { new SubCell(new CellValue("c")), new SubCell(new CellValue("d")) })
                },
                Styles = new Dictionary<CellAddr, CellStyle>
                {
                    [deletedAddr] = new CellStyle(TextHeight: 3),
                    [keptAddr] = new CellStyle(TextHeight: 4)
                },
                Roles = new Dictionary<CellAddr, CellRole>
                {
                    [deletedAddr] = CellRole.Header,
                    [keptAddr] = CellRole.Label
                }
            },
            Data = new GridData(new Dictionary<CellAddr, CellValue>
            {
                [deletedAddr] = new CellValue("gone"),
                [keptAddr] = new CellValue("stay")
            })
        };

        var result = GridEditor.DeleteRow(grid, rowIndex: 1);

        var shiftedAddr = new CellAddr(1, 0);
        result.Structure.Diagonals.Should().ContainSingle()
            .Which.Key.Should().Be(shiftedAddr);
        result.Structure.Diagonals[shiftedAddr].Direction.Should().Be(DiagonalDirection.SlashBLTR);
        result.Structure.Styles[shiftedAddr].TextHeight.Should().Be(4);
        result.Structure.Roles[shiftedAddr].Should().Be(CellRole.Label);
        result.Data.Cells[shiftedAddr].Text.Should().Be("stay");
        result.Data.Cells.Values.Should().NotContain(v => v.Text == "gone");
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void InsertRow_out_of_range_throws()
    {
        var grid = TableGrid.CreateEmpty(3, 3);

        var act = () => GridEditor.InsertRow(grid, rowIndex: 4);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void DeleteRow_last_row_throws()
    {
        var grid = TableGrid.CreateEmpty(1, 3);

        var act = () => GridEditor.DeleteRow(grid, rowIndex: 0);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*最后一行*");
    }

    [Fact]
    public void DeleteColumn_last_column_throws()
    {
        var grid = TableGrid.CreateEmpty(3, 1);

        var act = () => GridEditor.DeleteColumn(grid, colIndex: 0);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*最后一列*");
    }

    [Fact]
    public void DeleteRow_out_of_range_throws()
    {
        var grid = TableGrid.CreateEmpty(3, 3);

        var act = () => GridEditor.DeleteRow(grid, rowIndex: 3);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void WithRowInserted_inherits_adjacent_track_size()
    {
        var topology = GridTopology.CreateUniform(2, 2, rowHeight: 12, colWidth: 30);
        var inserted = topology.WithRowInserted(1);

        inserted.Rows[1].Size.Should().Be(12);
        inserted.RowCount.Should().Be(3);
    }
}
