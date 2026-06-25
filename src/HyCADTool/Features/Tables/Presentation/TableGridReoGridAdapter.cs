using System;
using HyCAD.Tables;
using HyCAD.Tables.Structure;
using unvell.ReoGrid;
using unvell.ReoGrid.Graphics;

namespace HyCADTool.Features.Tables.Presentation
{
    /// <summary>
    /// <see cref="TableGrid"/> ↔ ReoGrid <see cref="Worksheet"/> 单向加载与 Cell.Tag 元数据规范。
    /// 编辑回写由宿主控件经 ViewModel.OpLog 完成，ReoGrid 不作第二真相源。
    /// </summary>
    public static class TableGridReoGridAdapter
    {
        private const double ColScale = 1.6;
        private const double RowScale = 2.0;
        private const ushort MinColPx = 52;
        private const ushort MaxColPx = 260;
        private const ushort MinRowPx = 22;
        private const ushort MaxRowPx = 140;

        /// <summary>将 Domain 表格加载到 ReoGrid 工作表（会 Reset 工作表）。</summary>
        public static void Load(TableGrid grid, Worksheet sheet)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));
            if (sheet == null)
                throw new ArgumentNullException(nameof(sheet));

            var snapshot = ExcelGridSnapshotBuilder.Build(grid);
            var structure = grid.Structure;

            sheet.Reset(snapshot.RowCount, snapshot.ColCount);

            for (var r = 0; r < snapshot.RowCount; r++)
            {
                var h = MmToRowPx(snapshot.RowHeightsMm[r]);
                sheet.SetRowsHeight(r, 1, h);
            }

            for (var c = 0; c < snapshot.ColCount; c++)
            {
                var w = MmToColPx(snapshot.ColWidthsMm[c]);
                sheet.SetColumnsWidth(c, 1, w);
            }

            foreach (var cell in snapshot.Cells)
            {
                var row = cell.Anchor.Row;
                var col = cell.Anchor.Col;
                var reoCell = sheet.CreateAndGetCell(row, col);
                reoCell.Data = cell.Text;

                CellRole? role = null;
                if (structure.Roles.TryGetValue(cell.Anchor, out var roleValue))
                    role = roleValue;
                var fieldKey = ResolveFieldKey(structure, cell.Anchor);
                reoCell.Tag = TableGridReoGridCellTag.FromAnchor(cell.Anchor, fieldKey, role).Serialize();

                if (!cell.IsEditable)
                    reoCell.IsReadOnly = true;

                var style = BuildRangeStyle(cell);
                if (cell.RowSpan > 1 || cell.ColSpan > 1)
                {
                    sheet.MergeRange(row, col, cell.RowSpan, cell.ColSpan);
                    sheet.SetRangeStyles(row, col, cell.RowSpan, cell.ColSpan, style);
                }
                else
                {
                    sheet.SetRangeStyles(row, col, 1, 1, style);
                }
            }
        }

        /// <summary>从 ReoGrid 单元格解析 anchor 地址（合并格取 Tag 或合并左上角）。</summary>
        public static bool TryGetAnchor(Worksheet sheet, int row, int col, out CellAddr anchor)
        {
            anchor = default;
            if (sheet == null || row < 0 || col < 0)
                return false;

            var cell = sheet.GetCell(row, col);
            if (cell != null && TableGridReoGridCellTag.TryParse(cell.Tag, out var meta))
            {
                anchor = meta.ToAnchor();
                return true;
            }

            if (sheet.IsMergedCell(row, col))
            {
                var range = sheet.GetRangeIfMergedCell(new CellPosition(row, col));
                anchor = new CellAddr(range.Row, range.Col);
                return true;
            }

            anchor = new CellAddr(row, col);
            return true;
        }

        public static ReoGridHorAlign ToReoGridHorAlign(TextAlign align)
        {
            switch (align)
            {
                case TextAlign.Center:
                    return ReoGridHorAlign.Center;
                case TextAlign.End:
                    return ReoGridHorAlign.Right;
                default:
                    return ReoGridHorAlign.Left;
            }
        }

        public static ReoGridVerAlign ToReoGridVerAlign(TextAlign align)
        {
            switch (align)
            {
                case TextAlign.Start:
                    return ReoGridVerAlign.Top;
                case TextAlign.End:
                    return ReoGridVerAlign.Bottom;
                default:
                    return ReoGridVerAlign.Middle;
            }
        }

        private static WorksheetRangeStyle BuildRangeStyle(ExcelCellRender cell)
        {
            var style = new WorksheetRangeStyle
            {
                HAlign = ToReoGridHorAlign(cell.HAlign),
                VAlign = ToReoGridVerAlign(cell.VAlign),
                TextWrapMode = TextWrapMode.WordBreak,
            };

            if (cell.IsPhotoSlot)
                style.BackColor = new SolidColor(255, 0xF7, 0xF2, 0xE8);

            style.Flag = PlainStyleFlag.HorizontalAlign
                | PlainStyleFlag.VerticalAlign
                | PlainStyleFlag.TextWrap
                | (cell.IsPhotoSlot ? PlainStyleFlag.BackColor : PlainStyleFlag.None);

            return style;
        }

        private static ushort MmToColPx(double mm) =>
            (ushort)Math.Max(MinColPx, Math.Min(MaxColPx, mm * ColScale));

        private static ushort MmToRowPx(double mm) =>
            (ushort)Math.Max(MinRowPx, Math.Min(MaxRowPx, mm * RowScale));

        private static string ResolveFieldKey(GridStructure structure, CellAddr anchor)
        {
            foreach (var entry in structure.FieldIndex)
            {
                if (entry.Value == anchor)
                    return entry.Key;
            }

            return null;
        }
    }
}
