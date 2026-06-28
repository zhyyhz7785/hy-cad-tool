using FluentAssertions;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;
using Xunit;

namespace HyCAD.Tables.Tests;

public sealed class SetTrackSizeOpTests
{
    [Fact]
    public void SetTrackSizeOp_updates_row_height()
    {
        var grid = TableGrid.CreateEmpty(3, 3, rowHeight: 10, colWidth: 25);
        var log = new TableOpLog(grid);
        log.Apply(new SetTrackSizeOp(true, 1, 18));

        log.Current.Structure.Topology.Rows[1].Size.Should().Be(18);
        log.Current.Structure.Topology.Rows[0].Size.Should().Be(10);
    }

    [Fact]
    public void SetTrackSizeOp_updates_col_width()
    {
        var grid = TableGrid.CreateEmpty(3, 3, rowHeight: 10, colWidth: 25);
        var log = new TableOpLog(grid);
        log.Apply(new SetTrackSizeOp(false, 2, 40));

        log.Current.Structure.Topology.Cols[2].Size.Should().Be(40);
    }

    [Fact]
    public void BatchSetTrackSizeOp_updates_row_range_for_auto_fit()
    {
        var grid = TableGrid.CreateEmpty(4, 3, rowHeight: 10, colWidth: 25);
        var log = new TableOpLog(grid);
        var sizes = new[] { 12.5, 15.0, 18.25 };
        for (var i = 0; i < sizes.Length; i++)
            log.Apply(new SetTrackSizeOp(true, 1 + i, sizes[i]));

        log.Current.Structure.Topology.Rows[1].Size.Should().Be(12.5);
        log.Current.Structure.Topology.Rows[2].Size.Should().Be(15.0);
        log.Current.Structure.Topology.Rows[3].Size.Should().Be(18.25);
        log.Current.Structure.Topology.Rows[0].Size.Should().Be(10);
    }
}

public sealed class SetRoleOpTests
{
    [Fact]
    public void SetRoleOp_writes_and_clears_role()
    {
        var addr = new CellAddr(1, 1);
        var log = new TableOpLog(TableGrid.CreateEmpty(3, 3));

        log.Apply(new SetRoleOp(addr, CellRole.Value));
        log.Current.Structure.Roles[addr].Should().Be(CellRole.Value);

        log.Apply(new SetRoleOp(addr, null));
        log.Current.Structure.Roles.ContainsKey(addr).Should().BeFalse();
    }
}
