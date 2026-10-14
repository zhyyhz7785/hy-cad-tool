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
using HyCAD.Tables.Structure;
using HyCADTool.App.Bootstrap;
using HyCADTool.Shared.Office;
using TableCellValue = HyCAD.Tables.Data.CellValue;

namespace HyCADTool.Features.Tables.Presentation
{
    /// <summary>
    /// TableGrid 简易 xlsx 导入导出（工程表级别，不保证 Excel 全保真）。
    /// </summary>
    public static class TableGridXlsxAdapter
    {
        internal const int MaxImportRows = 4096;
        internal const int MaxImportCols = 512;

        private const double MmPerInch = 25.4;
        private const double PointsPerInch = 72.0;
        private const double PixelsPerInch = 96.0;
        private const double ExcelCharPixels = 7.0;
        private const double ExcelWidthPaddingPixels = 5.0;

        private static readonly HashSet<int> BuiltInDateFormatIds = new HashSet<int>
        {
            14, 15, 16, 17, 18, 19, 20, 21, 22,
            27, 30, 36, 45, 46, 47, 50, 57
        };

        internal static double MmToPoints(double mm) => mm * PointsPerInch / MmPerInch;

        internal static double PointsToMm(double points) => points * MmPerInch / PointsPerInch;

        internal static double MmToExcelColumnWidth(double mm)
        {
            double pixels = mm * PixelsPerInch / MmPerInch;
            if (pixels <= ExcelWidthPaddingPixels)
                return Math.Max(0.1, pixels / ExcelCharPixels);
            return (pixels - ExcelWidthPaddingPixels) / ExcelCharPixels;
        }

        internal static double ExcelColumnWidthToMm(double excelWidth)
        {
            double pixels = excelWidth * ExcelCharPixels + ExcelWidthPaddingPixels;
            return pixels * MmPerInch / PixelsPerInch;
        }

