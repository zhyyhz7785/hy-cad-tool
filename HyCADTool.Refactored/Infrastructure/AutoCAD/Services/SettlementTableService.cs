using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Models.Settlement;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 在 AutoCAD 中绘制沉降计算结果表格
    /// </summary>
    public static class SettlementTableService
    {
        /// <summary>
        /// 创建沉降计算结果 Table 对象
        /// </summary>
        public static Table CreateResultTable(
            SettlementResult result,
            Database db,
            double scale)
        {
            var layers = result.LayerResults;
            int dataRows = layers.Count;
            int cols = 8;
            // 表头 + 数据行 + 3 汇总行（s', ψ_s, s）
            int totalRows = 1 + dataRows + 3;

            var table = new Table();
            table.TableStyle = db.Tablestyle;
            table.SetSize(totalRows, cols);
            table.SetRowHeight(2.5 * scale);
            table.SetColumnWidth(8 * scale);

            // 调整部分列宽
            table.Columns[0].Width = 4 * scale;   // 序号
            table.Columns[1].Width = 5 * scale;   // 地层
            table.Columns[2].Width = 6 * scale;   // z(m)
            table.Columns[3].Width = 7 * scale;   // α
            table.Columns[4].Width = 7 * scale;   // ᾱ
            table.Columns[5].Width = 10 * scale;  // z·ᾱ差
            table.Columns[6].Width = 6 * scale;   // Es
            table.Columns[7].Width = 8 * scale;   // Δs'

            double textHeight = scale;

            // 表头
            string[] headers = { "#", "地层", "z(m)", "α", "ᾱ", "z·ᾱ差(m)", "Es(MPa)", "Δs'(mm)" };
            for (int c = 0; c < cols; c++)
            {
                table.Cells[0, c].TextString = headers[c];
                table.Cells[0, c].TextHeight = textHeight;
                table.Cells[0, c].Alignment = CellAlignment.MiddleCenter;
            }

            // 数据行
            for (int i = 0; i < dataRows; i++)
            {
                var r = layers[i];
                int row = i + 1;

                table.Cells[row, 0].TextString = r.Index.ToString();
                table.Cells[row, 1].TextString = r.LayerId;
                table.Cells[row, 2].TextString = r.Zi.ToString("F2");
                table.Cells[row, 3].TextString = r.Alpha.ToString("F4");
                table.Cells[row, 4].TextString = r.AlphaBar.ToString("F4");
                table.Cells[row, 5].TextString = r.ZAlphaBarDiff.ToString("F4");
                table.Cells[row, 6].TextString = r.Es.ToString("F1");
                table.Cells[row, 7].TextString = r.DeltaS.ToString("F3");

                for (int c = 0; c < cols; c++)
                {
                    table.Cells[row, c].TextHeight = textHeight;
                    table.Cells[row, c].Alignment = CellAlignment.MiddleCenter;
                }

                // 超出计算深度的行用灰色
                if (!r.WithinDepth)
                {
                    for (int c = 0; c < cols; c++)
                        table.Cells[row, c].ContentColor = Autodesk.AutoCAD.Colors.Color.FromRgb(160, 160, 160);
                }
            }

            // 汇总行
            int sumRow1 = dataRows + 1;
            int sumRow2 = dataRows + 2;
            int sumRow3 = dataRows + 3;

            // 理论沉降
            SetSummaryRow(table, sumRow1, cols, textHeight,
                $"理论沉降 s' = {result.TheoreticalSettlement:F2} mm",
                $"计算深度 zn = {result.CalculationDepth:F1} m");

            // 经验系数
            string psiInfo = result.PsiE < 1.0
                ? $"ψs={result.PsiS:F3}, ψe={result.PsiE:F3}"
                : $"ψs = {result.PsiS:F3}";
            SetSummaryRow(table, sumRow2, cols, textHeight,
                psiInfo,
                $"Ēs = {result.EquivalentEs:F1} MPa");

            // 最终沉降
            SetSummaryRow(table, sumRow3, cols, textHeight,
                $"最终沉降 s = {result.FinalSettlement:F2} mm", "");

            table.GenerateLayout();
            return table;
        }

        private static void SetSummaryRow(Table table, int row, int cols, double textHeight,
            string leftText, string rightText)
        {
            // 合并左侧列
            if (cols > 4)
            {
                table.MergeCells(CellRange.Create(table, row, 0, row, 3));
                table.Cells[row, 0].TextString = leftText;
                table.Cells[row, 0].TextHeight = textHeight;
                table.Cells[row, 0].Alignment = CellAlignment.MiddleLeft;

                if (!string.IsNullOrEmpty(rightText))
                {
                    table.MergeCells(CellRange.Create(table, row, 4, row, cols - 1));
                    table.Cells[row, 4].TextString = rightText;
                    table.Cells[row, 4].TextHeight = textHeight;
                    table.Cells[row, 4].Alignment = CellAlignment.MiddleLeft;
                }
            }
        }
    }
}
