using FluentAssertions;
using HyCAD.Tables.Data;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;
using Xunit;

namespace HyCAD.Tables.Tests;

public sealed class TableOpLogTests
{
    private static TableGrid CreateInitialWithFieldKey()
    {
        var addr = new CellAddr(2, 1);
        return TableGrid.CreateEmpty(4, 4) with
        {
            Structure = GridStructure.CreateEmpty(GridTopology.CreateUniform(4, 4, 10, 25)) with
            {
                FieldIndex = new Dictionary<string, CellAddr> { ["name"] = addr }
            }
        };
    }

    private static void AssertValid(TableOpLog log) =>
        GridInvariants.Validate(log.Current).IsValid.Should().BeTrue();

    [Fact]
    public void Apply_sequence_undo_stepwise_returns_to_initial_then_redo_to_final()
    {
        var initial = CreateInitialWithFieldKey();
        var log = new TableOpLog(initial);

        log.Apply(new MergeOp(new CellAddr(0, 0), 2, 2));
        var afterMerge = log.Current;
        AssertValid(log);

        log.Apply(new InsertRowOp(0));
        var afterInsert = log.Current;
        AssertValid(log);

        log.Apply(new SetValueByFieldOp("name", new CellValue("张三")));
        var afterSetField = log.Current;
        AssertValid(log);

        log.Apply(new SplitDiagonalOp(
            new CellAddr(3, 3),
            DiagonalDirection.BackSlashTLBR));
        var finalState = log.Current;
        AssertValid(log);

        log.Undo();
        log.Current.Should().BeEquivalentTo(afterSetField);
        AssertValid(log);

        log.Undo();
        log.Current.Should().BeEquivalentTo(afterInsert);
        AssertValid(log);

        log.Undo();
        log.Current.Should().BeEquivalentTo(afterMerge);
        AssertValid(log);

        log.Undo();
        log.Current.Should().BeEquivalentTo(initial);
        AssertValid(log);
        log.CanUndo.Should().BeFalse();

        log.Redo();
        log.Current.Should().BeEquivalentTo(afterMerge);
        AssertValid(log);

        log.Redo();
        log.Current.Should().BeEquivalentTo(afterInsert);
        AssertValid(log);

        log.Redo();
        log.Current.Should().BeEquivalentTo(afterSetField);
        AssertValid(log);

        log.Redo();
        log.Current.Should().BeEquivalentTo(finalState);
        AssertValid(log);
        log.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Undo_after_insert_row_restores_field_index_and_data_exactly()
    {
        var initial = CreateInitialWithFieldKey();
        var log = new TableOpLog(initial);

        log.Apply(new InsertRowOp(0));
        log.Undo();

        log.Current.Structure.FieldIndex.Should().BeEquivalentTo(initial.Structure.FieldIndex);
        log.Current.Data.Should().BeEquivalentTo(initial.Data);
        AssertValid(log);
    }

    [Fact]
    public void Apply_after_undo_truncates_redo_branch()
    {
        var log = new TableOpLog(TableGrid.CreateEmpty(3, 3));
        log.Apply(new SetValueOp(new CellAddr(0, 0), new CellValue("first")));
        log.Apply(new SetValueOp(new CellAddr(1, 1), new CellValue("second")));

        log.Undo();
        log.CanRedo.Should().BeTrue();

        log.Apply(new SetValueOp(new CellAddr(2, 2), new CellValue("branch")));
        log.CanRedo.Should().BeFalse();
        GridEditor.GetValue(log.Current, new CellAddr(2, 2)).Text.Should().Be("branch");
        log.Current.Data.Cells.Should().NotContainKey(new CellAddr(1, 1));
        AssertValid(log);
    }

    [Fact]
    public void Initial_state_cannot_undo_or_redo()
    {
        var log = new TableOpLog(TableGrid.CreateEmpty(3, 3));

        log.CanUndo.Should().BeFalse();
        log.CanRedo.Should().BeFalse();
        log.History.Should().BeEmpty();

        var undoAct = () => log.Undo();
        undoAct.Should().Throw<InvalidOperationException>().WithMessage("*撤销*");

        var redoAct = () => log.Redo();
        redoAct.Should().Throw<InvalidOperationException>().WithMessage("*重做*");
    }

    [Fact]
    public void History_reflects_applied_operations_and_shrinks_on_undo()
    {
        var log = new TableOpLog(TableGrid.CreateEmpty(3, 3));

        log.Apply(new InsertRowOp(0));
        log.Apply(new InsertColumnOp(1));
        log.Apply(new SetValueOp(new CellAddr(0, 0), new CellValue("x")));

        log.History.Should().HaveCount(3);
        log.History[0].Should().BeOfType<InsertRowOp>();
        log.History[1].Should().BeOfType<InsertColumnOp>();
        log.History[2].Should().BeOfType<SetValueOp>();

        log.Undo();
        log.History.Should().HaveCount(2);
    }

    [Fact]
    public void SetValueByField_after_insert_row_via_oplog_hits_remapped_cell()
    {
        var log = new TableOpLog(CreateInitialWithFieldKey());

        log.Apply(new InsertRowOp(0));
        log.Apply(new SetValueByFieldOp("name", new CellValue("李四")));

        log.Current.Structure.FieldIndex["name"].Should().Be(new CellAddr(3, 1));
        GridEditor.GetValueByField(log.Current, "name").Text.Should().Be("李四");
        AssertValid(log);
    }
}
