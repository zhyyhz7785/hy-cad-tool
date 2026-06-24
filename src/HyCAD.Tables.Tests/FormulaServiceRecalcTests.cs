using FluentAssertions;
using HyCAD.Tables.Data;
using HyCAD.Tables.Formulas;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Serialization;
using HyCAD.Tables.Structure;
using Xunit;

namespace HyCAD.Tables.Tests;

public sealed class FormulaServiceRecalcTests
{
    private static CellValue Formula(string formula) =>
        new("", Kind: CellValueKind.Formula, Formula: formula);

    private static TableGrid SetFormula(TableGrid grid, CellAddr addr, string formula, string? literal = null) =>
        GridEditor.SetValue(
            grid,
            addr,
            Formula(formula) with { Text = literal ?? string.Empty });

    private static TableGrid SetNumber(TableGrid grid, CellAddr addr, double value) =>
        GridEditor.SetValue(grid, addr, new CellValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Recalculate_evaluates_dependency_chain()
    {
        var grid = TableGrid.CreateEmpty(3, 3);
        grid = SetNumber(grid, new CellAddr(0, 0), 10);
        grid = SetFormula(grid, new CellAddr(0, 1), "A1+1");
        grid = SetFormula(grid, new CellAddr(0, 2), "B1*2");

        var result = FormulaService.Recalculate(grid);

        GridEditor.GetValue(result, new CellAddr(0, 1)).Text.Should().Be("11");
        GridEditor.GetValue(result, new CellAddr(0, 2)).Text.Should().Be("22");
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Recalculate_detects_circular_reference()
    {
        var grid = TableGrid.CreateEmpty(2, 2);
        grid = SetFormula(grid, new CellAddr(0, 0), "B1");
        grid = SetFormula(grid, new CellAddr(0, 1), "A1");

        var result = FormulaService.Recalculate(grid);

        GridEditor.GetValue(result, new CellAddr(0, 0)).Text.Should().Be("#REF!");
        GridEditor.GetValue(result, new CellAddr(0, 1)).Text.Should().Be("#REF!");
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Recalculate_resolves_merge_anchor_reference()
    {
        var grid = TableGrid.CreateEmpty(3, 3);
        grid = GridEditor.Merge(grid, new CellAddr(0, 0), 2, 2);
        grid = SetNumber(grid, new CellAddr(0, 0), 5);
        grid = SetFormula(grid, new CellAddr(2, 2), "A1*3");

        var result = FormulaService.Recalculate(grid);

        GridEditor.GetValue(result, new CellAddr(2, 2)).Text.Should().Be("15");
        GridInvariants.Validate(result).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Recalculate_after_json_round_trip_produces_same_results()
    {
        var grid = TableGrid.CreateEmpty(3, 3);
        grid = SetNumber(grid, new CellAddr(0, 0), 4);
        grid = SetFormula(grid, new CellAddr(0, 1), "SUM(A1,A1,2)");

        var recalculated = FormulaService.Recalculate(grid);
        var json = TableJson.SerializeGrid(recalculated);
        var restored = TableJson.DeserializeGrid(json);
        var again = FormulaService.Recalculate(restored);

        GridEditor.GetValue(again, new CellAddr(0, 1)).Text.Should().Be("10");
        GridInvariants.Validate(again).IsValid.Should().BeTrue();
    }
}
