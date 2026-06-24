using FluentAssertions;
using HyCAD.Tables.Data;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;
using Xunit;

namespace HyCAD.Tables.Tests;

public sealed class GridEditorValueTests
{
    [Fact]
    public void SetValue_on_SameValue_merge_mirrors_to_all_members()
    {
        var anchor = new CellAddr(0, 0);
        var member = new CellAddr(1, 1);
        var grid = GridEditor.Merge(
            TableGrid.CreateEmpty(3, 3),
            anchor,
            rowSpan: 2,
            colSpan: 2,
            MergeValuePolicy.SameValue);

        var result = GridEditor.SetValue(grid, anchor, new CellValue("hello"));

        result.Data.Cells.Should().ContainKeys(anchor, member);
        result.Data.Cells[member].Text.Should().Be("hello");
        GridEditor.GetValue(result, member).Text.Should().Be("hello");
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void SetValue_on_AnchorOnly_merge_writes_only_anchor_but_reads_via_member()
    {
        var anchor = new CellAddr(0, 0);
        var member = new CellAddr(1, 1);
        var grid = GridEditor.Merge(TableGrid.CreateEmpty(3, 3), anchor, 2, 2);

        var result = GridEditor.SetValue(grid, anchor, new CellValue("only-anchor"));

        result.Data.Cells.Should().ContainKey(anchor);
        result.Data.Cells.Should().NotContainKey(member);
        GridEditor.GetValue(result, member).Text.Should().Be("only-anchor");
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void SetValue_on_covered_member_redirects_to_anchor()
    {
        var anchor = new CellAddr(0, 0);
        var member = new CellAddr(1, 0);
        var grid = GridEditor.Merge(
            TableGrid.CreateEmpty(3, 3),
            anchor,
            rowSpan: 2,
            colSpan: 1,
            MergeValuePolicy.SameValue);

        var result = GridEditor.SetValue(grid, member, new CellValue("via-member"));

        GridEditor.GetValue(result, anchor).Text.Should().Be("via-member");
        result.Data.Cells[anchor].Text.Should().Be("via-member");
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void SetValueByField_writes_target_cell()
    {
        var addr = new CellAddr(1, 2);
        var grid = TableGrid.CreateEmpty(3, 3) with
        {
            Structure = GridStructure.CreateEmpty(GridTopology.CreateUniform(3, 3, 10, 25)) with
            {
                FieldIndex = new Dictionary<string, CellAddr> { ["name"] = addr }
            }
        };

        var result = GridEditor.SetValueByField(grid, "name", new CellValue("张三"));

        result.Structure.FieldIndex["name"].Should().Be(addr);
        GridEditor.GetValueByField(result, "name").Text.Should().Be("张三");
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void SetValueByField_after_insert_row_still_hits_remapped_cell()
    {
        var addr = new CellAddr(2, 1);
        var grid = TableGrid.CreateEmpty(4, 4) with
        {
            Structure = GridStructure.CreateEmpty(GridTopology.CreateUniform(4, 4, 10, 25)) with
            {
                FieldIndex = new Dictionary<string, CellAddr> { ["name"] = addr }
            }
        };

        var inserted = GridEditor.InsertRow(grid, rowIndex: 0);
        var result = GridEditor.SetValueByField(inserted, "name", new CellValue("李四"));

        result.Structure.FieldIndex["name"].Should().Be(new CellAddr(3, 1));
        GridEditor.GetValueByField(result, "name").Text.Should().Be("李四");
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void SetFieldKey_rejects_duplicate_key_on_different_cell()
    {
        var grid = TableGrid.CreateEmpty(3, 3);
        grid = GridEditor.SetFieldKey(grid, new CellAddr(0, 0), "name");

        var act = () => GridEditor.SetFieldKey(grid, new CellAddr(1, 1), "name");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*已绑定*");
    }

    [Fact]
    public void SetFieldKey_can_replace_key_on_same_cell()
    {
        var addr = new CellAddr(0, 0);
        var grid = GridEditor.SetFieldKey(TableGrid.CreateEmpty(3, 3), addr, "old");
        var result = GridEditor.SetFieldKey(grid, addr, "new");

        result.Structure.FieldIndex.Should().NotContainKey("old");
        result.Structure.FieldIndex["new"].Should().Be(addr);
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void SetFieldKey_null_removes_binding()
    {
        var addr = new CellAddr(0, 0);
        var grid = GridEditor.SetFieldKey(TableGrid.CreateEmpty(3, 3), addr, "name");
        var result = GridEditor.SetFieldKey(grid, addr, null);

        result.Structure.FieldIndex.Should().BeEmpty();
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void SetValue_empty_removes_sparse_keys_including_SameValue_members()
    {
        var anchor = new CellAddr(0, 0);
        var member = new CellAddr(1, 1);
        var grid = GridEditor.Merge(
            TableGrid.CreateEmpty(3, 3),
            anchor,
            2,
            2,
            MergeValuePolicy.SameValue);
        grid = GridEditor.SetValue(grid, anchor, new CellValue("temp"));

        var result = GridEditor.SetValue(grid, anchor, CellValue.Empty);

        result.Data.Cells.Should().NotContainKeys(anchor, member);
        GridEditor.GetValue(result, member).Should().Be(CellValue.Empty);
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Merge_SameValue_mirrors_existing_anchor_value_immediately()
    {
        var anchor = new CellAddr(0, 0);
        var member = new CellAddr(1, 1);
        var grid = GridEditor.SetValue(
            TableGrid.CreateEmpty(3, 3),
            anchor,
            new CellValue("preset"));

        var result = GridEditor.Merge(grid, anchor, 2, 2, MergeValuePolicy.SameValue);

        result.Data.Cells.Should().ContainKeys(anchor, member);
        GridEditor.GetValue(result, member).Text.Should().Be("preset");
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Merge_AnchorOnly_does_not_mirror_member_keys()
    {
        var anchor = new CellAddr(0, 0);
        var member = new CellAddr(1, 1);
        var grid = GridEditor.SetValue(
            TableGrid.CreateEmpty(3, 3),
            anchor,
            new CellValue("preset"));

        var result = GridEditor.Merge(grid, anchor, 2, 2, MergeValuePolicy.AnchorOnly);

        result.Data.Cells.Should().ContainKey(anchor);
        result.Data.Cells.Should().NotContainKey(member);
        GridEditor.GetValue(result, member).Text.Should().Be("preset");
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void SetValueByField_missing_key_throws()
    {
        var act = () => GridEditor.SetValueByField(TableGrid.CreateEmpty(3, 3), "missing", new CellValue("x"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*未找到 FieldKey*");
    }

    [Fact]
    public void GetValueByField_missing_key_throws()
    {
        var act = () => GridEditor.GetValueByField(TableGrid.CreateEmpty(3, 3), "missing");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*未找到 FieldKey*");
    }

    [Fact]
    public void SetValue_non_empty_formula_is_not_treated_as_empty()
    {
        var addr = new CellAddr(0, 0);
        var value = new CellValue("", Kind: CellValueKind.Formula, Formula: "=SUM(A1:A2)");
        var result = GridEditor.SetValue(TableGrid.CreateEmpty(3, 3), addr, value);

        result.Data.Cells.Should().ContainKey(addr);
        result.Data.Cells[addr].Formula.Should().Be("=SUM(A1:A2)");
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }
}
