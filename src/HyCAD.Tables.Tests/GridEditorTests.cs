using FluentAssertions;
using HyCAD.Tables.Data;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;
using Xunit;

namespace HyCAD.Tables.Tests;

public sealed class GridEditorTests
{
    [Fact]
    public void Merge_adds_region_and_passes_invariants()
    {
        var grid = TableGrid.CreateEmpty(3, 3);
        var anchor = new CellAddr(0, 0);

        var merged = GridEditor.Merge(grid, anchor, 2, 2);

        merged.Structure.Merges.Should().ContainSingle(m =>
            m.Anchor == anchor && m.RowSpan == 2 && m.ColSpan == 2);
        GridInvariants.Validate(merged).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Merge_anchor_only_clears_hidden_member_data()
    {
        var anchor = new CellAddr(0, 0);
        var member = new CellAddr(1, 1);
        var original = TableGrid.CreateEmpty(3, 3) with
        {
            Data = new GridData(new Dictionary<CellAddr, CellValue>
            {
                [anchor] = new CellValue("anchor"),
                [member] = new CellValue("member")
            })
        };

        var merged = GridEditor.Merge(original, anchor, 2, 2);

        merged.Data.Cells.Should().ContainKey(anchor);
        merged.Data.Cells.Should().NotContainKey(member);
        GridEditor.GetValue(merged, member).Text.Should().Be("anchor");
        GridInvariants.Validate(merged).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Merge_then_unmerge_preserves_anchor_data()
    {
        var anchor = new CellAddr(0, 0);
        var member = new CellAddr(1, 1);
        var original = TableGrid.CreateEmpty(3, 3) with
        {
            Data = new GridData(new Dictionary<CellAddr, CellValue>
            {
                [anchor] = new CellValue("anchor"),
                [member] = new CellValue("member")
            })
        };

        var merged = GridEditor.Merge(original, anchor, 2, 2);
        var restored = GridEditor.Unmerge(merged, anchor);

        restored.Structure.Should().BeEquivalentTo(original.Structure);
        restored.Data.Cells[anchor].Text.Should().Be("anchor");
        restored.Data.Cells.Should().NotContainKey(member);
        GridInvariants.Validate(restored).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Merge_rejects_overlapping_region()
    {
        var grid = GridEditor.Merge(TableGrid.CreateEmpty(3, 3), new CellAddr(0, 0), 2, 2);

        var act = () => GridEditor.Merge(grid, new CellAddr(1, 1), 2, 2);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*相交重叠*");
    }

    [Fact]
    public void Merge_rejects_out_of_bounds_region()
    {
        var grid = TableGrid.CreateEmpty(3, 3);

        var act = () => GridEditor.Merge(grid, new CellAddr(2, 2), 2, 1);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*超出网格*");
    }

    [Fact]
    public void Merge_rejects_1x1_region()
    {
        var grid = TableGrid.CreateEmpty(3, 3);

        var act = () => GridEditor.Merge(grid, new CellAddr(0, 0), 1, 1);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*1×1*");
    }

    [Fact]
    public void Merge_rejects_unsupported_policy()
    {
        var grid = TableGrid.CreateEmpty(3, 3);

        var act = () => GridEditor.Merge(
            grid,
            new CellAddr(0, 0),
            2,
            2,
            MergeValuePolicy.PreserveEach);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Unmerge_rejects_unknown_anchor()
    {
        var grid = TableGrid.CreateEmpty(3, 3);

        var act = () => GridEditor.Unmerge(grid, new CellAddr(0, 0));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*未找到*");
    }

    [Fact]
    public void Derived_visibility_queries_after_merge()
    {
        var anchor = new CellAddr(0, 0);
        var covered = new CellAddr(1, 1);
        var grid = GridEditor.Merge(TableGrid.CreateEmpty(3, 3), anchor, 2, 2);
        var structure = grid.Structure;

        structure.IsCovered(anchor).Should().BeTrue();
        structure.IsCovered(covered).Should().BeTrue();
        structure.IsHidden(anchor).Should().BeFalse();
        structure.IsHidden(covered).Should().BeTrue();
        structure.GetAnchorOf(covered).Should().Be(anchor);
        structure.GetAnchorOf(new CellAddr(2, 2)).Should().Be(new CellAddr(2, 2));
        structure.TryGetMergeAt(covered, out var merge).Should().BeTrue();
        merge.Anchor.Should().Be(anchor);
    }

    [Fact]
    public void SplitDiagonal_on_anchor_passes_invariants()
    {
        var anchor = new CellAddr(0, 0);
        var grid = GridEditor.Merge(TableGrid.CreateEmpty(3, 3), anchor, 2, 2);

        var split = GridEditor.SplitDiagonal(grid, anchor, DiagonalDirection.BackSlashTLBR);

        split.Structure.Diagonals.Should().ContainKey(anchor);
        split.Structure.Diagonals[anchor].Parts.Should().HaveCount(2);
        GridInvariants.Validate(split).IsValid.Should().BeTrue();
    }

    [Fact]
    public void SplitDiagonal_on_covered_non_anchor_throws()
    {
        var grid = GridEditor.Merge(TableGrid.CreateEmpty(3, 3), new CellAddr(0, 0), 2, 2);

        var act = () => GridEditor.SplitDiagonal(grid, new CellAddr(1, 1), DiagonalDirection.BackSlashTLBR);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*非 Anchor*");
    }

    [Fact]
    public void SplitDiagonal_twice_on_same_cell_throws()
    {
        var addr = new CellAddr(0, 0);
        var grid = GridEditor.SplitDiagonal(TableGrid.CreateEmpty(3, 3), addr, DiagonalDirection.SlashBLTR);

        var act = () => GridEditor.SplitDiagonal(grid, addr, DiagonalDirection.BackSlashTLBR);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*已有斜线*");
    }

    [Fact]
    public void ClearDiagonal_removes_split()
    {
        var addr = new CellAddr(0, 0);
        var grid = GridEditor.SplitDiagonal(TableGrid.CreateEmpty(3, 3), addr, DiagonalDirection.BackSlashTLBR);

        var cleared = GridEditor.ClearDiagonal(grid, addr);

        cleared.Structure.Diagonals.Should().BeEmpty();
        GridInvariants.Validate(cleared).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ClearDiagonal_without_split_throws()
    {
        var grid = TableGrid.CreateEmpty(3, 3);

        var act = () => GridEditor.ClearDiagonal(grid, new CellAddr(0, 0));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*没有斜线*");
    }
}
