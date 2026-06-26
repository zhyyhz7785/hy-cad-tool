using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using HyCAD.Tables;
using HyCAD.Tables.Operations;
using HyCADTool.App.Bootstrap;
using HyCADTool.Features.SpongeCity.Infrastructure;

namespace HyCADTool.Features.Tables.Presentation
{
    /// <summary>
    /// TableGrid 简易 xlsx 导入导出（工程表级别，不保证 Excel 全保真）。
    /// </summary>
    public static class TableGridXlsxAdapter
    {
        public static void Export(TableGrid grid, string filePath)
        {
            OpenXmlAssemblyBootstrap.EnsureLoaded();

            if (grid == null)
                throw new ArgumentNullException(nameof(grid));
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath 不能为空。", nameof(filePath));

            var snapshot = ExcelGridSnapshotBuilder.Build(grid);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? ".");

            using (var doc = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook))
            {
                var wbPart = doc.AddWorkbookPart();
                wbPart.Workbook = new Workbook();
                var stylesPart = wbPart.AddNewPart<WorkbookStylesPart>();
                stylesPart.Stylesheet = OpenXmlSheetHelper.BuildStylesheet();
                stylesPart.Stylesheet.Save();

                var sheetPart = wbPart.AddNewPart<WorksheetPart>();
                var ws = new Worksheet();
                var sheetData = new SheetData();
                ws.AppendChild(sheetData);

                var widths = new List<(int col, double width)>();
                for (var c = 0; c < snapshot.ColCount; c++)
                    widths.Add((c + 1, Math.Max(8, snapshot.ColWidthsMm[c] * 0.35)));
                OpenXmlSheetHelper.SetColumnWidths(ws, widths);

                for (var r = 0; r < snapshot.RowCount; r++)
                {
                    var rowIndex = (uint)(r + 1);
                    OpenXmlSheetHelper.SetRowHeight(sheetData, rowIndex, Math.Max(12, snapshot.RowHeightsMm[r] * 0.75));
                }

                foreach (var cell in snapshot.Cells)
                {
                    var rowIndex = (uint)(cell.Anchor.Row + 1);
                    var colIndex = cell.Anchor.Col + 1;
                    var row = OpenXmlSheetHelper.EnsureRow(sheetData, rowIndex);
                    OpenXmlSheetHelper.Write(ws, row, colIndex, cell.Text ?? string.Empty);

                    if (cell.RowSpan > 1 || cell.ColSpan > 1)
                    {
                        OpenXmlSheetHelper.Merge(
                            ws,
                            cell.Anchor.Row + 1,
                            colIndex,
                            cell.Anchor.Row + cell.RowSpan,
                            colIndex + cell.ColSpan - 1);
                    }
                }

                sheetPart.Worksheet = ws;
                sheetPart.Worksheet.Save();

                var sheets = wbPart.Workbook.AppendChild(new Sheets());
                sheets.AppendChild(new Sheet
                {
                    Id = wbPart.GetIdOfPart(sheetPart),
                    SheetId = 1,
                    Name = "Sheet1",
                });
                wbPart.Workbook.Save();
            }
        }

        public static TableGrid Import(string filePath, double defaultRowHeightMm, double defaultColWidthMm)
        {
            OpenXmlAssemblyBootstrap.EnsureLoaded();

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("未找到 xlsx 文件。", filePath);

            using (var doc = SpreadsheetDocument.Open(filePath, false))
            {
                var wbPart = doc.WorkbookPart;
                var sheet = wbPart.Workbook.Sheets.Elements<Sheet>().FirstOrDefault();
                if (sheet?.Id?.Value == null)
                    throw new InvalidOperationException("xlsx 中没有工作表。");

                var wsPart = (WorksheetPart)wbPart.GetPartById(sheet.Id.Value);
                var sheetData = wsPart.Worksheet.GetFirstChild<SheetData>();
                if (sheetData == null)
                    return TableGrid.CreateEmpty(1, 1, defaultRowHeightMm, defaultColWidthMm);

                var rows = sheetData.Elements<Row>().ToList();
                var maxRow = rows.Count == 0 ? 1 : (int)rows.Max(r => r.RowIndex?.Value ?? 1);
                var maxCol = 1;
                foreach (var row in rows)
                {
                    foreach (var cell in row.Elements<Cell>())
                    {
                        if (TryParseCellReference(cell.CellReference?.Value, out _, out var col))
                            maxCol = Math.Max(maxCol, col);
                    }
                }

                var grid = TableGrid.CreateEmpty(maxRow, maxCol, defaultRowHeightMm, defaultColWidthMm);
                foreach (var row in rows)
                {
                    var rowIndex = (int)(row.RowIndex?.Value ?? 0);
                    if (rowIndex <= 0)
                        continue;

                    foreach (var cell in row.Elements<Cell>())
                    {
                        if (!TryParseCellReference(cell.CellReference?.Value, out var excelRow, out var excelCol))
                            continue;

                        var addr = new HyCAD.Tables.Structure.CellAddr(excelRow - 1, excelCol - 1);
                        if (addr.Row >= maxRow || addr.Col >= maxCol)
                            continue;

                        var text = ReadCellText(cell, wbPart.SharedStringTablePart);
                        grid = GridEditor.SetValue(grid, addr, new HyCAD.Tables.Data.CellValue(text));
                    }
                }

                return grid;
            }
        }

        private static string ReadCellText(Cell cell, SharedStringTablePart sharedStrings)
        {
            if (cell == null)
                return string.Empty;

            if (cell.DataType?.Value == CellValues.SharedString)
            {
                if (int.TryParse(cell.CellValue?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx)
                    && sharedStrings?.SharedStringTable != null)
                {
                    var item = sharedStrings.SharedStringTable.ElementAt(idx);
                    return item?.InnerText ?? string.Empty;
                }
            }

            if (cell.DataType?.Value == CellValues.InlineString)
                return cell.InlineString?.InnerText ?? string.Empty;

            return cell.CellValue?.Text ?? string.Empty;
        }

        private static bool TryParseCellReference(string reference, out int row, out int col)
        {
            row = 0;
            col = 0;
            if (string.IsNullOrWhiteSpace(reference))
                return false;

            var i = 0;
            while (i < reference.Length && char.IsLetter(reference[i]))
                i++;

            if (i == 0 || i >= reference.Length)
                return false;

            var letters = reference.Substring(0, i);
            foreach (var ch in letters)
                col = col * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);

            return int.TryParse(reference.Substring(i), NumberStyles.Integer, CultureInfo.InvariantCulture, out row);
        }
    }
}