        public static void Export(TableGrid grid, string filePath)
        {
            OpenXmlAssemblyBootstrap.EnsureLoaded();

            if (grid == null)
                throw new ArgumentNullException(nameof(grid));
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath 不能为空。", nameof(filePath));

            var topology = grid.Structure.Topology;
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
                for (var c = 0; c < topology.ColCount; c++)
                    widths.Add((c + 1, Math.Max(0.1, MmToExcelColumnWidth(topology.Cols[c].Size))));
                OpenXmlSheetHelper.SetColumnWidths(ws, widths);

                for (var r = 0; r < topology.RowCount; r++)
                {
                    var rowIndex = (uint)(r + 1);
                    OpenXmlSheetHelper.SetRowHeight(sheetData, rowIndex, Math.Max(1, MmToPoints(topology.Rows[r].Size)));
                }

                for (var row = 0; row < topology.RowCount; row++)
                {
                    for (var col = 0; col < topology.ColCount; col++)
                    {
                        var addr = new CellAddr(row, col);
                        if (grid.Structure.IsHidden(addr))
                            continue;
                        if (grid.Structure.GetAnchorOf(addr) != addr)
                            continue;

                        var rowSpan = 1;
                        var colSpan = 1;
                        if (grid.Structure.TryGetMergeAt(addr, out var merge))
                        {
                            rowSpan = merge.RowSpan;
                            colSpan = merge.ColSpan;
                        }

                        var rowIndex = (uint)(row + 1);
                        var colIndex = col + 1;
                        var excelRow = OpenXmlSheetHelper.EnsureRow(sheetData, rowIndex);
                        OpenXmlSheetHelper.Write(ws, excelRow, colIndex, GridEditor.GetValue(grid, addr).Text ?? string.Empty);

                        if (rowSpan > 1 || colSpan > 1)
                        {
                            OpenXmlSheetHelper.Merge(
                                ws,
                                row + 1,
                                colIndex,
                                row + rowSpan,
                                colIndex + colSpan - 1);
                        }
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
                var worksheet = wsPart.Worksheet;
                var sheetData = worksheet.GetFirstChild<SheetData>();
                if (sheetData == null)
                    return TableGrid.CreateEmpty(1, 1, defaultRowHeightMm, defaultColWidthMm);

                var shared = BuildSharedStringCache(wbPart.SharedStringTablePart);
                var stylesheet = wbPart.WorkbookStylesPart?.Stylesheet;

                int maxRow = 1;
                int maxCol = 1;
                var pendingCells = new List<(int row, int col, string text)>();

                foreach (var row in sheetData.Elements<Row>())
                {
                    foreach (var cell in row.Elements<Cell>())
                    {
                        if (!TryParseCellReference(cell.CellReference?.Value, out var excelRow, out var excelCol))
                            continue;

                        var text = ReadCellText(cell, shared, stylesheet);
                        if (string.IsNullOrEmpty(text) && excelRow > MaxImportRows)
                            continue;

                        if (excelRow > MaxImportRows || excelCol > MaxImportCols)
                            continue;

                        maxRow = Math.Max(maxRow, excelRow);
                        maxCol = Math.Max(maxCol, excelCol);
                        if (!string.IsNullOrEmpty(text))
                            pendingCells.Add((excelRow, excelCol, text));
                    }
                }

                var mergeSpecs = new List<(int r1, int c1, int r2, int c2)>();
                var mergeCells = worksheet.Elements<MergeCells>().FirstOrDefault()
                    ?? worksheet.GetFirstChild<MergeCells>();
                if (mergeCells != null)
                {
                    foreach (var merge in mergeCells.Elements<MergeCell>())
                    {
                        if (!TryParseMergeReference(merge.Reference?.Value, out var r1, out var c1, out var r2, out var c2))
                            continue;
                        if (r2 > MaxImportRows || c2 > MaxImportCols)
                            continue;
                        maxRow = Math.Max(maxRow, r2);
                        maxCol = Math.Max(maxCol, c2);
                        mergeSpecs.Add((r1, c1, r2, c2));
                    }
                }

                maxRow = Math.Min(Math.Max(1, maxRow), MaxImportRows);
                maxCol = Math.Min(Math.Max(1, maxCol), MaxImportCols);

                var cells = new Dictionary<CellAddr, TableCellValue>();
                foreach (var (excelRow, excelCol, text) in pendingCells)
                {
                    if (excelRow > maxRow || excelCol > maxCol)
                        continue;
                    cells[new CellAddr(excelRow - 1, excelCol - 1)] = new TableCellValue(text);
                }

                var empty = TableGrid.CreateEmpty(maxRow, maxCol, defaultRowHeightMm, defaultColWidthMm);
                var grid = TableGrid.FromStructure(empty.Structure, new HyCAD.Tables.Data.GridData(cells));

                foreach (var (r1, c1, r2, c2) in mergeSpecs)
                {
                    int rowSpan = r2 - r1 + 1;
                    int colSpan = c2 - c1 + 1;
                    if (rowSpan <= 1 && colSpan <= 1)
                        continue;
                    if (r1 < 1 || c1 < 1 || r2 > maxRow || c2 > maxCol)
                        continue;
                    try
                    {
                        grid = GridEditor.Merge(grid, new CellAddr(r1 - 1, c1 - 1), rowSpan, colSpan);
                    }
                    catch (InvalidOperationException)
                    {
                        // overlapping or out-of-policy merges: skip rather than fail the whole sheet
                    }
                }

                var columns = worksheet.GetFirstChild<Columns>();
                if (columns != null)
                {
                    foreach (var column in columns.Elements<Column>())
                    {
                        if (column.Width == null)
                            continue;
                        uint min = column.Min?.Value ?? 1;
                        uint max = column.Max?.Value ?? min;
                        double mm = ExcelColumnWidthToMm(column.Width.Value);
                        for (uint c = min; c <= max && c <= (uint)maxCol; c++)
                            grid = GridEditor.SetColWidth(grid, (int)c - 1, mm);
                    }
                }

                foreach (var row in sheetData.Elements<Row>())
                {
                    if (row.CustomHeight == null || !row.CustomHeight.Value || row.Height == null)
                        continue;
                    int rowIndex = (int)(row.RowIndex?.Value ?? 0);
                    if (rowIndex < 1 || rowIndex > maxRow)
                        continue;
                    grid = GridEditor.SetRowHeight(grid, rowIndex - 1, PointsToMm(row.Height.Value));
                }

                return grid;
            }
        }

        private static string[] BuildSharedStringCache(SharedStringTablePart sharedStrings)
        {
            if (sharedStrings?.SharedStringTable == null)
                return Array.Empty<string>();

            return sharedStrings.SharedStringTable
                .Elements<SharedStringItem>()
                .Select(item => item.InnerText ?? string.Empty)
                .ToArray();
        }

        private static string ReadCellText(Cell cell, string[] shared, Stylesheet stylesheet)
        {
            if (cell == null)
                return string.Empty;

            if (cell.DataType?.Value == CellValues.SharedString)
            {
                if (int.TryParse(cell.CellValue?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx)
                    && idx >= 0 && idx < shared.Length)
                    return shared[idx];
                return string.Empty;
            }

            if (cell.DataType?.Value == CellValues.InlineString)
                return cell.InlineString?.InnerText ?? string.Empty;

            if (cell.DataType?.Value == CellValues.Boolean)
                return cell.CellValue?.Text == "1" ? "TRUE" : "FALSE";

            var raw = cell.CellValue?.Text ?? string.Empty;
            if ((cell.DataType == null || cell.DataType.Value == CellValues.Number)
                && TryFormatExcelDate(cell, stylesheet, raw, out var dateText))
                return dateText;

            return raw;
        }

        private static bool TryFormatExcelDate(Cell cell, Stylesheet stylesheet, string raw, out string dateText)
        {
            dateText = null;
            if (stylesheet == null || string.IsNullOrWhiteSpace(raw))
                return false;
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial))
                return false;
            if (serial < 0 || serial > 2958465)
                return false;

