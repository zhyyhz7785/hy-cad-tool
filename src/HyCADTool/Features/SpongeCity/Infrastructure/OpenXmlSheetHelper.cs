using System;
using System.Collections.Generic;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace HyCADTool.Features.SpongeCity.Infrastructure
{
    /// <summary>
    /// 轻量 OpenXml Spreadsheet 写入助手。
    /// 用 inline string / number / formula 写单元格；样式按预置 7 槽（见 BuildStylesheet）。
    /// 公式与值并存：写公式时同时设置 CachedResultValue=0（让 Excel 打开后自动重算）。
    /// </summary>
    public static class OpenXmlSheetHelper
    {
        // ── 样式槽位（与 BuildStylesheet 顺序一致）──
        public const uint S_NORMAL = 0;
        public const uint S_TITLE = 1;
        public const uint S_SUBTITLE = 2;
        public const uint S_HEADER = 3;
        public const uint S_INPUT = 4;
        public const uint S_CALC = 5;
        public const uint S_RESULT = 6;
        public const uint S_SMALL = 7;
        public const uint S_BOLD = 8;
        public const uint S_PERCENT_INPUT = 9;
        public const uint S_PERCENT_CALC = 10;
        public const uint S_DEC2_CALC = 11;
        public const uint S_DEC2_RESULT = 12;
        public const uint S_INT_INPUT = 13;
        public const uint S_INT_CALC = 14;

        /// <summary>把列号（1-based）转 A/B/.../AA。</summary>
        public static string Col(int col)
        {
            string s = "";
            while (col > 0)
            {
                int r = (col - 1) % 26;
                s = (char)('A' + r) + s;
                col = (col - 1) / 26;
            }
            return s;
        }

        public static string Addr(int row, int col) => Col(col) + row;

        /// <summary>
        /// 写一个单元格（数值/公式/字符串自动判断）。
        /// - value 是 double / int → CellValue=number；
        /// - value 是 string 以 "=" 开头 → CellFormula；
        /// - 其它 string → InlineString（避免 SharedStringTable，简化代码）。
        /// </summary>
        public static Cell Write(Worksheet ws, Row row, int col, object value, uint styleIdx = S_NORMAL)
        {
            var cell = new Cell { CellReference = Addr((int)row.RowIndex.Value, col), StyleIndex = styleIdx };
            if (value == null)
            {
                // empty
            }
            else if (value is string s && s.StartsWith("="))
            {
                cell.CellFormula = new CellFormula(s.Substring(1));
                cell.CellValue = new CellValue("0");
            }
            else if (value is double d)
            {
                cell.DataType = CellValues.Number;
                cell.CellValue = new CellValue(d.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            else if (value is int i)
            {
                cell.DataType = CellValues.Number;
                cell.CellValue = new CellValue(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            else if (value is bool b)
            {
                cell.DataType = CellValues.Boolean;
                cell.CellValue = new CellValue(b ? "1" : "0");
            }
            else
            {
                cell.DataType = CellValues.InlineString;
                cell.InlineString = new InlineString(new Text(value.ToString()) { Space = SpaceProcessingModeValues.Preserve });
            }

            // 按 CellReference 顺序插入（OpenXml 要求列升序）
            Cell after = null;
            foreach (var c in row.Elements<Cell>())
            {
                if (CompareCellRef(c.CellReference, cell.CellReference) > 0) { after = c; break; }
            }
            if (after != null) row.InsertBefore(cell, after);
            else row.AppendChild(cell);
            return cell;
        }

        public static Row EnsureRow(SheetData data, uint rowIndex)
        {
            foreach (var r in data.Elements<Row>())
            {
                if (r.RowIndex == rowIndex) return r;
                if (r.RowIndex > rowIndex)
                {
                    var nr = new Row { RowIndex = rowIndex };
                    data.InsertBefore(nr, r);
                    return nr;
                }
            }
            var newRow = new Row { RowIndex = rowIndex };
            data.AppendChild(newRow);
            return newRow;
        }

        /// <summary>设置一行的高度（pt）。</summary>
        public static void SetRowHeight(SheetData data, uint rowIndex, double pt)
        {
            var r = EnsureRow(data, rowIndex);
            r.Height = pt;
            r.CustomHeight = true;
        }

        /// <summary>合并单元格 A:B → ws.MergeCells。</summary>
        public static void Merge(Worksheet ws, int r1, int c1, int r2, int c2)
        {
            var mc = ws.GetFirstChild<MergeCells>();
            if (mc == null)
            {
                mc = new MergeCells();
                var dataAfter = ws.GetFirstChild<SheetData>();
                ws.InsertAfter(mc, dataAfter);
            }
            mc.AppendChild(new MergeCell { Reference = $"{Addr(r1, c1)}:{Addr(r2, c2)}" });
        }

        /// <summary>设置列宽。widths = (col 1-based, 字符宽度) 列表。</summary>
        public static void SetColumnWidths(Worksheet ws, IEnumerable<(int col, double width)> widths)
        {
            var cols = new Columns();
            foreach (var (col, width) in widths)
            {
                cols.AppendChild(new Column
                {
                    Min = (uint)col,
                    Max = (uint)col,
                    Width = width,
                    CustomWidth = true,
                });
            }
            // Columns 必须在 SheetData 之前
            var data = ws.GetFirstChild<SheetData>();
            if (data != null) ws.InsertBefore(cols, data);
            else ws.AppendChild(cols);
        }

        /// <summary>预置 15 个样式（NormFmt + 12 业务样式）。</summary>
        public static Stylesheet BuildStylesheet()
        {
            var fonts = new Fonts(
                new Font(new FontSize { Val = 10 }, new FontName { Val = "微软雅黑" }),                                        // 0 NORMAL
                new Font(new FontSize { Val = 14 }, new FontName { Val = "微软雅黑" }, new Bold()),                            // 1 TITLE
                new Font(new FontSize { Val = 11 }, new FontName { Val = "微软雅黑" }, new Bold()),                            // 2 SUBTITLE
                new Font(new FontSize { Val = 10 }, new FontName { Val = "微软雅黑" }, new Bold(), new Color { Rgb = "FFFFFFFF" }), // 3 HEADER WHITE
                new Font(new FontSize { Val = 10 }, new FontName { Val = "微软雅黑" }, new Bold(), new Color { Rgb = "FF1F4E79" }), // 4 INPUT BLUE
                new Font(new FontSize { Val = 10 }, new FontName { Val = "微软雅黑" }, new Bold(), new Color { Rgb = "FFC00000" }), // 5 RESULT RED
                new Font(new FontSize { Val = 9 }, new FontName { Val = "微软雅黑" }, new Color { Rgb = "FF666666" })           // 6 SMALL GRAY
            ) { Count = 7 };

            var fills = new Fills(
                new Fill(new PatternFill { PatternType = PatternValues.None }),                                                 // 0
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),                                              // 1
                new Fill(new PatternFill(new ForegroundColor { Rgb = "FF4472C4" }) { PatternType = PatternValues.Solid }),     // 2 HEADER (blue)
                new Fill(new PatternFill(new ForegroundColor { Rgb = "FFDAEEF3" }) { PatternType = PatternValues.Solid }),     // 3 INPUT
                new Fill(new PatternFill(new ForegroundColor { Rgb = "FFF2F2F2" }) { PatternType = PatternValues.Solid }),     // 4 CALC
                new Fill(new PatternFill(new ForegroundColor { Rgb = "FFFFF2CC" }) { PatternType = PatternValues.Solid })      // 5 RESULT
            ) { Count = 6 };

            Border MakeBorder()
            {
                return new Border(
                    new LeftBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = "FF999999" } },
                    new RightBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = "FF999999" } },
                    new TopBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = "FF999999" } },
                    new BottomBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = "FF999999" } },
                    new DiagonalBorder());
            }
            var borders = new Borders(new Border(), MakeBorder()) { Count = 2 };

            var numFmts = new NumberingFormats(
                new NumberingFormat { NumberFormatId = 164, FormatCode = "0.0%" },
                new NumberingFormat { NumberFormatId = 165, FormatCode = "0.00" },
                new NumberingFormat { NumberFormatId = 166, FormatCode = "#,##0" },
                new NumberingFormat { NumberFormatId = 167, FormatCode = "0.000" }
            ) { Count = 4 };

            CellFormat CF(uint font, uint fill, uint border, uint numFmt, bool center = false, bool right = false)
            {
                var cf = new CellFormat
                {
                    FontId = font,
                    FillId = fill,
                    BorderId = border,
                    NumberFormatId = numFmt,
                    ApplyFont = true,
                    ApplyFill = fill > 0,
                    ApplyBorder = border > 0,
                    ApplyNumberFormat = numFmt > 0,
                    ApplyAlignment = true,
                };
                cf.Alignment = new Alignment
                {
                    Horizontal = right ? HorizontalAlignmentValues.Right
                                       : (center ? HorizontalAlignmentValues.Center : HorizontalAlignmentValues.Left),
                    Vertical = VerticalAlignmentValues.Center,
                    WrapText = true,
                };
                return cf;
            }

            var cellFormats = new CellFormats(
                CF(0, 0, 0, 0),                                  // 0 NORMAL
                CF(1, 0, 0, 0, center: true),                    // 1 TITLE
                CF(2, 0, 0, 0),                                  // 2 SUBTITLE
                CF(3, 2, 1, 0, center: true),                    // 3 HEADER
                CF(4, 3, 1, 0, right: true),                     // 4 INPUT
                CF(0, 4, 1, 0, right: true),                     // 5 CALC
                CF(5, 5, 1, 0, right: true),                     // 6 RESULT
                CF(6, 0, 0, 0),                                  // 7 SMALL
                CF(0, 0, 1, 0),                                  // 8 BOLD/border-plain
                CF(4, 3, 1, 164, right: true),                   // 9 PERCENT INPUT
                CF(0, 4, 1, 164, right: true),                   // 10 PERCENT CALC
                CF(0, 4, 1, 165, right: true),                   // 11 DEC2 CALC
                CF(5, 5, 1, 165, right: true),                   // 12 DEC2 RESULT
                CF(4, 3, 1, 166, right: true),                   // 13 INT INPUT
                CF(0, 4, 1, 166, right: true)                    // 14 INT CALC
            ) { Count = 15 };

            return new Stylesheet(numFmts, fonts, fills, borders, cellFormats);
        }

        private static int CompareCellRef(StringValue a, StringValue b)
        {
            // 简化：按列字母 + 行号比较（A1 < B1 < AA1 < A2）
            string aRef = a?.Value ?? "";
            string bRef = b?.Value ?? "";
            // 提取列字母与行号
            void Split(string r, out int col, out int row)
            {
                int i = 0;
                while (i < r.Length && r[i] >= 'A' && r[i] <= 'Z') i++;
                string letters = r.Substring(0, i);
                col = 0;
                foreach (var ch in letters) col = col * 26 + (ch - 'A' + 1);
                int.TryParse(r.Substring(i), out row);
            }
            Split(aRef, out var ac, out var ar);
            Split(bRef, out var bc, out var br);
            if (ar != br) return ar.CompareTo(br);
            return ac.CompareTo(bc);
        }
    }
}
