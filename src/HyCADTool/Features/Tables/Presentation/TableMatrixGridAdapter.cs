using System;
using System.Collections.Generic;
using HyCAD.Tables;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;
using HyCADTool.Features.Tables.TableApp;

namespace HyCADTool.Features.Tables.Presentation
{
    /// <summary>
    /// 将 <see cref="TableGrid"/> 投影为 Excel 式 R×C 矩阵（含 merge 从属格）。
    /// </summary>
    public static class TableMatrixGridAdapter
    {
        public static TableMatrixSnapshot BuildMatrix(TableGrid grid)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            var structure = grid.Structure;
            var topology = structure.Topology;
            var rows = new List<TableMatrixRowItem>(topology.RowCount);

            for (var row = 0; row < topology.RowCount; row++)
            {
                var cells = new List<TableMatrixCellItem>(topology.ColCount);
                for (var col = 0; col < topology.ColCount; col++)
                {
                    var addr = new CellAddr(row, col);
                    var isHidden = structure.IsHidden(addr);
                    var anchor = structure.GetAnchorOf(addr);
                    var isAnchor = addr == anchor;

                    string text = string.Empty;
                    if (isAnchor)
                    {
                        text = TableSummaryBuilder.FormatCellValue(GridEditor.GetValue(grid, anchor));
                    }

                    var editable = !isHidden && isAnchor && IsMatrixCellEditable(grid, anchor);
                    cells.Add(new TableMatrixCellItem(addr, text, editable, isHidden));
                }

                rows.Add(new TableMatrixRowItem(row, cells));
            }

            return new TableMatrixSnapshot(topology.RowCount, topology.ColCount, rows);
        }

        private static bool IsMatrixCellEditable(TableGrid grid, CellAddr anchor)
        {
            if (TableFillGridAdapter.IsCellEditable(grid, anchor))
                return true;

            // 空表无 Role 时默认可编辑（Excel 式填值）
            return !grid.Structure.Roles.TryGetValue(anchor, out _);
        }
    }

    public sealed class TableMatrixSnapshot
    {
        public TableMatrixSnapshot(int rowCount, int colCount, IReadOnlyList<TableMatrixRowItem> rows)
        {
            RowCount = rowCount;
            ColCount = colCount;
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
        }

        public int RowCount { get; }

        public int ColCount { get; }

        public IReadOnlyList<TableMatrixRowItem> Rows { get; }
    }

    public sealed class TableMatrixRowItem
    {
        public TableMatrixRowItem(int rowIndex, IReadOnlyList<TableMatrixCellItem> cells)
        {
            RowIndex = rowIndex;
            Cells = cells ?? throw new ArgumentNullException(nameof(cells));
        }

        public int RowIndex { get; }

        public IReadOnlyList<TableMatrixCellItem> Cells { get; }
    }

    public sealed class TableMatrixCellItem
    {
        public TableMatrixCellItem(CellAddr address, string text, bool isEditable, bool isHidden)
        {
            Address = address;
            Text = text ?? string.Empty;
            IsEditable = isEditable;
            IsHidden = isHidden;
        }

        public CellAddr Address { get; }

        public string Text { get; }

        public bool IsEditable { get; }

        public bool IsHidden { get; }
    }
}
