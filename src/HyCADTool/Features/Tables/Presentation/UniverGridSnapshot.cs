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

        // ---- v2 富快照（可选，向后兼容；export 不反推这些字段）----

        /// <summary>水平对齐：start|center|end。</summary>
        public string HAlign { get; set; }

        /// <summary>垂直对齐：start|center|end。</summary>
        public string VAlign { get; set; }

        /// <summary>字高（mm）。</summary>
        public double? TextHeightMm { get; set; }

        /// <summary>是否允许自动换行。</summary>
        public bool? AllowWrap { get; set; }

        /// <summary>文本方向：horizontal|verticalStacked。</summary>
        public string Orientation { get; set; }

        /// <summary>四边边框线宽（mm）。</summary>
        public UniverGridBorders Borders { get; set; }

        /// <summary>单元格角色：title|header|label|value|photoSlot|spacer。</summary>
        public string Role { get; set; }

        /// <summary>稳定字段键（FieldIndex 反查）。</summary>
        public string FieldKey { get; set; }

        /// <summary>是否照片占位格。</summary>
        public bool? IsPhotoSlot { get; set; }
    }

    /// <summary>单元格四边边框线宽（mm）。</summary>
    public sealed class UniverGridBorders
    {
        public double TopMm { get; set; }

        public double RightMm { get; set; }

        public double BottomMm { get; set; }

        public double LeftMm { get; set; }
    }

    public static class UniverGridSnapshotMapper
    {
        /// <summary>与 Web <c>mm-display.ts</c> 的 DISPLAY_PX_PER_MM 一致（96/25.4）。</summary>
        public const double DisplayPxPerMm = 96.0 / 25.4;

        public const double MaxDisplayPx = 800;

        public const double DefaultRowHeightMm = 10.0;

        public const double DefaultColWidthMm = 25.0;

        public static UniverGridSnapshot FromTableGrid(TableGrid grid)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            var excel = ExcelGridSnapshotBuilder.Build(grid);
            var structure = grid.Structure;
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
                    HAlign = ToAlignString(c.HAlign),
                    VAlign = ToAlignString(c.VAlign),
                    TextHeightMm = c.TextHeightMm,
                    AllowWrap = c.AllowWrap,
                    Orientation = ToOrientationString(c.Orientation),
                    Borders = ToBorders(c.Borders),
                    IsPhotoSlot = c.IsPhotoSlot,
                    Role = ResolveRole(structure, c.Anchor),
                    FieldKey = ResolveFieldKey(structure, c.Anchor),
                }).ToList(),
            };
        }

        private static string ToAlignString(TextAlign align)
        {
            switch (align)
            {
                case TextAlign.Center:
                    return "center";
                case TextAlign.End:
                    return "end";
                default:
                    return "start";
            }
        }

        private static string ToOrientationString(TextOrientation orientation) =>
            orientation == TextOrientation.VerticalStacked ? "verticalStacked" : "horizontal";

        private static UniverGridBorders ToBorders(BorderSet borders)
        {
            if (borders == null || borders == BorderSet.None)
                return null;

            if (borders.Top <= 0 && borders.Right <= 0 && borders.Bottom <= 0 && borders.Left <= 0)
                return null;

            return new UniverGridBorders
            {
                TopMm = borders.Top,
                RightMm = borders.Right,
                BottomMm = borders.Bottom,
                LeftMm = borders.Left,
            };
        }

        private static string ResolveRole(GridStructure structure, CellAddr anchor) =>
            structure.Roles.TryGetValue(anchor, out var role) ? ToRoleString(role) : null;

        private static string ToRoleString(CellRole role)
        {
            switch (role)
            {
                case CellRole.Title:
                    return "title";
                case CellRole.Header:
                    return "header";
                case CellRole.Label:
                    return "label";
                case CellRole.Value:
                    return "value";
                case CellRole.PhotoSlot:
                    return "photoSlot";
                case CellRole.Spacer:
                    return "spacer";
                default:
                    return null;
            }
        }

        private static string ResolveFieldKey(GridStructure structure, CellAddr anchor)
        {
            foreach (var entry in structure.FieldIndex)
            {
                if (entry.Value == anchor)
                    return entry.Key;
            }

            return null;
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
            return MmToDisplayPx(mm, DefaultRowHeightMm);
        }

        public static double MmToColPx(double mm)
        {
            return MmToDisplayPx(mm, DefaultColWidthMm);
        }

        public static double DisplayPxToMm(double px, double fallbackMm)
        {
            if (px <= 0 || double.IsNaN(px) || double.IsInfinity(px))
                return fallbackMm;

            var mm = px / DisplayPxPerMm;
            return Math.Round(mm, 2);
        }

        private static double MmToDisplayPx(double mm, double fallbackMm)
        {
            var value = mm > 0 ? mm : fallbackMm;
            var px = value * DisplayPxPerMm;
            if (px > MaxDisplayPx)
                px = MaxDisplayPx;
            return Math.Round(px, 2);
        }
    }
}
