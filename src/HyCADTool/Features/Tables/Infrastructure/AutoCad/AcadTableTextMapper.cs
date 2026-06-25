using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCAD.Tables.Layout;
using HyCAD.Tables.Structure;

namespace HyCADTool.Features.Tables.Infrastructure.AutoCad
{
    /// <summary>
    /// Domain 对齐 → DBText 对齐点与模式映射。
    /// </summary>
    internal static class AcadTableTextMapper
    {
        public static Point3d GetAlignmentPoint(LayoutRect bounds, CellStyle style, double paddingMm, double z)
        {
            double x;
            switch (style.HAlign)
            {
                case TextAlign.Center:
                    x = (bounds.Left + bounds.Right) / 2;
                    break;
                case TextAlign.End:
                    x = bounds.Right - paddingMm;
                    break;
                default:
                    x = bounds.Left + paddingMm;
                    break;
            }

            double y;
            switch (style.VAlign)
            {
                case TextAlign.Center:
                    y = (bounds.Top + bounds.Bottom) / 2;
                    break;
                case TextAlign.End:
                    y = bounds.Bottom + paddingMm;
                    break;
                default:
                    y = bounds.Top - paddingMm;
                    break;
            }

            return new Point3d(x, y, z);
        }

        public static TextHorizontalMode ToHorizontalMode(TextAlign align)
        {
            switch (align)
            {
                case TextAlign.Center:
                    return TextHorizontalMode.TextCenter;
                case TextAlign.End:
                    return TextHorizontalMode.TextRight;
                default:
                    return TextHorizontalMode.TextLeft;
            }
        }

        public static TextVerticalMode ToVerticalMode(TextAlign align)
        {
            switch (align)
            {
                case TextAlign.Center:
                    return TextVerticalMode.TextVerticalMid;
                case TextAlign.End:
                    return TextVerticalMode.TextBottom;
                default:
                    return TextVerticalMode.TextTop;
            }
        }

        public static void ApplyAlignment(
            DBText text,
            LayoutRect bounds,
            CellStyle style,
            double paddingMm,
            double z,
            Database db)
        {
            var point = GetAlignmentPoint(bounds, style, paddingMm, z);
            ApplyAtPoint(text, point.X, point.Y, style.HAlign, style.VAlign, z, db);
        }

        public static void ApplyAtPoint(
            DBText text,
            double x,
            double y,
            TextAlign hAlign,
            double z,
            Database db)
        {
            ApplyAtPoint(text, x, y, hAlign, TextAlign.Center, z, db);
        }

        public static void ApplyAtPoint(
            DBText text,
            double x,
            double y,
            TextAlign hAlign,
            TextAlign vAlign,
            double z,
            Database db)
        {
            var point = new Point3d(x, y, z);
            text.HorizontalMode = ToHorizontalMode(hAlign);
            text.VerticalMode = ToVerticalMode(vAlign);
            text.AlignmentPoint = point;
            text.Position = point;
            text.AdjustAlignment(db);
        }
    }
}
