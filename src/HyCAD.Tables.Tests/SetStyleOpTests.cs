using FluentAssertions;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;
using Xunit;

namespace HyCAD.Tables.Tests;

public sealed class SetStyleOpTests
{
    [Fact]
    public void SetStyleOp_updates_HAlign_on_anchor()
    {
        var initial = TableGrid.CreateEmpty(3, 3);
        var addr = new CellAddr(1, 1);
        var style = new CellStyle(HAlign: TextAlign.Center, VAlign: TextAlign.End);

        var log = new TableOpLog(initial);
        log.Apply(new SetStyleOp(addr, style));

        var anchor = log.Current.Structure.GetAnchorOf(addr);
        log.Current.Structure.Styles[anchor].HAlign.Should().Be(TextAlign.Center);
        log.Current.Structure.Styles[anchor].VAlign.Should().Be(TextAlign.End);
    }

    [Fact]
    public void SetStyleOp_on_merge_member_writes_anchor()
    {
        var anchor = new CellAddr(0, 0);
        var member = new CellAddr(1, 1);
        var grid = GridEditor.Merge(TableGrid.CreateEmpty(3, 3), anchor, 2, 2);
        var style = new CellStyle(HAlign: TextAlign.End);

        var result = new SetStyleOp(member, style).Apply(grid);

        result.Structure.Styles[anchor].HAlign.Should().Be(TextAlign.End);
    }

    [Fact]
    public void SetCellWrapOp_sets_and_clears_AllowWrap()
    {
        var addr = new CellAddr(0, 0);
        var log = new TableOpLog(TableGrid.CreateEmpty(3, 3));

        log.Apply(new SetCellWrapOp(addr, true));
        GridEditor.GetCellAllowWrap(log.Current, addr).Should().BeTrue();

        log.Apply(new SetCellWrapOp(addr, false));
        GridEditor.GetCellAllowWrap(log.Current, addr).Should().BeFalse();
    }
}
