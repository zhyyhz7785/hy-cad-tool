using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HyCAD.Tables;
using HyCAD.Tables.Data;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;

namespace HyCADTool.Features.Tables.TableApp
{
    /// <summary>
    /// 从 <see cref="TableGrid"/> 构建 AC7 摘要（纯 Domain 统计）。
    /// </summary>
    public static class TableSummaryBuilder
    {
        public const string SchemaVersion = "1";
        private const int MaxFieldKeysListed = 20;

        public static TableSummary Build(TableGrid grid)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            var structure = grid.Structure;
            var topology = structure.Topology;
            var report = GridInvariants.Validate(grid);

            var fieldKeys = structure.FieldIndex.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

            return new TableSummary
            {
                TableId = grid.Id,
                DisplayTitle = ResolveDisplayTitle(grid),
                RowCount = topology.RowCount,
                ColCount = topology.ColCount,
                VisibleCellCount = CountVisibleCells(structure),
                MergeRegionCount = structure.Merges.Count,
                FieldKeyCount = fieldKeys.Count,
                FieldKeys = fieldKeys.Take(MaxFieldKeysListed).ToList(),
                FormulaCellCount = CountCellsByKind(grid, CellValueKind.Formula),
                BoundCellCount = CountCellsByKind(grid, CellValueKind.Bound),
                InvariantsValid = report.IsValid,
                FirstInvariantMessage = report.Violations.Count > 0 ? report.Violations[0].Message : null,
                SchemaVersion = SchemaVersion,
            };
        }

        public static string ResolveDisplayTitle(TableGrid grid)
        {
            if (grid.Metadata != null
                && grid.Metadata.TryGetValue("title", out var metaTitle)
                && !string.IsNullOrWhiteSpace(metaTitle))
            {
                return metaTitle.Trim();
            }

            foreach (var entry in grid.Structure.Roles)
            {
                if (entry.Value != CellRole.Title)
                    continue;

                var text = FormatCellValue(GridEditor.GetValue(grid, entry.Key));
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            foreach (var entry in grid.Structure.Roles)
            {
                if (entry.Value != CellRole.Header)
                    continue;

                var text = FormatCellValue(GridEditor.GetValue(grid, entry.Key));
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            return "表格 " + grid.Id.ToString("N").Substring(0, 8);
        }

        private static int CountVisibleCells(GridStructure structure)
        {
            var topology = structure.Topology;
            var count = 0;
            for (var row = 0; row < topology.RowCount; row++)
            {
                for (var col = 0; col < topology.ColCount; col++)
                {
                    if (!structure.IsHidden(new CellAddr(row, col)))
                        count++;
                }
            }

            return count;
        }

        private static int CountCellsByKind(TableGrid grid, CellValueKind kind)
        {
            var count = 0;
            foreach (var entry in grid.Data.Cells)
            {
                if (entry.Value.Kind == kind)
                    count++;
            }

            return count;
        }

        internal static string FormatCellValue(CellValue value)
        {
            if (value.Runs != null && value.Runs.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var run in value.Runs)
                    sb.Append(run.Text);
                return sb.ToString();
            }

            return value.Text ?? string.Empty;
        }
    }
}