            int numFmtId = 0;
            string formatCode = null;
            if (cell.StyleIndex != null)
            {
                var formats = stylesheet.CellFormats?.Elements<CellFormat>().ToList();
                int styleIndex = (int)cell.StyleIndex.Value;
                if (formats != null && styleIndex >= 0 && styleIndex < formats.Count)
                {
                    numFmtId = (int)(formats[styleIndex].NumberFormatId?.Value ?? 0);
                    formatCode = stylesheet.NumberingFormats?
                        .Elements<NumberingFormat>()
                        .FirstOrDefault(f => f.NumberFormatId != null && f.NumberFormatId.Value == (uint)numFmtId)
                        ?.FormatCode?.Value;
                }
            }

            if (!IsExcelDateFormat(numFmtId, formatCode))
                return false;

            try
            {
                var dt = DateTime.FromOADate(serial);
                dateText = dt.TimeOfDay.TotalSeconds < 1
                    ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool IsExcelDateFormat(int numberFormatId, string formatCode)
        {
            if (BuiltInDateFormatIds.Contains(numberFormatId))
                return true;
            if (string.IsNullOrEmpty(formatCode))
                return false;

            var lower = formatCode.ToLowerInvariant();
            if (lower.IndexOf("red", StringComparison.Ordinal) >= 0 && lower.IndexOf('y') < 0 && lower.IndexOf('d') < 0)
                return false;
            return lower.IndexOf('y') >= 0
                || lower.IndexOf('d') >= 0
                || (lower.IndexOf('m') >= 0 && (lower.IndexOf('h') >= 0 || lower.IndexOf('s') >= 0));
        }

        internal static bool TryParseMergeReference(string reference, out int r1, out int c1, out int r2, out int c2)
        {
            r1 = c1 = r2 = c2 = 0;
            if (string.IsNullOrWhiteSpace(reference))
                return false;

            var parts = reference.Split(':');
            if (parts.Length == 1)
            {
                if (!TryParseCellReference(parts[0], out r1, out c1))
                    return false;
                r2 = r1;
                c2 = c1;
                return true;
            }

            if (parts.Length != 2)
                return false;
            if (!TryParseCellReference(parts[0], out r1, out c1))
                return false;
            if (!TryParseCellReference(parts[1], out r2, out c2))
                return false;
            if (r2 < r1)
            {
                var tmp = r1;
                r1 = r2;
                r2 = tmp;
            }
            if (c2 < c1)
            {
                var tmp = c1;
                c1 = c2;
                c2 = tmp;
            }

            return true;
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
