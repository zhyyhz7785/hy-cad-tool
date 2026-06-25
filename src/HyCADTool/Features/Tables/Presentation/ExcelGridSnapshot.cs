using System;
using System.Collections.Generic;
using HyCAD.Tables;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;
using HyCADTool.Features.Tables.TableApp;

namespace HyCADTool.Features.Tables.Presentation
{
    /// <summary>
    /// Excel 网格控件的渲染快照：行列尺寸 + 仅 anchor 格（含合并跨度）。
    /// </summary>
    public sealed class ExcelGridSnapshot
    {
        public ExcelGridSnapshot(
            int rowCount,
            int colCount,
            IReadOnlyList<double> rowHeightsMm,
            IReadOnlyList<double> colWidthsMm,
            IReadOnlyList<ExcelCellRender> cells)
        {
            RowCount = rowCount;
            ColCount = colCount;
            RowHeightsMm = rowHeightsMm;
            ColWidthsMm = colWidthsMm;
            Cells = cells;
        }

        public int RowCount { get; }

        public int ColCount { get; }

        public IReadOnlyList<double> RowHeightsMm { get; }

        public IReadOnlyList<double> ColWidthsMm { get; }

        /// <summary>仅包含可见 anchor 格（合并从属格不出现）。</summary>
        public IReadOnlyList<ExcelCellRender> Cells { get; }
    }

    /// <summary>单个 anchor 格的渲染信息。</summary>
    public sealed class ExcelCellRender
    {
        public ExcelCellRender(
            CellAddr anchor,
            int rowSpan,
            int colSpan,
            string text,
            bool isEditable,
            TextAlign hAlign,
            TextAlign vAlign,
            bool isPhotoSlot)
        {
            Anchor = anchor;
            RowSpan = rowSpan;
            ColSpan = colSpan;
            Text = text ?? string.Empty;
            IsEditable = isEditable;
            HAlign = hAlign;
            VAlign = vAlign;
            IsPhotoSlot = isPhotoSlot;
        }

        public CellAddr Anchor { get; }

        public int RowSpan { get; }

        public int ColSpan { get; }

        public string Text { get; }

        public bool IsEditable { get; }

        public TextAlign HAlign { get; }

        public TextAlign VAlign { get; }

        public bool IsPhotoSlot { get; }
    }

    /// <summary>
    /// 从 <see cref="TableGrid"/> 构建 Excel 网格渲染快照。
    /// </summary>
    public static class ExcelGridSnapshotBuilder
    {
        public static ExcelGridSnapshot Build(TableGrid grid)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            var structure = grid.Structure;
            var topology = structure.Topology;

            var rowHeights = new double[topology.RowCount];
            for (var r = 0; r < topology.RowCount; r++)
                rowHeights[r] = topology.Rows[r].Size;

            var colWidths = new double[topology.ColCount];
            for (var c = 0; c < topology.ColCount; c++)
                colWidths[c] = topology.Cols[c].Size;

            var cells = new List<ExcelCellRender>();
            for (var row = 0; row < topology.RowCount; row++)
            {
                for (var col = 0; col < topology.ColCount; col++)
                {
                    var addr = new CellAddr(row, col);

                    if (structure.IsHidden(addr))
                        continue;

                    var anchor = structure.GetAnchorOf(addr);
                    if (addr != anchor)
                        continue;

                    var rowSpan = 1;
                    var colSpan = 1;
                    if (structure.TryGetMergeAt(anchor, out var merge))
                    {
                        rowSpan = merge.RowSpan;
                        colSpan = merge.ColSpan;
                    }

                    var text = TableSummaryBuilder.FormatCellValue(GridEditor.GetValue(grid, anchor));
                    var editable = TableFillGridAdapter.IsCellEditable(grid, anchor)
                        || !structure.Roles.ContainsKey(anchor);

                    var style = GridEditor.GetCellStyle(grid, anchor);
                    var isPhoto = structure.Roles.TryGetValue(anchor, out var role)
                        && role == CellRole.PhotoSlot;

                    cells.Add(new ExcelCellRender(
                        anchor,
                        rowSpan,
                        colSpan,
                        text,
                        editable,
                        style.HAlign,
                        style.VAlign,
                        isPhoto));
                }
            }

            return new ExcelGridSnapshot(
                topology.RowCount,
                topology.ColCount,
                rowHeights,
                colWidths,
                cells);
        }
    }
}
