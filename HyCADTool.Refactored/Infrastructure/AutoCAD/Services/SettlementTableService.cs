using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Models.Settlement;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 在 AutoCAD 中绘制沉降计算结果表格（表 5-7 格式）
    /// </summary>
    public static class SettlementTableService
    {
        public static Table CreateResultTable(
            SettlementResult result,
            Database db,
            double scale)
        {
            var layers = result.LayerResults;
            int dataRows = layers.Count;
            int cols = 11;
            int totalRows = 1 + dataRows + 3;

            var table = new Table();
            table.TableStyle = db.Tablestyle;
            table.SetSize(totalRows, cols);

            // AutoCAD 默认表格样式可能会自动将首行合并为 Title，
            // 这里显式解除初始合并，避免表头只显示第一列。
            for (int r = 0; r < totalRows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    try
                    {
                        var range = table.Cells[r, c].GetMergeRange();
                        if (range.TopRow != range.BottomRow || range.LeftColumn != range.RightColumn)
                            table.UnmergeCells(range);
                    }
                    catch
                    {
                        // 某些图纸样式下未合并时也可能抛异常，忽略即可。
                    }
                }
            }

            table.SetRowHeight(2.5 * scale);
            table.SetColumnWidth(6 * scale);

            table.Columns[0].Width = 3 * scale;    // #
            table.Columns[1].Width = 5 * scale;    // 地层
            table.Columns[2].Width = 5 * scale;    // z(m)
            table.Columns[3].Width = 5 * scale;    // z/0.5b
            table.Columns[4].Width = 6 * scale;    // ᾱ（角点）
            table.Columns[5].Width = 6 * scale;    // 4ᾱ（中心）
            table.Columns[6].Width = 8 * scale;    // 4ᾱz(mm)
            table.Columns[7].Width = 8 * scale;    // Δ4ᾱz
            table.Columns[8].Width = 5 * scale;    // p₀/Es
            table.Columns[9].Width = 6 * scale;    // Δs'
            table.Columns[10].Width = 6 * scale;   // Σ

            double textHeight = scale;

            string[] headers =
            {
                "#",
                "地层",
                "z(m)",
                "z/0.5b",
                Overline("a"),
                "4" + Overline("a"),
                "4" + Overline("a") + "z",
                "Δ4" + Overline("a") + "z",
                "p₀/Es",
                "Δs'",
                "Σ(mm)"
            };
            for (int c = 0; c < cols; c++)
            {
                table.Cells[0, c].TextString = headers[c];
                table.Cells[0, c].TextHeight = textHeight;
                table.Cells[0, c].Alignment = CellAlignment.MiddleCenter;
            }

            for (int i = 0; i < dataRows; i++)
            {
                var r = layers[i];
                int row = i + 1;

                table.Cells[row, 0].TextString = r.Index.ToString();
                table.Cells[row, 1].TextString = r.LayerId;
                table.Cells[row, 2].TextString = r.Zi.ToString("F2");
                table.Cells[row, 3].TextString = r.NHalf.ToString("F2");
                table.Cells[row, 4].TextString = r.AlphaBarCorner.ToString("F4");
                table.Cells[row, 5].TextString = r.AlphaBar.ToString("F4");
                table.Cells[row, 6].TextString = r.AlphaBarZi.ToString("F1");
                table.Cells[row, 7].TextString = r.ZAlphaBarDiff.ToString("F1");
                table.Cells[row, 8].TextString = r.P0overEs.ToString("F4");
                table.Cells[row, 9].TextString = r.DeltaS.ToString("F2");
                table.Cells[row, 10].TextString = r.CumulativeDeltaS.ToString("F2");

                for (int c = 0; c < cols; c++)
                {
                    table.Cells[row, c].TextHeight = textHeight;
                    table.Cells[row, c].Alignment = CellAlignment.MiddleCenter;
                }

                if (!r.WithinDepth)
                {
                    for (int c = 0; c < cols; c++)
                        table.Cells[row, c].ContentColor = Autodesk.AutoCAD.Colors.Color.FromRgb(160, 160, 160);
                }
            }

            int sumRow1 = dataRows + 1;
            int sumRow2 = dataRows + 2;
            int sumRow3 = dataRows + 3;

            SetSummaryRow(table, sumRow1, cols, textHeight,
                $"s' = {result.TheoreticalSettlement:F2} mm  zn = {result.CalculationDepth:F1} m",
                $"{Overline("E")}s = {result.EquivalentEs:F2} MPa");

            string psiInfo = result.PsiE < 1.0
                ? $"ψs={result.PsiS:F3}  ψe={result.PsiE:F3}"
                : $"ψs = {result.PsiS:F3}";
            SetSummaryRow(table, sumRow2, cols, textHeight,
                psiInfo, "");

            SetSummaryRow(table, sumRow3, cols, textHeight,
                $"最终沉降 s = {result.FinalSettlement:F2} mm", "");

            table.GenerateLayout();
            return table;
        }

        private static void SetSummaryRow(Table table, int row, int cols, double textHeight,
            string leftText, string rightText)
        {
            int mid = cols / 2;
            if (cols > 3)
            {
                table.MergeCells(CellRange.Create(table, row, 0, row, mid));
                table.Cells[row, 0].TextString = PadCellText(leftText);
                table.Cells[row, 0].TextHeight = textHeight;
                table.Cells[row, 0].Alignment = CellAlignment.MiddleLeft;

                if (!string.IsNullOrEmpty(rightText))
                {
                    table.MergeCells(CellRange.Create(table, row, mid + 1, row, cols - 1));
                    table.Cells[row, mid + 1].TextString = PadCellText(rightText);
                    table.Cells[row, mid + 1].TextHeight = textHeight;
                    table.Cells[row, mid + 1].Alignment = CellAlignment.MiddleLeft;
                }
            }
        }

        private static string Overline(string text) => $"\\O{text}\\o";

        private static string PadCellText(string text, int hardSpaceCount = 2)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return string.Concat(System.Linq.Enumerable.Repeat("\\~", hardSpaceCount)) + text;
        }
    }
}
