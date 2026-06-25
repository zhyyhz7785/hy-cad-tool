using HyCAD.Tables.Structure;

namespace HyCADTool.Features.Tables.Infrastructure.AutoCad
{
    /// <summary>CellStyle + CellRole 叠加结果（AC5）。</summary>
    internal readonly struct EffectiveCellStyle
    {
        public EffectiveCellStyle(CellStyle style, double widthFactor = 1.0)
        {
            Style = style;
            WidthFactor = widthFactor;
        }

        public CellStyle Style { get; }
        public double WidthFactor { get; }
    }

    /// <summary>
    /// CellStyle + CellRole 叠加（AC5）。
    /// </summary>
    internal static class AcadTableRoleStyle
    {
        public static EffectiveCellStyle ResolveEffectiveStyle(
            GridStructure structure,
            CellAddr addr,
            AcadTableRenderOptions options)
        {
            var baseStyle = structure.Styles.TryGetValue(addr, out var style)
                ? style
                : new CellStyle(TextHeight: options.DefaultTextHeightMm);

            if (baseStyle.TextHeight <= 0)
            {
                baseStyle = CopyStyle(baseStyle, textHeight: options.DefaultTextHeightMm);
            }

            if (!structure.Roles.TryGetValue(addr, out var role))
                return new EffectiveCellStyle(baseStyle);

            return ApplyRole(baseStyle, role, options);
        }

        private static EffectiveCellStyle ApplyRole(
            CellStyle baseStyle,
            CellRole role,
            AcadTableRenderOptions options)
        {
            switch (role)
            {
                case CellRole.Title:
                {
                    var height = baseStyle.TextHeight * options.TitleTextHeightScale;
                    var hAlign = options.TitleForceCenter ? TextAlign.Center : baseStyle.HAlign;
                    return new EffectiveCellStyle(CopyStyle(baseStyle, hAlign: hAlign, textHeight: height));
                }
                case CellRole.Header:
                {
                    var height = baseStyle.TextHeight * options.HeaderTextHeightScale;
                    return new EffectiveCellStyle(
                        CopyStyle(baseStyle, textHeight: height),
                        options.HeaderWidthFactor);
                }
                default:
                    return new EffectiveCellStyle(baseStyle);
            }
        }

        private static CellStyle CopyStyle(
            CellStyle source,
            TextOrientation? orientation = null,
            TextAlign? hAlign = null,
            TextAlign? vAlign = null,
            double? textHeight = null)
        {
            return new CellStyle(
                orientation ?? source.Orientation,
                hAlign ?? source.HAlign,
                vAlign ?? source.VAlign,
                textHeight ?? source.TextHeight,
                source.FontKey,
                source.Borders,
                source.BackColor);
        }
    }
}
