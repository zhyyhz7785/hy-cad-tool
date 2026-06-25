using HyCADTool.Features.Tables.Presentation;
using Xunit;

namespace HyCADTool.Tests.Features.Tables
{
    public sealed class ColumnHeaderFormatterTests
    {
        [Theory]
        [InlineData(0, "A")]
        [InlineData(1, "B")]
        [InlineData(25, "Z")]
        [InlineData(26, "AA")]
        public void Format_returnsExcelLetters(int index, string expected)
        {
            Assert.Equal(expected, ColumnHeaderFormatter.Format(index));
        }
    }
}
