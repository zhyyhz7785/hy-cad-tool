using FluentAssertions;
using HyCAD.Tables.Formulas;
using HyCAD.Tables.Structure;
using Xunit;

namespace HyCAD.Tables.Tests;

public sealed class FormulaReferenceTests
{
    private sealed class TestFormulaContext : IFormulaContext
    {
        private readonly Dictionary<CellAddr, string> _cells = new();

        public TestFormulaContext Set(CellAddr addr, string text)
        {
            _cells[addr] = text;
            return this;
        }

        public double GetNumber(CellAddr addr)
        {
            if (!_cells.TryGetValue(addr, out var text) || string.IsNullOrWhiteSpace(text))
                return 0;

            if (FormulaError.TryParseDisplay(text, out var code))
                throw new FormulaException(code);

            if (!double.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                throw new FormulaException(FormulaErrorCode.Value);
            }

            return value;
        }

        public bool TryGetNumber(CellAddr addr, out double value)
        {
            value = 0;
            if (!_cells.TryGetValue(addr, out var text) || string.IsNullOrWhiteSpace(text))
                return false;

            if (FormulaError.TryParseDisplay(text, out _))
                return false;

            return double.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value);
        }
    }

    [Fact]
    public void Evaluate_cell_reference_without_context_returns_ref_error()
    {
        FormulaEvaluator.Evaluate("A1+1").ToDisplay().Should().Be("#REF!");
    }

    [Fact]
    public void Evaluate_cell_addition()
    {
        var ctx = new TestFormulaContext()
            .Set(new CellAddr(0, 0), "2")
            .Set(new CellAddr(1, 1), "3");

        FormulaEvaluator.Evaluate("A1+B2", ctx).Value.Should().Be(5);
    }

    [Fact]
    public void Evaluate_sum_range_and_mixed_args()
    {
        var ctx = new TestFormulaContext()
            .Set(new CellAddr(0, 0), "1")
            .Set(new CellAddr(1, 0), "2")
            .Set(new CellAddr(2, 0), "3")
            .Set(new CellAddr(0, 1), "4");

        FormulaEvaluator.Evaluate("SUM(A1:A3)", ctx).Value.Should().Be(6);
        FormulaEvaluator.Evaluate("SUM(A1:A2,B1,5)", ctx).Value.Should().Be(12);
    }

    [Fact]
    public void Aggregate_ignores_empty_and_text()
    {
        var ctx = new TestFormulaContext()
            .Set(new CellAddr(0, 0), "1")
            .Set(new CellAddr(1, 0), "text")
            .Set(new CellAddr(2, 0), "");

        FormulaEvaluator.Evaluate("SUM(A1:A3)", ctx).Value.Should().Be(1);
        FormulaEvaluator.Evaluate("COUNT(A1:A3)", ctx).Value.Should().Be(1);
        FormulaEvaluator.Evaluate("AVERAGE(A1:A3)", ctx).Value.Should().Be(1);
    }
}
