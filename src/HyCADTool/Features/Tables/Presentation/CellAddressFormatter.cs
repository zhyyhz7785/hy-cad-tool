namespace HyCADTool.Features.Tables.Presentation
{
    /// <summary>
    /// Excel 单元格地址：row=0,col=0 → A1（1-based 行 + 列字母）。
    /// </summary>
    public static class CellAddressFormatter
    {
        public static string Format(int row, int col)
        {
            if (row < 0 || col < 0)
                return string.Empty;

            return ColumnHeaderFormatter.Format(col) + (row + 1).ToString();
        }
    }
}
