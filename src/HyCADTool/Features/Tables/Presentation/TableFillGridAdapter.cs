using System;
using System.Collections.Generic;
using HyCAD.Tables;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;
using HyCADTool.Features.Tables.TableApp;

namespace HyCADTool.Features.Tables.Presentation
{
    /// <summary>
    /// 将 <see cref="TableGrid"/> 可见 Anchor 格投影为填值面板行。
    /// </summary>
    public static class TableFillGridAdapter
    {
        public static IReadOnlyList<TableFillRowItem> BuildRows(TableGrid grid)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            var structure = grid.Structure;
            var topology = structure.Topology;
            var rows = new List<TableFillRowItem>();

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

                    rows.Add(CreateRow(grid, anchor));
                }
            }

            return rows;
        }

        public static bool IsCellEditable(TableGrid grid, CellAddr anchor)
        {
            if (grid.Structure.Roles.TryGetValue(anchor, out var role))
            {
                if (role == CellRole.Value)
                    return true;

                if (role == CellRole.Label
                    || role == CellRole.Header
                    || role == CellRole.Title
                    || role == CellRole.PhotoSlot
                    || role == CellRole.Spacer)
                {
                    return false;
                }
            }

            foreach (var entry in grid.Structure.FieldIndex)
            {
                if (entry.Value == anchor)
                    return true;
            }

            return false;
        }

        private static TableFillRowItem CreateRow(TableGrid grid, CellAddr anchor)
        {
            grid.Structure.Roles.TryGetValue(anchor, out var role);

            string fieldKey = null;
            foreach (var entry in grid.Structure.FieldIndex)
            {
                if (entry.Value == anchor)
                {
                    fieldKey = entry.Key;
                    break;
                }
            }

            var text = TableSummaryBuilder.FormatCellValue(GridEditor.GetValue(grid, anchor));
            var editable = IsCellEditable(grid, anchor);

            return new TableFillRowItem(
                anchor,
                $"({anchor.Row},{anchor.Col})",
                role.ToString(),
                fieldKey,
                text,
                editable);
        }
    }

    public sealed class TableFillRowItem
    {
        public TableFillRowItem(
            CellAddr address,
            string addressText,
            string roleText,
            string fieldKey,
            string text,
            bool isEditable)
        {
            Address = address;
            AddressText = addressText;
            RoleText = roleText;
            FieldKey = fieldKey ?? string.Empty;
            Text = text ?? string.Empty;
            IsEditable = isEditable;
        }

        public CellAddr Address { get; }

        public string AddressText { get; }

        public string RoleText { get; }

        public string FieldKey { get; }

        public string Text { get; set; }

        public bool IsEditable { get; }
    }
}
