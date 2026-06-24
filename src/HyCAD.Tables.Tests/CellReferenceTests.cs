using FluentAssertions;
using HyCAD.Tables.Formulas;
using HyCAD.Tables.Structure;
using Xunit;

namespace HyCAD.Tables.Tests;

public sealed class CellReferenceTests
{
    [Theory]
    [InlineData(0, 0, "A1")]
    [InlineData(0, 25, "Z1")]
    [InlineData(0, 26, "AA1")]
    [InlineData(9, 27, "AB10")]
    public void FormatA1_round_trips(int row, int col, string expected)
    {
        var addr = new CellAddr(row, col);
        CellReference.FormatA1(addr).Should().Be(expected);
        CellReference.TryParseA1(expected, out var parsed).Should().BeTrue();
        parsed.Should().Be(addr);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1A")]
    [InlineData("A")]
    [InlineData("A0")]
    public void TryParseA1_rejects_invalid(string text)
    {
        CellReference.TryParseA1(text, out _).Should().BeFalse();
    }
}
