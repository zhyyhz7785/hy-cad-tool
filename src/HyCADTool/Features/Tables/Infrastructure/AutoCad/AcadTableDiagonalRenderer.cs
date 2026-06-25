using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCAD.Tables.Layout;
using HyCAD.Tables.Structure;

namespace HyCADTool.Features.Tables.Infrastructure.AutoCad
{
    /// <summary>
    /// 斜线格：对角 Line + SubCell 文字（AC5 D3）。
    /// </summary>
    internal static class AcadTableDiagonalRenderer
    {
        public static Line CreateDiagonalLine(
            LayoutRect bounds,
            DiagonalDirection direction,
            AcadTableRenderOptions options)
        {
            Point3d start;
            Point3d end;
            var z = options.ZElevation;

            switch (direction)
            {
                case DiagonalDirection.SlashBLTR:
                    start = new Point3d(bounds.Left, bounds.Bottom, z);
                    end = new Point3d(bounds.Right, bounds.Top, z);
                    break;
                default:
                    start = new Point3d(bounds.Left, bounds.Top, z);
                    end = new Point3d(bounds.Right, bounds.Bottom, z);
                    break;
            }

            return new Line(start, end) { Layer = options.EffectiveDiagonalLayerName };
        }

        public static IEnumerable<DBText> CreateSubCellTexts(
            DiagonalSplit split,
            LayoutRect bounds,
            EffectiveCellStyle effective,
            AcadTableRenderOptions options,
            Database database)
        {
            for (var i = 0; i < split.Parts.Count; i++)
            {
                var part = split.Parts[i];
                var display = GetDisplayText(part.Value);
                if (string.IsNullOrEmpty(display) && !options.DrawEmptyCellText)
                    continue;

                var anchor = part.TextAnchor;
                if (anchor == default)
                    anchor = GetDefaultAnchor(split.Direction, i);

                var px = bounds.Left + anchor.X * bounds.Width;
                var py = bounds.Top - anchor.Y * bounds.Height;

                var text = new DBText
                {
                    Height = effective.Style.TextHeight,
                    TextString = display,
                    Layer = options.TextLayerName,
                    WidthFactor = effective.WidthFactor,
                };

                AcadTableTextMapper.ApplyAtPoint(
                    text,
                    px,
                    py,
                    part.Align,
                    options.ZElevation,
                    database);

                yield return text;
            }
        }

        private static NormalizedPoint GetDefaultAnchor(DiagonalDirection direction, int partIndex)
        {
            if (direction == DiagonalDirection.SlashBLTR)
                return partIndex == 0 ? new NormalizedPoint(0.25, 0.75) : new NormalizedPoint(0.75, 0.25);

            return partIndex == 0 ? new NormalizedPoint(0.25, 0.25) : new NormalizedPoint(0.75, 0.75);
        }

        private static string GetDisplayText(HyCAD.Tables.Data.CellValue value)
        {
            if (value.Runs != null && value.Runs.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var run in value.Runs)
                    sb.Append(run.Text);
                return sb.ToString();
            }

            return value.Text ?? string.Empty;
        }
    }
}
