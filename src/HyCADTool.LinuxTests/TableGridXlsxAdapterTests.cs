using System;
using System.IO;
using System.Linq;
using HyCAD.Tables;
using HyCAD.Tables.Data;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;
using HyCADTool.Features.Tables.Presentation;
using Xunit;

namespace HyCADTool.LinuxTests
{
    public class TableGridXlsxAdapterTests
    {
        [Fact]
        public void Mm_to_points_uses_72_per_inch()
        {
            Assert.Equal(72, TableGridXlsxAdapter.MmToPoints(25.4), 9);
            Assert.Equal(25.4, TableGridXlsxAdapter.PointsToMm(72), 9);
        }

        [Fact]
        public void Column_width_round_trips()
        {
            const double mm = 30;
            double excel = TableGridXlsxAdapter.MmToExcelColumnWidth(mm);
            Assert.Equal(mm, TableGridXlsxAdapter.ExcelColumnWidthToMm(excel), 6);
        }

        [Fact]
        public void Merge_reference_parses_range()
        {
            Assert.True(TableGridXlsxAdapter.TryParseMergeReference("A1:C2", out var r1, out var c1, out var r2, out var c2));
            Assert.Equal(1, r1);
            Assert.Equal(1, c1);
            Assert.Equal(2, r2);
            Assert.Equal(3, c2);
        }

        [Fact]
        public void Export_import_keeps_text_merges_and_track_sizes()
        {
            var grid = TableGrid.CreateEmpty(3, 3, 8, 20);
            grid = GridEditor.SetValue(grid, new CellAddr(0, 0), new CellValue("标题"));
            grid = GridEditor.SetValue(grid, new CellAddr(1, 0), new CellValue("A1"));
            grid = GridEditor.SetValue(grid, new CellAddr(2, 2), new CellValue("角"));
            grid = GridEditor.Merge(grid, new CellAddr(0, 0), 1, 2);
            grid = GridEditor.SetRowHeight(grid, 0, 18);
            grid = GridEditor.SetColWidth(grid, 0, 32);

            string path = Path.Combine(Path.GetTempPath(), "hy-xlsx-" + Guid.NewGuid().ToString("N") + ".xlsx");
            try
            {
                TableGridXlsxAdapter.Export(grid, path);
                var imported = TableGridXlsxAdapter.Import(path, 8, 20);

                Assert.Equal(3, imported.Structure.Topology.RowCount);
                Assert.Equal(3, imported.Structure.Topology.ColCount);
                Assert.Equal("标题", GridEditor.GetValue(imported, new CellAddr(0, 0)).Text);
                Assert.Equal("A1", GridEditor.GetValue(imported, new CellAddr(1, 0)).Text);
                Assert.Equal("角", GridEditor.GetValue(imported, new CellAddr(2, 2)).Text);

                Assert.True(imported.Structure.TryGetMergeAt(new CellAddr(0, 0), out var merge));
                Assert.Equal(1, merge.RowSpan);
                Assert.Equal(2, merge.ColSpan);

                Assert.InRange(imported.Structure.Topology.Rows[0].Size, 17.5, 18.5);
                Assert.InRange(imported.Structure.Topology.Cols[0].Size, 31.5, 32.5);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Fact]
        public void Import_ignores_far_empty_row_numbers()
        {
            Assert.True(TableGridXlsxAdapter.MaxImportRows < 100000);
            Assert.True(TableGridXlsxAdapter.MaxImportCols < 2000);
        }
    }
}
