using FluentAssertions;
using HyCAD.Tables.Data;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Serialization;
using HyCAD.Tables.Structure;
using Xunit;

namespace HyCAD.Tables.Tests;

/// <summary>
/// P8 黄金样表端到端：家庭成员表（7×6）+ 人员基本情况表（7×7）。
/// 建模逻辑可作为后续 Adapter 黄金夹具。
/// </summary>
public sealed class SampleTablesEndToEndTests
{
    private static void AssertValid(TableGrid grid) =>
        GridInvariants.Validate(grid).IsValid.Should().BeTrue();

    private static TableGrid WithRoles(TableGrid grid, params (CellAddr Addr, CellRole Role)[] roles)
    {
        var dict = grid.Structure.Roles.ToDictionary(entry => entry.Key, entry => entry.Value);
        foreach (var (addr, role) in roles)
            dict[addr] = role;
        return grid with { Structure = grid.Structure with { Roles = dict } };
    }

    private static TableGrid WithStyles(TableGrid grid, params (CellAddr Addr, CellStyle Style)[] styles)
    {
        var dict = grid.Structure.Styles.ToDictionary(entry => entry.Key, entry => entry.Value);
        foreach (var (addr, style) in styles)
            dict[addr] = style;
        return grid with { Structure = grid.Structure with { Styles = dict } };
    }

    /// <summary>家庭成员表（7×6）：表头 + colspan + 6 行数据 + FieldKey。</summary>
    public static TableGrid BuildFamilyTable()
    {
        var grid = TableGrid.CreateEmpty(7, 6);

        grid = GridEditor.SetValue(grid, new CellAddr(0, 0), new CellValue("称谓"));
        grid = GridEditor.SetValue(grid, new CellAddr(0, 1), new CellValue("姓名"));
        grid = GridEditor.SetValue(grid, new CellAddr(0, 2), new CellValue("年龄"));
        grid = GridEditor.SetValue(grid, new CellAddr(0, 3), new CellValue("政治面貌"));
        grid = GridEditor.Merge(grid, new CellAddr(0, 4), rowSpan: 1, colSpan: 2);
        grid = GridEditor.SetValue(grid, new CellAddr(0, 4), new CellValue("工作单位及职务"));

        grid = WithRoles(
            grid,
            (new CellAddr(0, 0), CellRole.Header),
            (new CellAddr(0, 1), CellRole.Header),
            (new CellAddr(0, 2), CellRole.Header),
            (new CellAddr(0, 3), CellRole.Header),
            (new CellAddr(0, 4), CellRole.Header));

        for (var row = 1; row <= 6; row++)
        {
            grid = GridEditor.SetValue(grid, new CellAddr(row, 0), new CellValue("父亲"));
            grid = GridEditor.SetFieldKey(grid, new CellAddr(row, 1), $"member{row - 1}_name");
            grid = GridEditor.SetValueByField(grid, $"member{row - 1}_name", new CellValue($"成员{row}"));
            grid = GridEditor.SetValue(grid, new CellAddr(row, 2), new CellValue($"{40 + row}"));
            grid = GridEditor.SetValue(grid, new CellAddr(row, 3), new CellValue("群众"));
            grid = GridEditor.SetValue(grid, new CellAddr(row, 4), new CellValue($"单位{row}"));
        }

        return grid;
    }

