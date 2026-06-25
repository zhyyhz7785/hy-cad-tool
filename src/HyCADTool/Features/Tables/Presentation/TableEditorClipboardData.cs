using System.Collections.Generic;
using HyCAD.Tables.Structure;

namespace HyCADTool.Features.Tables.Presentation
{
    /// <summary>表格编辑器选区剪贴板 DTO（内存，M2）。</summary>
    public sealed class TableEditorClipboardData
    {
        public TableEditorClipboardData(int rowCount, int colCount, IReadOnlyList<TableEditorClipboardCell> cells)
        {
            RowCount = rowCount;
            ColCount = colCount;
            Cells = cells ?? new List<TableEditorClipboardCell>();
        }

        public int RowCount { get; }

        public int ColCount { get; }

        public IReadOnlyList<TableEditorClipboardCell> Cells { get; }
    }

    public sealed class TableEditorClipboardCell
    {
        public TableEditorClipboardCell(
            int rowOffset,
            int colOffset,
            string text,
            CellStyle style,
            bool allowWrap)
        {
            RowOffset = rowOffset;
            ColOffset = colOffset;
            Text = text ?? string.Empty;
            Style = style;
            AllowWrap = allowWrap;
        }

        public int RowOffset { get; }

        public int ColOffset { get; }

        public string Text { get; }

        public CellStyle Style { get; }

        public bool AllowWrap { get; }
    }
}
