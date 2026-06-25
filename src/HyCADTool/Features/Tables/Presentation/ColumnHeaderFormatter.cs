namespace HyCADTool.Features.Tables.Presentation
{
    /// <summary>
    /// Excel 列头：0→A, 1→B, … 25→Z, 26→AA。
    /// </summary>
    public static class ColumnHeaderFormatter
    {
        public static string Format(int columnIndex)
        {
            if (columnIndex < 0)
                return string.Empty;

            var letters = string.Empty;
            var index = columnIndex;
            do
            {
                letters = (char)('A' + (index % 26)) + letters;
                index = index / 26 - 1;
            }
            while (index >= 0);

            return letters;
        }
    }
}