    /// <summary>人员基本情况表（7×7）：Title/PhotoSlot/竖排/colspan/FieldKey name。</summary>
    public static TableGrid BuildPersonnelTable()
    {
        var grid = TableGrid.CreateEmpty(7, 7);

        grid = GridEditor.Merge(grid, new CellAddr(0, 0), rowSpan: 1, colSpan: 7);
        grid = GridEditor.SetValue(grid, new CellAddr(0, 0), new CellValue("人员基本情况表"));
        grid = WithRoles(grid, (new CellAddr(0, 0), CellRole.Title));

        grid = GridEditor.Merge(grid, new CellAddr(1, 6), rowSpan: 3, colSpan: 1);
        grid = WithRoles(grid, (new CellAddr(1, 6), CellRole.PhotoSlot));

        grid = GridEditor.SetValue(grid, new CellAddr(1, 0), new CellValue("姓名"));
        grid = GridEditor.SetFieldKey(grid, new CellAddr(1, 1), "name");
        grid = GridEditor.SetValueByField(grid, "name", new CellValue("张三"));
        grid = GridEditor.SetValue(grid, new CellAddr(1, 2), new CellValue("性别"));
        grid = GridEditor.SetValue(grid, new CellAddr(1, 3), new CellValue("男"));
        grid = GridEditor.SetValue(grid, new CellAddr(1, 4), new CellValue("出生年月"));
        grid = GridEditor.SetValue(grid, new CellAddr(1, 5), new CellValue("1990-01"));

        grid = WithRoles(
            grid,
            (new CellAddr(1, 0), CellRole.Label),
            (new CellAddr(1, 1), CellRole.Value),
            (new CellAddr(1, 2), CellRole.Label),
            (new CellAddr(1, 3), CellRole.Value),
            (new CellAddr(1, 4), CellRole.Label),
            (new CellAddr(1, 5), CellRole.Value));

        grid = GridEditor.SetValue(grid, new CellAddr(3, 0), new CellValue("籍贯"));
        grid = GridEditor.Merge(grid, new CellAddr(3, 1), rowSpan: 1, colSpan: 3);
        grid = GridEditor.SetValue(grid, new CellAddr(3, 1), new CellValue("北京市"));
        grid = WithRoles(
            grid,
            (new CellAddr(3, 0), CellRole.Label),
            (new CellAddr(3, 1), CellRole.Value));

        grid = GridEditor.Merge(grid, new CellAddr(4, 0), rowSpan: 3, colSpan: 1);
        grid = GridEditor.SetValue(grid, new CellAddr(4, 0), new CellValue("家庭主要成员及重要社会关系"));
        grid = WithStyles(
            grid,
            (new CellAddr(4, 0), new CellStyle(Orientation: TextOrientation.VerticalStacked)));
        grid = WithRoles(grid, (new CellAddr(4, 0), CellRole.Header));

        grid = GridEditor.SetValue(grid, new CellAddr(5, 1), new CellValue("学习和工作简历"));
        grid = GridEditor.Merge(grid, new CellAddr(5, 2), rowSpan: 1, colSpan: 5);
        grid = GridEditor.SetValue(grid, new CellAddr(5, 2), new CellValue("2008-2012 某大学；2012-至今 某单位"));
        grid = WithRoles(
            grid,
            (new CellAddr(5, 1), CellRole.Label),
            (new CellAddr(5, 2), CellRole.Value));

        return grid;
    }

    private static MergeRegion FindMergeByAnchor(TableGrid grid, CellAddr anchor) =>
        grid.Structure.Merges.Should().ContainSingle(m => m.Anchor == anchor).Subject;

    [Fact]
    public void Family_table_end_to_end_round_trip_is_equivalent()
    {
        var original = BuildFamilyTable();
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
        var original = BuildPersonnelTable();
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
        var grid = BuildPersonnelTable();
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
        var grid = BuildPersonnelTable();
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
        var grid = BuildPersonnelTable();
        var inserted = GridEditor.InsertRow(grid, rowIndex: 0);

        inserted.Structure.FieldIndex["name"].Should().Be(new CellAddr(2, 1));
        GridEditor.GetValueByField(inserted, "name").Text.Should().Be("张三");
        AssertValid(inserted);
    }

    [Fact]
    public void Personnel_delete_column_shrinks_native_place_merge_span()
    {
        var grid = BuildPersonnelTable();
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
            Tables = new[] { BuildFamilyTable(), BuildPersonnelTable() },
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
}
