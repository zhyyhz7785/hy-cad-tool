using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HyCADTool.Features.Export.Services.Svg
{
    /// <summary>
    /// 纯 C# SVG 写出器，不引用 AutoCAD。输入坐标为 DWG（Y 向上）。
    /// 纸张尺寸用 mm；viewBox 为打印窗口（图形单位）。
    /// </summary>
    internal static class SvgDocumentWriter
    {
        private const double DefaultLineMm = 0.18;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Write(
            IReadOnlyList<SvgPrimitive> primitives,
            SvgPlotSettings settings,
            SvgPlotLayout layout)
        {
            var sb = new StringBuilder(Math.Max(4096, primitives.Count * 80));
            double vx = layout.ViewX;
            double vy = layout.ViewY;
            double vw = layout.ViewW;
            double vh = layout.ViewH;
            if (vw < 1e-9) vw = 1;
            if (vh < 1e-9) vh = 1;

            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
            sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\"");
            sb.Append(" width=\"").Append(N(layout.PaperWidthMm)).Append("mm\"");
            sb.Append(" height=\"").Append(N(layout.PaperHeightMm)).Append("mm\"");
            sb.Append(" viewBox=\"").Append(N(vx)).Append(' ').Append(N(vy)).Append(' ')
                .Append(N(vw)).Append(' ').Append(N(vh)).Append('"');
            sb.Append(" preserveAspectRatio=\"xMidYMid meet\"");
            sb.Append(" overflow=\"hidden\">\n");

            if (settings.WhiteBackground)
            {
                sb.Append("  <rect x=\"").Append(N(vx)).Append("\" y=\"").Append(N(vy))
                    .Append("\" width=\"").Append(N(vw)).Append("\" height=\"").Append(N(vh))
                    .Append("\" fill=\"#ffffff\"/>\n");
            }

            var clip = layout.ClipDwg;
            double clipX = clip.MinX;
            double clipY = Ty(clip.MaxY);
            double clipW = Math.Max(1e-9, clip.Width);
            double clipH = Math.Max(1e-9, clip.Height);

            if (settings.ClipToWindow)
            {
                sb.Append("  <defs>\n    <clipPath id=\"plotClip\">\n");
                sb.Append("      <rect x=\"").Append(N(clipX)).Append("\" y=\"").Append(N(clipY))
                    .Append("\" width=\"").Append(N(clipW)).Append("\" height=\"").Append(N(clipH))
                    .Append("\"/>\n");
                sb.Append("    </clipPath>\n  </defs>\n");
            }

            double cx = vx + vw * 0.5;
            double cy = vy + vh * 0.5;
            if (layout.UpsideDown)
            {
                sb.Append("  <g transform=\"rotate(180 ").Append(N(cx)).Append(' ').Append(N(cy)).Append(")\">\n");
            }

            string clipAttr = settings.ClipToWindow ? " clip-path=\"url(#plotClip)\"" : "";
            sb.Append("  <g id=\"drawing\"").Append(clipAttr)
                .Append(" fill=\"none\" stroke=\"#000000\" stroke-linecap=\"round\" stroke-linejoin=\"round\">\n");

            var groups = new List<string>();
            var byLayer = new Dictionary<string, List<SvgPrimitive>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < primitives.Count; i++)
            {
                var p = primitives[i];
                string layer = string.IsNullOrEmpty(p.Layer) ? "0" : p.Layer;
                if (!byLayer.TryGetValue(layer, out var list))
                {
                    list = new List<SvgPrimitive>();
                    byLayer[layer] = list;
                    groups.Add(layer);
                }
                list.Add(p);
            }

            for (int g = 0; g < groups.Count; g++)
            {
                string layer = groups[g];
                var list = byLayer[layer];
                sb.Append("    <g id=\"layer_").Append(SanitizeId(layer))
                    .Append("\" data-layer=\"").Append(EscAttr(layer)).Append("\">\n");
                for (int i = 0; i < list.Count; i++)
                    AppendPrimitive(sb, list[i], settings, layout);
                sb.Append("    </g>\n");
            }

            sb.Append("  </g>\n");

            if (settings.DrawBorder)
            {
                double bw = settings.BorderWidthMm > 1e-9 ? settings.BorderWidthMm : 0.25;
                double strokeUser = bw * layout.UnitsPerMm;
                sb.Append("  <rect x=\"").Append(N(clipX)).Append("\" y=\"").Append(N(clipY))
                    .Append("\" width=\"").Append(N(clipW)).Append("\" height=\"").Append(N(clipH))
                    .Append("\" fill=\"none\" stroke=\"#000000\" stroke-width=\"").Append(N(strokeUser)).Append("\"/>\n");
            }

            if (layout.UpsideDown)
                sb.Append("  </g>\n");

            sb.Append("</svg>\n");
            return sb.ToString();
        }

        private static void AppendPrimitive(
            StringBuilder sb, SvgPrimitive p, SvgPlotSettings settings, SvgPlotLayout layout)
        {
            sb.Append("      ");
            switch (p.Kind)
            {
                case SvgPrimitiveKind.Line:
                    sb.Append("<line");
                    AppendId(sb, p);
                    sb.Append(" x1=\"").Append(N(p.X1)).Append("\" y1=\"").Append(N(Ty(p.Y1)))
                        .Append("\" x2=\"").Append(N(p.X2)).Append("\" y2=\"").Append(N(Ty(p.Y2))).Append('"');
                    AppendStroke(sb, p, settings, layout);
                    sb.Append("/>\n");
                    break;

                case SvgPrimitiveKind.Circle:
                    sb.Append("<circle");
                    AppendId(sb, p);
                    sb.Append(" cx=\"").Append(N(p.Cx)).Append("\" cy=\"").Append(N(Ty(p.Cy)))
                        .Append("\" r=\"").Append(N(p.R)).Append('"');
                    AppendStroke(sb, p, settings, layout);
                    AppendFill(sb, p, settings);
                    sb.Append("/>\n");
                    break;

                case SvgPrimitiveKind.Ellipse:
                    sb.Append("<ellipse");
                    AppendId(sb, p);
                    sb.Append(" cx=\"").Append(N(p.Cx)).Append("\" cy=\"").Append(N(Ty(p.Cy)))
                        .Append("\" rx=\"").Append(N(p.Rx)).Append("\" ry=\"").Append(N(p.Ry)).Append('"');
                    if (Math.Abs(p.RotationDeg) > 1e-9)
                    {
                        sb.Append(" transform=\"rotate(").Append(N(-p.RotationDeg)).Append(' ')
                            .Append(N(p.Cx)).Append(' ').Append(N(Ty(p.Cy))).Append(")\"");
                    }
                    AppendStroke(sb, p, settings, layout);
                    AppendFill(sb, p, settings);
                    sb.Append("/>\n");
                    break;

                case SvgPrimitiveKind.Path:
                    sb.Append("<path");
                    AppendId(sb, p);
                    sb.Append(" d=\"").Append(PathD(p.Path)).Append('"');
                    AppendStroke(sb, p, settings, layout);
                    AppendFill(sb, p, settings);
                    sb.Append("/>\n");
                    break;

                case SvgPrimitiveKind.Polygon:
                    sb.Append("<polygon");
                    AppendId(sb, p);
                    sb.Append(" points=\"");
                    if (p.Points != null)
                    {
                        for (int i = 0; i < p.Points.Count; i++)
                        {
                            if (i > 0) sb.Append(' ');
                            sb.Append(N(p.Points[i].X)).Append(',').Append(N(Ty(p.Points[i].Y)));
                        }
                    }
                    sb.Append('"');
                    AppendStroke(sb, p, settings, layout);
                    AppendFill(sb, p, settings);
                    sb.Append("/>\n");
                    break;

                case SvgPrimitiveKind.Text:
                    AppendText(sb, p, settings);
                    break;
            }
        }

        private static void AppendText(StringBuilder sb, SvgPrimitive p, SvgPlotSettings settings)
        {
            double sx = p.Cx;
            double sy = Ty(p.Cy);
            double rot = -p.RotationDeg;
            bool fitBox = p.BoxWidth > 1e-9;
            double wf = p.WidthFactor <= 0 ? 1.0 : p.WidthFactor;
            string fill = MapColor(string.IsNullOrEmpty(p.Fill) ? "#000000" : p.Fill, settings, true);

            sb.Append("<text");
            AppendId(sb, p);
            sb.Append(" x=\"0\" y=\"0\"");
            sb.Append(" font-size=\"").Append(N(p.FontSize)).Append('"');
            if (fitBox)
                sb.Append(" textLength=\"").Append(N(p.BoxWidth)).Append("\" lengthAdjust=\"spacingAndGlyphs\"");
            if (!string.IsNullOrEmpty(p.FontFamily))
                sb.Append(" font-family=\"").Append(EscAttr(FontFamilyCss(p.FontFamily))).Append('"');
            if (!string.IsNullOrEmpty(p.TextAnchor))
                sb.Append(" text-anchor=\"").Append(EscAttr(p.TextAnchor)).Append('"');
            if (!string.IsNullOrEmpty(p.DominantBaseline))
                sb.Append(" dominant-baseline=\"").Append(EscAttr(p.DominantBaseline)).Append('"');
            if (p.Bold)
                sb.Append(" font-weight=\"bold\"");
            if (p.Italic)
                sb.Append(" font-style=\"italic\"");
            sb.Append(" fill=\"").Append(fill).Append('"');
            sb.Append(" stroke=\"none\"");

            sb.Append(" transform=\"translate(").Append(N(sx)).Append(' ').Append(N(sy)).Append(')');
            if (Math.Abs(rot) > 1e-9)
                sb.Append(" rotate(").Append(N(rot)).Append(')');
            if (!fitBox && Math.Abs(wf - 1.0) > 1e-6)
                sb.Append(" scale(").Append(N(wf)).Append(" 1)");
            sb.Append('"');

            sb.Append('>').Append(EscText(p.Text ?? "")).Append("</text>\n");
        }

        private static string PathD(List<SvgPathSeg> segs)
        {
            if (segs == null || segs.Count == 0)
                return "";
            var sb = new StringBuilder(segs.Count * 24);
            for (int i = 0; i < segs.Count; i++)
            {
                var s = segs[i];
                if (i > 0) sb.Append(' ');
                switch (s.Op)
                {
                    case SvgPathOp.Move:
                        sb.Append('M').Append(N(s.X)).Append(' ').Append(N(Ty(s.Y)));
                        break;
                    case SvgPathOp.Line:
                        sb.Append('L').Append(N(s.X)).Append(' ').Append(N(Ty(s.Y)));
                        break;
                    case SvgPathOp.Arc:
                        sb.Append('A').Append(N(s.Rx)).Append(' ').Append(N(s.Ry)).Append(' ')
                            .Append(N(-s.XRotDeg)).Append(' ')
                            .Append(s.Large ? '1' : '0').Append(' ')
                            .Append(s.SweepCcw ? '0' : '1').Append(' ')
                            .Append(N(s.X)).Append(' ').Append(N(Ty(s.Y)));
                        break;
                    case SvgPathOp.Close:
                        sb.Append('Z');
                        break;
                }
            }
            return sb.ToString();
        }

        private static void AppendId(StringBuilder sb, SvgPrimitive p)
        {
            if (!string.IsNullOrEmpty(p.Handle))
                sb.Append(" id=\"h").Append(SanitizeId(p.Handle)).Append('"');
            if (!string.IsNullOrEmpty(p.ParentHandle))
                sb.Append(" data-parent=\"h").Append(SanitizeId(p.ParentHandle)).Append('"');
        }

        private static void AppendStroke(
            StringBuilder sb, SvgPrimitive p, SvgPlotSettings settings, SvgPlotLayout layout)
        {
            string stroke = MapColor(string.IsNullOrEmpty(p.Stroke) ? "none" : p.Stroke, settings, false);
            sb.Append(" stroke=\"").Append(stroke).Append('"');
            if (stroke != "none")
            {
                if (p.StrokeWidthInDrawingUnits && p.StrokeWidthMm > 0)
                {
                    sb.Append(" stroke-width=\"").Append(N(p.StrokeWidthMm)).Append('"');
                }
                else
                {
                    double mm = settings.PlotObjectLineweights && p.StrokeWidthMm > 0
                        ? p.StrokeWidthMm
                        : DefaultLineMm;
                    double px = Math.Max(0.7, mm / 0.25);
                    sb.Append(" stroke-width=\"").Append(N(px)).Append('"');
                    sb.Append(" vector-effect=\"non-scaling-stroke\"");
                }
            }
        }

        private static void AppendFill(StringBuilder sb, SvgPrimitive p, SvgPlotSettings settings)
        {
            string fill = MapColor(string.IsNullOrEmpty(p.Fill) ? "none" : p.Fill, settings, true);
            sb.Append(" fill=\"").Append(fill).Append('"');
        }

        private static string MapColor(string color, SvgPlotSettings settings, bool isFill)
        {
            if (string.IsNullOrEmpty(color) || color == "none")
                return "none";
            if (settings.Style == SvgPlotStyle.Color)
                return color;

            if (isFill && IsNearWhite(color))
                return "#ffffff";

            if (settings.Style == SvgPlotStyle.Monochrome)
                return "#000000";

            if (!TryRgb(color, out int r, out int g, out int b))
                return "#000000";
            int y = (int)Math.Round(0.299 * r + 0.587 * g + 0.114 * b);
            if (y > 250) y = 0;
            return string.Format("#{0:X2}{1:X2}{2:X2}", y, y, y);
        }

        private static bool IsNearWhite(string color)
        {
            if (!TryRgb(color, out int r, out int g, out int b))
                return false;
            return r > 250 && g > 250 && b > 250;
        }

        private static bool TryRgb(string color, out int r, out int g, out int b)
        {
            r = g = b = 0;
            if (string.IsNullOrEmpty(color) || color[0] != '#' || color.Length < 7)
                return false;
            try
            {
                r = Convert.ToInt32(color.Substring(1, 2), 16);
                g = Convert.ToInt32(color.Substring(3, 2), 16);
                b = Convert.ToInt32(color.Substring(5, 2), 16);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static double Ty(double yDwg) => -yDwg;

        private static string N(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v))
                return "0";
            if (Math.Abs(v) < 1e-12)
                return "0";
            return v.ToString("0.####", Inv);
        }

        private static string SanitizeId(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "x";
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            if (sb.Length == 0 || (sb[0] >= '0' && sb[0] <= '9'))
                sb.Insert(0, '_');
            return sb.ToString();
        }

        private static string FontFamilyCss(string family)
        {
            if (string.IsNullOrWhiteSpace(family))
                return "\"Microsoft YaHei\", SimSun, sans-serif";
            if (family.IndexOf("Microsoft YaHei", StringComparison.OrdinalIgnoreCase) >= 0)
                return family + ", SimSun, sans-serif";
            return family + ", \"Microsoft YaHei\", SimSun, sans-serif";
        }

        private static string EscAttr(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            return s.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private static string EscText(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
