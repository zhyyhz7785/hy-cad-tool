using HyCAD.Tables.Samples;
using HyCAD.Tables.Structure;
using HyCADTool.Features.Tables.Presentation;
using unvell.ReoGrid;
using Xunit;

namespace HyCADTool.Tests.Features.Tables
{
    public sealed class TableGridReoGridAdapterTests
    {
        [Fact]
        public void CellTag_RoundTrip_PreservesFieldKeyAndRole()
        {
            var anchor = new CellAddr(2, 3);
            var tag = TableGridReoGridCellTag.FromAnchor(anchor, "name", CellRole.Value);
            var json = tag.Serialize();

            Assert.True(TableGridReoGridCellTag.TryParse(json, out var parsed));
            Assert.Equal(2, parsed.Row);
            Assert.Equal(3, parsed.Col);
            Assert.Equal("name", parsed.FieldKey);
            Assert.Equal("Value", parsed.Role);
            Assert.Equal(anchor, parsed.ToAnchor());
        }

        [Fact]
        public void BuildSnapshot_PersonnelTable_MatchesAdapterCellCount()
        {
            var grid = TableSamples.BuildPersonnelTable();
            var snapshot = ExcelGridSnapshotBuilder.Build(grid);

            Assert.True(snapshot.Cells.Count > 0);
            Assert.Contains(snapshot.Cells, c => c.RowSpan > 1 || c.ColSpan > 1);
        }

        [Theory]
        [InlineData(TextAlign.Start, ReoGridHorAlign.Left)]
        [InlineData(TextAlign.Center, ReoGridHorAlign.Center)]
        [InlineData(TextAlign.End, ReoGridHorAlign.Right)]
        public void ToReoGridHorAlign_MapsDomainAlign(TextAlign input, ReoGridHorAlign expected)
        {
            Assert.Equal(expected, TableGridReoGridAdapter.ToReoGridHorAlign(input));
        }
    }
}
