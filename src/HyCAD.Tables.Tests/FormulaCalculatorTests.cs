using FluentAssertions;
using HyCAD.Tables;
using HyCAD.Tables.Data;
using HyCAD.Tables.Formulas;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;
using Xunit;

namespace HyCAD.Tables.Tests;

public sealed class FormulaCalculatorTests
{
    [Theory]
    [InlineData("1+2*3", 7)]
    [InlineData("(1+2)*3", 9)]
    [InlineData("-5+2", -3)]
    [InlineData("2^3^2", 512)]
    [InlineData("5!", 120)]
    [InlineData("50%", 0.5)]
    [InlineData("1.5e2", 150)]
    public void Evaluate_basic_arithmetic(string formula, double expected)
    {
        FormulaEvaluator.Evaluate(formula).Value.Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void Evaluate_scientific_functions_and_constants()
    {
        FormulaEvaluator.Evaluate("SIN(PI/2)").Value.Should().BeApproximately(1, 1e-9);
        FormulaEvaluator.Evaluate("LOG(8,2)").Value.Should().BeApproximately(3, 1e-9);
        FormulaEvaluator.Evaluate("SQRT(2)").Value.Should().BeApproximately(Math.Sqrt(2), 1e-9);
        FormulaEvaluator.Evaluate("=EXP(1)").Value.Should().BeApproximately(Math.E, 1e-9);
    }

    [Fact]
    public void Evaluate_reports_excel_errors()
    {
        FormulaEvaluator.Evaluate("1/0").ToDisplay().Should().Be("#DIV/0!");
        FormulaEvaluator.Evaluate("UNKNOWN(1)").ToDisplay().Should().Be("#NAME?");
        FormulaEvaluator.Evaluate("1+").ToDisplay().Should().Be("#VALUE!");
        FormulaEvaluator.Evaluate("(-1)!").ToDisplay().Should().Be("#NUM!");
        FormulaEvaluator.Evaluate("MOD(1,0)").ToDisplay().Should().Be("#DIV/0!");
        FormulaEvaluator.Evaluate("LN(-1)").ToDisplay().Should().Be("#NUM!");
    }

    [Fact]
    public void Recalculate_propagates_dependency_error_without_throwing()
    {
        var grid = TableGrid.CreateEmpty(2, 2);
        grid = GridEditor.SetValue(grid, new CellAddr(0, 0), new CellValue("#VALUE!"));
        grid = GridEditor.SetValue(
            grid,
            new CellAddr(0, 1),
            new CellValue("", Kind: CellValueKind.Formula, Formula: "A1+1"));

        var act = () => FormulaService.Recalculate(grid);

        act.Should().NotThrow();
        GridEditor.GetValue(act(), new CellAddr(0, 1)).Text.Should().Be("#VALUE!");
    }
}
