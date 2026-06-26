using System;
using System.Collections.Generic;
using System.Linq;
using HyCAD.Tables;
using HyCAD.Tables.Data;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;
using HyCADTool.Features.Tables.TableApp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HyCADTool.Features.Tables.Presentation
{
    /// <summary>
    /// TableGrid ↔ Univer WebView 的 JSON 快照（Phase 2 桥接 DTO）。
    /// </summary>
    public sealed class UniverGridSnapshot
    {
        public int RowCount { get; set; }

        public int ColCount { get; set; }

        public IList<double> RowHeightsMm { get; set; } = new List<double>();

        public IList<double> ColWidthsMm { get; set; } = new List<double>();

        public IList<UniverGridCellSnapshot> Cells { get; set; } = new List<UniverGridCellSnapshot>();
    }

    public sealed class UniverGridCellSnapshot
    {
        public int Row { get; set; }

        public int Col { get; set; }

        public int RowSpan { get; set; } = 1;

        public int ColSpan { get; set; } = 1;

        public string Text { get; set; } = string.Empty;

        public bool Editable { get; set; } = true;
    }

    public static class UniverGridSnapshotMapper
    {
        private const double ColScale = 1.6;
        private const double RowScale = 2.0;

        public static UniverGridSnapshot FromTableGrid(TableGrid grid)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            var excel = ExcelGridSnapshotBuilder.Build(grid);
            return new UniverGridSnapshot
            {
                RowCount = excel.RowCount,
                ColCount = excel.ColCount,
                RowHeightsMm = excel.RowHeightsMm.ToList(),
                ColWidthsMm = excel.ColWidthsMm.ToList(),
                Cells = excel.Cells.Select(c => new UniverGridCellSnapshot
                {
                    Row = c.Anchor.Row,
                    Col = c.Anchor.Col,
                    RowSpan = c.RowSpan,
                    ColSpan = c.ColSpan,
                    Text = TableGridReoGridAdapter.FormatDisplayText(c),
                    Editable = c.IsEditable,
                }).ToList(),
            };
        }

        public static string ToJson(TableGrid grid) =>
            JsonConvert.SerializeObject(FromTableGrid(grid));

        public static UniverGridSnapshot Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonConvert.DeserializeObject<UniverGridSnapshot>(json);
        }

        public static UniverGridSnapshot Parse(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;

            return token.ToObject<UniverGridSnapshot>();
        }

        /// <summary>若快照行列大于当前拓扑，在末尾插入行/列直至容纳快照尺寸。</summary>
        public static void EnsureGridFits(TableOpLog opLog, UniverGridSnapshot snapshot)
        {
            if (opLog == null || snapshot == null)
                return;

            var rows = opLog.Current.Structure.Topology.RowCount;
            var cols = opLog.Current.Structure.Topology.ColCount;

            while (rows < snapshot.RowCount)
            {
                opLog.Apply(new InsertRowOp(rows));
                rows++;
            }

            while (cols < snapshot.ColCount)
            {
                opLog.Apply(new InsertColumnOp(cols));
                cols++;
            }
        }

        /// <summary>将 Univer 导出的文本写回 OpLog（仅 editable anchor 格）。</summary>
        public static void ApplyTextValues(TableOpLog opLog, UniverGridSnapshot snapshot)
        {
            if (opLog == null || snapshot?.Cells == null)
                return;

            var grid = opLog.Current;
            var rowCount = grid.Structure.Topology.RowCount;
            var colCount = grid.Structure.Topology.ColCount;
            var lookup = snapshot.Cells.ToDictionary(
                c => new CellAddr(c.Row, c.Col),
                c => c.Text ?? string.Empty);

            foreach (var cell in snapshot.Cells)
            {
                if (!cell.Editable)
                    continue;

                if (cell.Row < 0 || cell.Col < 0 || cell.Row >= rowCount || cell.Col >= colCount)
                    continue;

                var anchor = new CellAddr(cell.Row, cell.Col);
                if (!lookup.TryGetValue(anchor, out var text))
                    continue;

                var current = GridEditor.GetValue(grid, anchor);
                var normalized = text ?? string.Empty;
                var style = GridEditor.GetCellStyle(grid, anchor);
                if (style.Orientation == TextOrientation.VerticalStacked)
                {
                    normalized = normalized.Replace("\r", string.Empty).Replace("\n", string.Empty);
                }

                if (string.Equals(TableSummaryBuilder.FormatCellValue(current), normalized, StringComparison.Ordinal))
                    continue;

                opLog.Apply(new SetValueOp(anchor, new CellValue(normalized)));
            }
        }

        public static double MmToRowPx(double mm)
        {
            var value = mm <= 0 ? 10 : mm;
            return Math.Max(22, Math.Min(140, value * RowScale));
        }

        public static double MmToColPx(double mm)
        {
            var value = mm <= 0 ? 25 : mm;
            return Math.Max(52, Math.Min(260, value * ColScale));
        }
    }
}
