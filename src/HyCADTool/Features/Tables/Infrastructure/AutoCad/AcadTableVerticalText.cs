using System.Collections.Generic;
using HyCAD.Tables.Layout;
using HyCAD.Tables.Structure;

namespace HyCADTool.Features.Tables.Infrastructure.AutoCad
{
    /// <summary>
    /// 竖排逐字坐标（Down 方向，mm）。
    /// </summary>
    internal static class AcadTableVerticalText
    {
        public static IReadOnlyList<(char Character, double X, double Y)> ComputePositions(
            string text,
            LayoutRect bounds,
            CellStyle style,
            AcadTableRenderOptions options)
        {
            if (string.IsNullOrEmpty(text))
                return System.Array.Empty<(char, double, double)>();

            var textHeight = style.TextHeight > 0 ? style.TextHeight : options.DefaultTextHeightMm;
            var gap = options.VerticalCharGapMm;
            var step = textHeight + gap;
            var n = text.Length;
            var blockHeight = n * textHeight + (n - 1) * gap;

            var x = ResolveColumnX(bounds, options);
            var y0 = ResolveFirstCharCenterY(bounds, blockHeight, textHeight, options);

            var result = new (char, double, double)[n];
            for (var i = 0; i < n; i++)
                result[i] = (text[i], x, y0 - i * step);

            return result;
        }

        private static double ResolveColumnX(LayoutRect bounds, AcadTableRenderOptions options)
        {
            var padding = options.TextPaddingMm;
            switch (options.VerticalStackAlign)
            {
                case TextAlign.End:
                    return bounds.Right - padding;
                case TextAlign.Start:
                    return bounds.Left + padding;
                default:
                    return (bounds.Left + bounds.Right) / 2;
            }
        }

        private static double ResolveFirstCharCenterY(
            LayoutRect bounds,
            double blockHeight,
            double textHeight,
            AcadTableRenderOptions options)
        {
            var padding = options.TextPaddingMm;
            switch (options.VerticalBlockVAlign)
            {
                case TextAlign.Start:
                    return bounds.Top - padding - textHeight / 2;
                case TextAlign.End:
                    return bounds.Bottom + padding + blockHeight - textHeight / 2;
                default:
                    return (bounds.Top + bounds.Bottom) / 2 + blockHeight / 2 - textHeight / 2;
            }
        }
    }
}
