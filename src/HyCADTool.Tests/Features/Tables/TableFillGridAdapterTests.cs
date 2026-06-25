using HyCAD.Tables;
using HyCAD.Tables.Samples;
using HyCAD.Tables.Structure;
using HyCADTool.Features.Tables.Presentation;
using Xunit;

namespace HyCADTool.Tests.Features.Tables
{
    public sealed class TableFillGridAdapterTests
    {
        [Fact]
        public void BuildRows_Empty5x4_HasTwentyReadOnlyEmptyCells()
        {
            var grid = TableGrid.CreateEmpty(5, 4);
            var rows = TableFillGridAdapter.BuildRows(grid);

            Assert.Equal(20, rows.Count);
            Assert.All(rows, r =>
            {
                Assert.False(r.IsEditable);
                Assert.Equal(string.Empty, r.Text);
            });
        }

        [Fact]
        public void BuildRows_PersonnelTable_MatchesVisibleAnchorCount()
        {
            var grid = TableSamples.BuildPersonnelTable();
            var expected = CountVisibleAnchors(grid.Structure);

            var rows = TableFillGridAdapter.BuildRows(grid);

            Assert.Equal(expected, rows.Count);
            Assert.Contains(rows, r => r.FieldKey == "name" && r.IsEditable);
            Assert.Contains(rows, r => r.Address == new CellAddr(1, 0) && !r.IsEditable);
        }

        private static int CountVisibleAnchors(GridStructure structure)
        {
            var topology = structure.Topology;
            var count = 0;
            for (var row = 0; row < topology.RowCount; row++)
            {
                for (var col = 0; col < topology.ColCount; col++)
                {
                    var addr = new CellAddr(row, col);
                    if (structure.IsHidden(addr))
                        continue;
                    if (addr != structure.GetAnchorOf(addr))
                        continue;
                    count++;
                }
            }

            return count;
        }
    }
}
