using System.Linq;
using HyCAD.Tables.Samples;
using HyCADTool.Features.Tables.Presentation;
using Newtonsoft.Json;
using Xunit;

namespace HyCADTool.Tests.Features.Tables
{
    public sealed class TableGridUniverAdapterTests
    {
        [Fact]
        public void ToJson_FromPersonnelTable_RoundTripsRowColCounts()
        {
            var grid = TableSamples.BuildPersonnelTable();
            var json = UniverGridSnapshotMapper.ToJson(grid);
            var snapshot = UniverGridSnapshotMapper.Parse(json);

            Assert.NotNull(snapshot);
            Assert.True(snapshot.RowCount > 0);
            Assert.True(snapshot.ColCount > 0);
            Assert.Equal(snapshot.RowCount, snapshot.RowHeightsMm.Count);
            Assert.Equal(snapshot.ColCount, snapshot.ColWidthsMm.Count);
            Assert.Contains(snapshot.Cells, c => c.RowSpan > 1 || c.ColSpan > 1);
        }

        [Fact]
        public void ApplyTextValues_UpdatesEditableCell()
        {
            var grid = TableSamples.BuildPersonnelTable();
            var opLog = new HyCAD.Tables.Operations.TableOpLog(grid);
            var snapshot = UniverGridSnapshotMapper.FromTableGrid(opLog.Current);
            var target = snapshot.Cells.FirstOrDefault(c => c.Editable);
            Assert.NotNull(target);

            target.Text = "UniverEdited";
            UniverGridSnapshotMapper.ApplyTextValues(opLog, snapshot);

            var addr = new HyCAD.Tables.Structure.CellAddr(target.Row, target.Col);
            var value = HyCAD.Tables.Operations.GridEditor.GetValue(opLog.Current, addr);
            Assert.Equal("UniverEdited", HyCADTool.Features.Tables.TableApp.TableSummaryBuilder.FormatCellValue(value));
        }

        [Fact]
        public void ExportImportXlsx_PersonnelTable_PreservesSampleText()
        {
            var grid = TableSamples.BuildPersonnelTable();
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hytable-univer-test.xlsx");
            try
            {
                TableGridXlsxAdapter.Export(grid, path);
                var imported = TableGridXlsxAdapter.Import(path, 10, 25);
                var roundTrip = ExcelGridSnapshotBuilder.Build(imported);
                Assert.Contains(roundTrip.Cells, c => c.Text == "张三");
                Assert.Contains(roundTrip.Cells, c => c.Text == "人员基本情况表");
            }
            finally
            {
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }
        }
    }
}
