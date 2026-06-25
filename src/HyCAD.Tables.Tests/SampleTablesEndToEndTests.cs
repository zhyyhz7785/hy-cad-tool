using FluentAssertions;
using HyCAD.Tables.Data;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Samples;
using HyCAD.Tables.Serialization;
using HyCAD.Tables.Structure;
using Xunit;

namespace HyCAD.Tables.Tests;

/// <summary>
/// P8 黄金样表端到端：家庭成员表（7×6）+ 人员基本情况表（7×7）。
/// </summary>
public sealed class SampleTablesEndToEndTests
{
    private static void AssertValid(TableGrid grid) =>
        GridInvariants.Validate(grid).IsValid.Should().BeTrue();

    [Fact]
    public void Family_table_end_to_end_round_trip_is_equivalent()
    {
        var original = TableSamples.BuildFamilyTable();
        AssertValid(original);

        var restored = TableJson.DeserializeGrid(TableJson.SerializeGrid(original, indented: true));

        restored.Should().BeEquivalentTo(original, options => options
            .ComparingByMembers<TableGrid>()
            .ComparingByMembers<GridStructure>()
            .ComparingByMembers<GridData>());
        AssertValid(restored);
    }

    [Fact]
    public void Personnel_table_end_to_end_round_trip_is_equivalent()
    {
        var original = TableSamples.BuildPersonnelTable();
        AssertValid(original);

        var restored = TableJson.DeserializeGrid(TableJson.SerializeGrid(original));

        restored.Should().BeEquivalentTo(original, options => options
            .ComparingByMembers<TableGrid>()
            .ComparingByMembers<GridStructure>()
            .ComparingByMembers<GridData>());
        AssertValid(restored);
    }

    [Fact]
    public void Personnel_table_has_expected_title_photo_and_vertical_sidebar()
    {
        var grid = TableSamples.BuildPersonnelTable();
        AssertValid(grid);

        var titleMerge = FindMergeByAnchor(grid, new CellAddr(0, 0));
        titleMerge.RowSpan.Should().Be(1);
        titleMerge.ColSpan.Should().Be(7);
        grid.Structure.Roles[new CellAddr(0, 0)].Should().Be(CellRole.Title);

        var photoMerge = FindMergeByAnchor(grid, new CellAddr(1, 6));
        photoMerge.RowSpan.Should().Be(3);
        photoMerge.ColSpan.Should().Be(1);
        grid.Structure.Roles[new CellAddr(1, 6)].Should().Be(CellRole.PhotoSlot);

        grid.Structure.Styles[new CellAddr(4, 0)].Orientation
            .Should().Be(TextOrientation.VerticalStacked);
    }

    [Fact]
    public void Personnel_title_unmerge_is_reversible_without_data_loss()
    {
        var grid = TableSamples.BuildPersonnelTable();
        var titleAnchor = new CellAddr(0, 0);
        var titleValue = GridEditor.GetValue(grid, titleAnchor);

        var unmerged = GridEditor.Unmerge(grid, titleAnchor);

        unmerged.Structure.Merges.Should().NotContain(m => m.Anchor == titleAnchor);
        GridEditor.GetValue(unmerged, titleAnchor).Should().BeEquivalentTo(titleValue);
        AssertValid(unmerged);
    }

    [Fact]
    public void Personnel_insert_row_preserves_name_field_key_resolution()
    {
        var grid = TableSamples.BuildPersonnelTable();
        var inserted = GridEditor.InsertRow(grid, rowIndex: 0);

        inserted.Structure.FieldIndex["name"].Should().Be(new CellAddr(2, 1));
        GridEditor.GetValueByField(inserted, "name").Text.Should().Be("张三");
        AssertValid(inserted);
    }

    [Fact]
    public void Personnel_delete_column_shrinks_native_place_merge_span()
    {
        var grid = TableSamples.BuildPersonnelTable();
        var nativeMerge = FindMergeByAnchor(grid, new CellAddr(3, 1));
        nativeMerge.ColSpan.Should().Be(3);

        var result = GridEditor.DeleteColumn(grid, colIndex: 2);

        var shrunk = FindMergeByAnchor(result, new CellAddr(3, 1));
        shrunk.ColSpan.Should().Be(2);
        GridEditor.GetValue(result, new CellAddr(3, 1)).Text.Should().Be("北京市");
        AssertValid(result);
    }

    [Fact]
    public void Document_with_both_sample_tables_round_trips()
    {
        var original = new TableDocument
        {
            Id = Guid.Parse("88888888-9999-aaaa-bbbb-cccccccccccc"),
            Name = "样表集合",
            Tables = new[] { TableSamples.BuildFamilyTable(), TableSamples.BuildPersonnelTable() },
            Source = new TableSourceInfo(TableSourceKind.Template, "p8-golden-fixture"),
            Metadata = new Dictionary<string, string> { ["phase"] = "P8" }
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

    private static MergeRegion FindMergeByAnchor(TableGrid grid, CellAddr anchor) =>
        grid.Structure.Merges.Should().ContainSingle(m => m.Anchor == anchor).Subject;
}
