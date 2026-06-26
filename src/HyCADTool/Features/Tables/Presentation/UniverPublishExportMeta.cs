namespace HyCADTool.Features.Tables.Presentation
{
    /// <summary>Univer 选区矩形（0-based 闭区间）。</summary>
    public sealed class UniverClipRect
    {
        public UniverClipRect(int startRow, int startCol, int endRow, int endCol)
        {
            StartRow = startRow;
            StartCol = startCol;
            EndRow = endRow;
            EndCol = endCol;
        }

        public int StartRow { get; }

        public int StartCol { get; }

        public int EndRow { get; }

        public int EndCol { get; }
    }

    public enum UniverPublishExportMode
    {
        Default,
        RangeFull,
        RangeContent,
    }
}
