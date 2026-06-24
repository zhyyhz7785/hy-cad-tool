using FluentAssertions;
using HyCAD.Tables.Data;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Serialization;
using HyCAD.Tables.Structure;
using Xunit;

namespace HyCAD.Tables.Tests;

public sealed class TableJsonTests
{
    private static void AssertValid(TableGrid grid) =>
        GridInvariants.Validate(grid).IsValid.Should().BeTrue();

    private static TableGrid CreateComplexGrid()
    {
        var sameAnchor = new CellAddr(0, 0);
        var sameMember = new CellAddr(1, 1);
        var anchorOnlyAnchor = new CellAddr(2, 0);
        var diagonalAddr = new CellAddr(2, 2);
        var styledAddr = new CellAddr(0, 2);
        var fieldAddr = new CellAddr(1, 2);

        var topology = GridTopology.CreateUniform(4, 4, 10, 25);
        var grid = new TableGrid
        {
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Structure = GridStructure.CreateEmpty(topology) with
            {
                FieldIndex = new Dictionary<string, CellAddr> { ["name"] = fieldAddr }
            }
        };

        grid = GridEditor.Merge(grid, sameAnchor, 2, 2, MergeValuePolicy.SameValue);
        grid = GridEditor.Merge(grid, anchorOnlyAnchor, 1, 2, MergeValuePolicy.AnchorOnly);
        grid = GridEditor.SetValue(grid, sameAnchor, new CellValue("same"));
        grid = GridEditor.SetValue(grid, anchorOnlyAnchor, new CellValue("anchor-only"));
        grid = GridEditor.SetValue(grid, fieldAddr, new CellValue("张三"));
        grid = GridEditor.SetValue(
            grid,
            new CellAddr(3, 3),
            new CellValue("", Kind: CellValueKind.Formula, Formula: "=SUM(A1:A2)"));

        grid = GridEditor.SplitDiagonal(
            grid,
            diagonalAddr,
            DiagonalDirection.BackSlashTLBR,
            new[]
            {
                new SubCell(new CellValue("学历"), FieldKey: "education"),
                new SubCell(new CellValue("学位"), FieldKey: "degree")
            });

        var structure = grid.Structure with
        {
            Styles = new Dictionary<CellAddr, CellStyle>
            {
                [styledAddr] = new CellStyle(
                    Orientation: TextOrientation.VerticalStacked,
                    HAlign: TextAlign.Center,
                    VAlign: TextAlign.Center,
                    TextHeight: 4.5,
                    FontKey: "song",
                    Borders: BorderSet.Uniform(0.5),
                    BackColor: "#FFEEAA")
            },
            Roles = new Dictionary<CellAddr, CellRole>
            {
                [styledAddr] = CellRole.Header
            }
        };

        return grid with { Structure = structure };
    }

    [Fact]
    public void Complex_grid_round_trip_is_equivalent_and_valid()
    {
        var original = CreateComplexGrid();

        var json = TableJson.SerializeGrid(original, indented: true);
        var restored = TableJson.DeserializeGrid(json);

        restored.Should().BeEquivalentTo(original, options => options
            .ComparingByMembers<TableGrid>()
            .ComparingByMembers<GridStructure>()
            .ComparingByMembers<GridData>());
        AssertValid(restored);
    }

    [Fact]
    public void Serialize_omits_hidden_SameValue_member_cells()
    {
        var anchor = new CellAddr(0, 0);
        var member = new CellAddr(1, 1);
        var grid = GridEditor.Merge(
            GridEditor.SetValue(TableGrid.CreateEmpty(3, 3), anchor, new CellValue("x")),
            anchor,
            2,
            2,
            MergeValuePolicy.SameValue);

        grid.Data.Cells.Should().ContainKey(member);

        var json = TableJson.SerializeGrid(grid);
        json.Should().NotContain("\"1,1\"");
        json.Should().Contain("\"0,0\"");

        var restored = TableJson.DeserializeGrid(json);
        GridEditor.GetValue(restored, member).Text.Should().Be("x");
        AssertValid(restored);
    }

    [Fact]
    public void Round_trip_after_insert_row_preserves_field_key_and_value()
    {
        var fieldAddr = new CellAddr(2, 1);
        var grid = TableGrid.CreateEmpty(4, 4) with
        {
            Structure = GridStructure.CreateEmpty(GridTopology.CreateUniform(4, 4, 10, 25)) with
            {
                FieldIndex = new Dictionary<string, CellAddr> { ["name"] = fieldAddr }
            }
        };
        grid = GridEditor.SetValue(grid, fieldAddr, new CellValue("李四"));
        grid = GridEditor.InsertRow(grid, rowIndex: 0);

        var restored = TableJson.DeserializeGrid(TableJson.SerializeGrid(grid));

        restored.Structure.FieldIndex["name"].Should().Be(new CellAddr(3, 1));
        GridEditor.GetValueByField(restored, "name").Text.Should().Be("李四");
        AssertValid(restored);
    }

    [Fact]
    public void Round_trip_then_unmerge_matches_pre_round_trip_unmerge()
    {
        var anchor = new CellAddr(0, 0);
        var grid = GridEditor.Merge(
            GridEditor.SetValue(TableGrid.CreateEmpty(3, 3), anchor, new CellValue("keep")),
            anchor,
            2,
            2,
            MergeValuePolicy.SameValue);

        var expected = GridEditor.Unmerge(grid, anchor);
        var roundTripped = TableJson.DeserializeGrid(TableJson.SerializeGrid(grid));
        var actual = GridEditor.Unmerge(roundTripped, anchor);

        actual.Should().BeEquivalentTo(expected);
        AssertValid(actual);
    }

    [Fact]
    public void Empty_grid_round_trip_is_equivalent()
    {
        var original = TableGrid.CreateEmpty(3, 3);
        var restored = TableJson.DeserializeGrid(TableJson.SerializeGrid(original));

        restored.Should().BeEquivalentTo(original);
        AssertValid(restored);
    }

    [Fact]
    public void TableDocument_with_multiple_tables_round_trip_is_equivalent()
    {
        var grid1 = TableGrid.CreateEmpty(2, 2);
        var grid2 = GridEditor.SetValue(TableGrid.CreateEmpty(3, 3), new CellAddr(1, 1), new CellValue("b"));
        var original = new TableDocument
        {
            Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            Name = "测试文档",
            Tables = new[] { grid1, grid2 },
            Source = new TableSourceInfo(TableSourceKind.File, @"C:\temp\a.json"),
            Metadata = new Dictionary<string, string> { ["version"] = "1" }
        };

        var restored = TableJson.Deserialize(TableJson.Serialize(original, indented: true));

        restored.Should().BeEquivalentTo(original, options => options
            .ComparingByMembers<TableDocument>()
            .ComparingByMembers<TableGrid>()
            .ComparingByMembers<GridStructure>()
            .ComparingByMembers<GridData>());
        AssertValid(restored.Tables[0]);
        AssertValid(restored.Tables[1]);
    }

    [Fact]
    public void Double_track_sizes_round_trip_with_invariant_precision()
    {
        var topology = GridTopology.CreateUniform(2, 2, rowHeight: 12.5, colWidth: 30.25);
        var original = new TableGrid
        {
            Structure = GridStructure.CreateEmpty(topology)
        };

        var restored = TableJson.DeserializeGrid(TableJson.SerializeGrid(original));

        restored.Structure.Topology.Rows[0].Size.Should().Be(12.5);
        restored.Structure.Topology.Cols[1].Size.Should().Be(30.25);
        restored.Should().BeEquivalentTo(original);
        AssertValid(restored);
    }
}
