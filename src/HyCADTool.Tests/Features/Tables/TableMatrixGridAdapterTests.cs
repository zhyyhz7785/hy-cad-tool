using HyCAD.Tables;
using HyCAD.Tables.Samples;
using HyCAD.Tables.Structure;
using HyCADTool.Features.Tables.Presentation;
using Xunit;

namespace HyCADTool.Tests.Features.Tables
{
    public sealed class TableMatrixGridAdapterTests
    {
        [Fact]
        public void BuildMatrix_Empty5x4_HasTwentyCellsAndFourColumns()
        {
            var grid = TableGrid.CreateEmpty(5, 4);
            var matrix = TableMatrixGridAdapter.BuildMatrix(grid);

            Assert.Equal(5, matrix.RowCount);
            Assert.Equal(4, matrix.ColCount);
            Assert.Equal(5, matrix.Rows.Count);
            Assert.Equal("A", ColumnHeaderFormatter.Format(0));
            Assert.Equal("D", ColumnHeaderFormatter.Format(3));

            var cellCount = 0;
            foreach (var row in matrix.Rows)
            {
                Assert.Equal(4, row.Cells.Count);
                foreach (var cell in row.Cells)
                {
                    cellCount++;
                    Assert.False(cell.IsHidden);
                    Assert.True(cell.IsEditable);
                    Assert.Equal(string.Empty, cell.Text);
                }
            }

            Assert.Equal(20, cellCount);
        }

        [Fact]
        public void BuildMatrix_PersonnelTable_HasHiddenAndEditableCells()
        {
            var grid = TableSamples.BuildPersonnelTable();
            var matrix = TableMatrixGridAdapter.BuildMatrix(grid);

            Assert.True(matrix.RowCount > 0);
            Assert.True(matrix.ColCount > 0);

            var hasHidden = false;
            var hasEditable = false;
            var hasAnchorText = false;

            foreach (var row in matrix.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    if (cell.IsHidden)
                        hasHidden = true;
                    if (cell.IsEditable)
                        hasEditable = true;
                    if (!cell.IsHidden && !string.IsNullOrEmpty(cell.Text))
                        hasAnchorText = true;
                }
            }

            Assert.True(hasHidden, "人员表应含 merge 从属格");
            Assert.True(hasEditable, "人员表应含可编辑 Value 格");
            Assert.True(hasAnchorText, "人员表 anchor 格应有文本");
        }

        [Fact]
        public void BuildMatrix_HiddenCellsAreNotEditable()
        {
            var grid = TableSamples.BuildPersonnelTable();
            var matrix = TableMatrixGridAdapter.BuildMatrix(grid);

            foreach (var row in matrix.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    if (cell.IsHidden)
                        Assert.False(cell.IsEditable);
                }
            }
        }
    }
}
