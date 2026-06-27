using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Samples;
using HyCAD.Tables.Structure;
using HyCADTool.Features.Tables.Presentation;
using HyCADTool.Features.Tables.Services;
using HyCADTool.Features.Tables.TableApp;
using HyCADTool.Features.Tables.ViewModels;
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
        public void MmToDisplayPx_PreservesRowColAspectRatio()
        {
            const double rowMm = 10;
            const double colMm = 25;

            var rowPx = UniverGridSnapshotMapper.MmToRowPx(rowMm);
            var colPx = UniverGridSnapshotMapper.MmToColPx(colMm);

            Assert.Equal(rowMm / colMm, rowPx / colPx, 6);
        }

        [Theory]
        [InlineData(12.5)]
        [InlineData(30.25)]
        [InlineData(10)]
        [InlineData(25)]
        public void MmToDisplayPx_RoundTripsWithinTolerance(double mm)
        {
            var rowPx = UniverGridSnapshotMapper.MmToRowPx(mm);
            var colPx = UniverGridSnapshotMapper.MmToColPx(mm);

            var rowBack = UniverGridSnapshotMapper.DisplayPxToMm(rowPx, mm);
            var colBack = UniverGridSnapshotMapper.DisplayPxToMm(colPx, mm);

            Assert.InRange(rowBack, mm - 0.01, mm + 0.01);
            Assert.InRange(colBack, mm - 0.01, mm + 0.01);
        }

        [Fact]
        public void MmToDisplayPx_PersonnelTable_UniformAspectRatio()
        {
            var snapshot = UniverGridSnapshotMapper.FromTableGrid(TableSamples.BuildPersonnelTable());
            const double expectedRatio = 10.0 / 25.0;

            foreach (var rowMm in snapshot.RowHeightsMm)
            {
                foreach (var colMm in snapshot.ColWidthsMm)
                {
                    var rowPx = UniverGridSnapshotMapper.MmToRowPx(rowMm);
                    var colPx = UniverGridSnapshotMapper.MmToColPx(colMm);
                    Assert.Equal(expectedRatio, rowPx / colPx, 6);
                }
            }
        }

        [Fact]
        public void FromTableGrid_Personnel_PreservesMergeRegions()
        {
            var snapshot = UniverGridSnapshotMapper.FromTableGrid(TableSamples.BuildPersonnelTable());

            Assert.Equal(7, snapshot.RowCount);
            Assert.Equal(7, snapshot.ColCount);

            var title = FindCell(snapshot, 0, 0);
            Assert.NotNull(title);
            Assert.Equal(7, title.ColSpan);
            Assert.Equal(1, title.RowSpan);

            var photo = FindCell(snapshot, 1, 6);
            Assert.NotNull(photo);
            Assert.Equal(3, photo.RowSpan);
            Assert.Equal(1, photo.ColSpan);

            var hometown = FindCell(snapshot, 3, 1);
            Assert.NotNull(hometown);
            Assert.Equal(3, hometown.ColSpan);

            Assert.Equal(snapshot.Cells.Count, snapshot.Cells.Select(c => (c.Row, c.Col)).Distinct().Count());
        }

        [Fact]
        public void FromTableGrid_Personnel_PopulatesRoleAndFieldKey()
        {
            var snapshot = UniverGridSnapshotMapper.FromTableGrid(TableSamples.BuildPersonnelTable());

            Assert.Equal("title", FindCell(snapshot, 0, 0).Role);
            Assert.Equal("label", FindCell(snapshot, 1, 0).Role);

            var nameValue = FindCell(snapshot, 1, 1);
            Assert.Equal("value", nameValue.Role);
            Assert.Equal("name", nameValue.FieldKey);

            Assert.Equal("photoSlot", FindCell(snapshot, 1, 6).Role);
            Assert.True(FindCell(snapshot, 1, 6).IsPhotoSlot);
        }

        [Fact]
        public void FromTableGrid_Personnel_PopulatesStyleFields()
        {
            var snapshot = UniverGridSnapshotMapper.FromTableGrid(TableSamples.BuildPersonnelTable());

            foreach (var cell in snapshot.Cells)
            {
                Assert.NotNull(cell.HAlign);
                Assert.NotNull(cell.VAlign);
                Assert.NotNull(cell.TextHeightMm);
                Assert.NotNull(cell.AllowWrap);
                Assert.NotNull(cell.Orientation);
            }

            var vertical = FindCell(snapshot, 4, 0);
            Assert.Equal("verticalStacked", vertical.Orientation);
        }

        [Fact]
        public void ApplyTextValues_DoesNotReverseInferStyleOrRole()
        {
            var grid = TableSamples.BuildPersonnelTable();
            var opLog = new TableOpLog(grid);
            var snapshot = UniverGridSnapshotMapper.FromTableGrid(opLog.Current);

            var nameCell = FindCell(snapshot, 1, 1);
            nameCell.Text = "李四";
            nameCell.Role = "label";
            nameCell.FieldKey = "tampered";
            nameCell.HAlign = "end";
            nameCell.IsPhotoSlot = true;

            UniverGridSnapshotMapper.ApplyTextValues(opLog, snapshot);

            var after = UniverGridSnapshotMapper.FromTableGrid(opLog.Current);
            var afterName = FindCell(after, 1, 1);
            Assert.Equal("李四", afterName.Text);
            Assert.Equal("value", afterName.Role);
            Assert.Equal("name", afterName.FieldKey);
            Assert.False(afterName.IsPhotoSlot);
        }

        [Fact]
        public void ApplyTextValues_UpdatesEditableCell()
        {
            var grid = TableSamples.BuildPersonnelTable();
            var opLog = new TableOpLog(grid);
            var snapshot = UniverGridSnapshotMapper.FromTableGrid(opLog.Current);
            var target = snapshot.Cells.FirstOrDefault(c => c.Editable);
            Assert.NotNull(target);

            target.Text = "UniverEdited";
            UniverGridSnapshotMapper.ApplyTextValues(opLog, snapshot);

            var addr = new CellAddr(target.Row, target.Col);
            var value = GridEditor.GetValue(opLog.Current, addr);
            Assert.Equal("UniverEdited", TableSummaryBuilder.FormatCellValue(value));
        }

        [Fact]
        public void ApplyTextValues_SkipsNonEditableCells()
        {
            var grid = TableSamples.BuildPersonnelTable();
            var opLog = new TableOpLog(grid);
            var snapshot = UniverGridSnapshotMapper.FromTableGrid(opLog.Current);
            var label = FindCell(snapshot, 1, 0);
            Assert.NotNull(label);
            Assert.False(label.Editable);

            var before = TableSummaryBuilder.FormatCellValue(GridEditor.GetValue(opLog.Current, new CellAddr(1, 0)));
            label.Text = "篡改标签";
            UniverGridSnapshotMapper.ApplyTextValues(opLog, snapshot);

            var after = TableSummaryBuilder.FormatCellValue(GridEditor.GetValue(opLog.Current, new CellAddr(1, 0)));
            Assert.Equal(before, after);
            Assert.Equal("姓名", after);
        }

        [Fact]
        public void ApplyTextValues_VerticalStacked_StripsNewlines()
        {
            var grid = TableSamples.BuildPersonnelTable();
            var opLog = new TableOpLog(grid);
            var addr = new CellAddr(1, 1);
            opLog.Apply(new SetStyleOp(addr, new CellStyle(Orientation: TextOrientation.VerticalStacked)));

            var snapshot = UniverGridSnapshotMapper.FromTableGrid(opLog.Current);
            var target = FindCell(snapshot, 1, 1);
            Assert.NotNull(target);
            Assert.True(target.Editable);

            target.Text = "李\n四";
            UniverGridSnapshotMapper.ApplyTextValues(opLog, snapshot);

            var value = TableSummaryBuilder.FormatCellValue(GridEditor.GetValue(opLog.Current, addr));
            Assert.Equal("李四", value);
        }

        [Fact]
        public void RoundTrip_FromGrid_ApplySnapshot_MatchesEditableValues()
        {
            var opLog = new TableOpLog(TableSamples.BuildPersonnelTable());
            var snapshot = UniverGridSnapshotMapper.FromTableGrid(opLog.Current);

            FindCell(snapshot, 1, 1).Text = "李四";
            FindCell(snapshot, 1, 3).Text = "女";
            FindCell(snapshot, 3, 1).Text = "上海市";

            UniverGridSnapshotMapper.ApplyTextValues(opLog, snapshot);

            Assert.Equal("李四", ReadCellText(opLog.Current, 1, 1));
            Assert.Equal("女", ReadCellText(opLog.Current, 1, 3));
            Assert.Equal("上海市", ReadCellText(opLog.Current, 3, 1));
            Assert.Equal("姓名", ReadCellText(opLog.Current, 1, 0));
        }

        [Fact]
        public void PersonnelFixture_IsInSyncWithDomain()
        {
            var expected = UniverGridSnapshotMapper.FromTableGrid(TableSamples.BuildPersonnelTable());
            var fixturePath = GetPersonnelFixturePath();
            Assert.True(File.Exists(fixturePath), $"fixture 不存在: {fixturePath}");

            var actual = UniverGridSnapshotMapper.Parse(File.ReadAllText(fixturePath));
            Assert.NotNull(actual);
            AssertSnapshotCellsEquivalent(expected, actual);
        }

        [Fact(Skip = "手动：刷新 personnel fixture 时取消 Skip 并运行一次")]
        public void ExportPersonnelUniverFixture()
        {
            var grid = TableSamples.BuildPersonnelTable();
            var json = JsonConvert.SerializeObject(UniverGridSnapshotMapper.FromTableGrid(grid), Formatting.Indented);
            var fixturePath = GetPersonnelFixturePath();
            Directory.CreateDirectory(Path.GetDirectoryName(fixturePath));
            File.WriteAllText(fixturePath, json);
            Assert.True(File.Exists(fixturePath));
            Assert.Contains("人员基本情况表", json);
        }

        [Fact]
        public void RequestPublish_WithoutExportHandler_SetsStatusAndDoesNotHang()
        {
            var vm = new TableEditorViewModel();
            using (var bridge = new UniverTableEditorHostBridge(vm))
            {
                bridge.RequestPublish();
                Assert.Contains("未就绪", vm.StatusMessage);
            }
        }

        [Fact]
        public void RequestPublish_WithExportHandler_InvokesExport()
        {
            var vm = new TableEditorViewModel();
            using (var bridge = new UniverTableEditorHostBridge(vm))
            {
                var invoked = false;
                bridge.SetExportSnapshotHandler(() => invoked = true);

                bridge.RequestPublish();

                Assert.True(invoked);
            }
        }

        [Fact]
        public void RequestPublish_WithoutTable_SetsStatusAfterExport()
        {
            var vm = new TableEditorViewModel();
            using (var bridge = new UniverTableEditorHostBridge(vm))
            {
                bridge.SetExportSnapshotHandler(() => { });
                bridge.RequestPublish();
                bridge.CompleteExportSnapshot(null);

                Assert.Contains("请先加载或拾取表格", vm.StatusMessage);
            }
        }

        [Fact]
        public void ApplyTextValues_SkipsOutOfGridCells()
        {
            var grid = TableSamples.BuildPersonnelTable();
            var opLog = new TableOpLog(grid);
            var snapshot = UniverGridSnapshotMapper.FromTableGrid(opLog.Current);
            snapshot.Cells.Add(new UniverGridCellSnapshot
            {
                Row = 0,
                Col = 7,
                Text = "越界",
                Editable = true,
            });

            UniverGridSnapshotMapper.ApplyTextValues(opLog, snapshot);

            Assert.Equal("人员基本情况表", ReadCellText(opLog.Current, 0, 0));
        }

        [Fact]
        public void EnsureGridFits_ExtendsTopology_ThenApplyTextValues_WritesExtendedCell()
        {
            var grid = TableSamples.BuildPersonnelTable();
            var opLog = new TableOpLog(grid);
            Assert.Equal(7, opLog.Current.Structure.Topology.RowCount);

            var snapshot = new UniverGridSnapshot
            {
                RowCount = 10,
                ColCount = 7,
                Cells =
                {
                    new UniverGridCellSnapshot
                    {
                        Row = 8,
                        Col = 1,
                        Text = "扩展行文字",
                        Editable = true,
                    },
                },
            };

            UniverGridSnapshotMapper.EnsureGridFits(opLog, snapshot);
            Assert.Equal(10, opLog.Current.Structure.Topology.RowCount);

            UniverGridSnapshotMapper.ApplyTextValues(opLog, snapshot);
            Assert.Equal("扩展行文字", ReadCellText(opLog.Current, 8, 1));
        }

        [Fact]
        public void Crop_PersonnelTable_PreservesSubRegionLabels()
        {
            var source = TableSamples.BuildPersonnelTable();
            var cropped = TableGridCropper.Crop(source, 1, 0, 3, 2);

            Assert.Equal(3, cropped.Structure.Topology.RowCount);
            Assert.Equal(3, cropped.Structure.Topology.ColCount);
            Assert.Equal("姓名", ReadCellText(cropped, 0, 0));
            Assert.Equal("张三", ReadCellText(cropped, 0, 1));
            Assert.Equal("北京市", ReadCellText(cropped, 2, 1));
        }

        [Fact]
        public void ExportImportXlsx_PersonnelTable_PreservesSampleText()
        {
            var grid = TableSamples.BuildPersonnelTable();
            var path = Path.Combine(Path.GetTempPath(), "hytable-univer-test.xlsx");
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
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static UniverGridCellSnapshot FindCell(UniverGridSnapshot snapshot, int row, int col) =>
            snapshot.Cells.FirstOrDefault(c => c.Row == row && c.Col == col);

        private static string ReadCellText(HyCAD.Tables.TableGrid grid, int row, int col) =>
            TableSummaryBuilder.FormatCellValue(GridEditor.GetValue(grid, new CellAddr(row, col)));

        private static string GetPersonnelFixturePath()
        {
            var repoRoot = Path.GetFullPath(
                Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            return Path.Combine(
                repoRoot,
                "src",
                "HyCADTool.UniverEditor",
                "Web",
                "public",
                "fixtures",
                "personnel-snapshot.json");
        }

        private static void AssertSnapshotCellsEquivalent(UniverGridSnapshot expected, UniverGridSnapshot actual)
        {
            Assert.Equal(expected.RowCount, actual.RowCount);
            Assert.Equal(expected.ColCount, actual.ColCount);

            var expectedCells = NormalizeCells(expected.Cells);
            var actualCells = NormalizeCells(actual.Cells);
            Assert.Equal(expectedCells.Count, actualCells.Count);

            for (var i = 0; i < expectedCells.Count; i++)
            {
                var e = expectedCells[i];
                var a = actualCells[i];
                Assert.Equal(e.Row, a.Row);
                Assert.Equal(e.Col, a.Col);
                Assert.Equal(e.RowSpan, a.RowSpan);
                Assert.Equal(e.ColSpan, a.ColSpan);
                Assert.Equal(e.Text, a.Text);
                Assert.Equal(e.Editable, a.Editable);
                Assert.Equal(e.Role, a.Role);
                Assert.Equal(e.FieldKey, a.FieldKey);
                Assert.Equal(e.HAlign, a.HAlign);
                Assert.Equal(e.VAlign, a.VAlign);
            }
        }

        private static List<UniverGridCellSnapshot> NormalizeCells(IEnumerable<UniverGridCellSnapshot> cells) =>
            cells.OrderBy(c => c.Row).ThenBy(c => c.Col).ToList();
    }
}
