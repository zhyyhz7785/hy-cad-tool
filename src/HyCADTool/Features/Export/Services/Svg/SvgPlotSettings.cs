using System;

namespace HyCADTool.Features.Export.Services.Svg
{
    internal enum SvgPlotArea
    {
        Window,
        Extents,
        Display,
        Selection
    }

    internal enum SvgPlotStyle
    {
        Monochrome,
        Color,
        Grayscale
    }

    internal enum SvgPlotOrientation
    {
        Portrait,
        Landscape,
        UpsideDown
    }

    internal struct SvgPlotRect
    {
        public double MinX;
        public double MinY;
        public double MaxX;
        public double MaxY;

        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;
        public bool IsValid => MaxX > MinX && MaxY > MinY && !double.IsNaN(MinX) && !double.IsInfinity(MaxX);

        public static SvgPlotRect FromPoints(double x1, double y1, double x2, double y2)
        {
            return new SvgPlotRect
            {
                MinX = Math.Min(x1, x2),
                MinY = Math.Min(y1, y2),
                MaxX = Math.Max(x1, x2),
                MaxY = Math.Max(y1, y2)
            };
        }

        public static SvgPlotRect Intersect(SvgPlotRect a, SvgPlotRect b)
        {
            return new SvgPlotRect
            {
                MinX = Math.Max(a.MinX, b.MinX),
                MinY = Math.Max(a.MinY, b.MinY),
                MaxX = Math.Min(a.MaxX, b.MaxX),
                MaxY = Math.Min(a.MaxY, b.MaxY)
            };
        }
    }

    internal sealed class SvgPaperSize
    {
        public string Id { get; }
        public string DisplayName { get; }
        public double WidthMm { get; }
        public double HeightMm { get; }
        public bool IsCustom { get; }

        public SvgPaperSize(string id, string displayName, double widthMm, double heightMm, bool isCustom = false)
        {
            Id = id;
            DisplayName = displayName;
            WidthMm = widthMm;
            HeightMm = heightMm;
            IsCustom = isCustom;
        }
    }

    internal static class SvgPaperCatalog
    {
        public static readonly SvgPaperSize[] Items =
        {
            new SvgPaperSize("ISO_A0_P", "ISO A0 纵向 (841 × 1189 mm)", 841, 1189),
            new SvgPaperSize("ISO_A0_L", "ISO A0 横向 (1189 × 841 mm)", 1189, 841),
            new SvgPaperSize("ISO_A1_P", "ISO A1 纵向 (594 × 841 mm)", 594, 841),
            new SvgPaperSize("ISO_A1_L", "ISO A1 横向 (841 × 594 mm)", 841, 594),
            new SvgPaperSize("ISO_A2_P", "ISO A2 纵向 (420 × 594 mm)", 420, 594),
            new SvgPaperSize("ISO_A2_L", "ISO A2 横向 (594 × 420 mm)", 594, 420),
            new SvgPaperSize("ISO_A3_P", "ISO A3 纵向 (297 × 420 mm)", 297, 420),
            new SvgPaperSize("ISO_A3_L", "ISO A3 横向 (420 × 297 mm)", 420, 297),
            new SvgPaperSize("ISO_A4_P", "ISO A4 纵向 (210 × 297 mm)", 210, 297),
            new SvgPaperSize("ISO_A4_L", "ISO A4 横向 (297 × 210 mm)", 297, 210),
            new SvgPaperSize("ANSI_A_P", "ANSI A 纵向 (216 × 279 mm)", 215.9, 279.4),
            new SvgPaperSize("ANSI_A_L", "ANSI A 横向 (279 × 216 mm)", 279.4, 215.9),
            new SvgPaperSize("ANSI_B_P", "ANSI B 纵向 (279 × 432 mm)", 279.4, 431.8),
            new SvgPaperSize("ANSI_B_L", "ANSI B 横向 (432 × 279 mm)", 431.8, 279.4),
            new SvgPaperSize("ANSI_C_P", "ANSI C 纵向 (432 × 559 mm)", 431.8, 558.8),
            new SvgPaperSize("ANSI_C_L", "ANSI C 横向 (559 × 432 mm)", 558.8, 431.8),
            new SvgPaperSize("ANSI_D_P", "ANSI D 纵向 (559 × 864 mm)", 558.8, 863.6),
            new SvgPaperSize("ANSI_D_L", "ANSI D 横向 (864 × 559 mm)", 863.6, 558.8),
            new SvgPaperSize("CUSTOM", "自定义", 420, 297, true)
        };

        public static SvgPaperSize Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return Items[7];
            for (int i = 0; i < Items.Length; i++)
            {
                if (string.Equals(Items[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return Items[i];
            }
            return Items[7];
        }
    }

    /// <summary>hysvg 打印式导出设置。本会话静态记住，不落盘。</summary>
    internal sealed class SvgPlotSettings
    {
        public static SvgPlotSettings Session;

        public string PaperId { get; set; } = "ISO_A3_L";
        public double CustomWidthMm { get; set; } = 420;
        public double CustomHeightMm { get; set; } = 297;

        public SvgPlotArea Area { get; set; } = SvgPlotArea.Extents;
        public bool HasWindow { get; set; }
        public double WindowX1 { get; set; }
        public double WindowY1 { get; set; }
        public double WindowX2 { get; set; }
        public double WindowY2 { get; set; }

        public bool CenterPlot { get; set; } = true;
        public double OffsetXMm { get; set; }
        public double OffsetYMm { get; set; }

        public bool FitToPaper { get; set; } = true;
        /// <summary>1 mm 纸面 = 该值个图形单位。1:100 → 100。</summary>
        public double UnitsPerMm { get; set; } = 1.0;

        public SvgPlotStyle Style { get; set; } = SvgPlotStyle.Monochrome;

        public bool PlotObjectLineweights { get; set; } = true;
        public bool ScaleLineweights { get; set; }
        public bool WhiteBackground { get; set; } = true;

        public SvgPlotOrientation Orientation { get; set; } = SvgPlotOrientation.Landscape;

        public bool IgnoreBlank { get; set; } = true;
        public bool ClipToWindow { get; set; } = true;
        public double MarginMm { get; set; } = 5;
        public bool DrawBorder { get; set; }
        public double BorderWidthMm { get; set; } = 0.25;

        public SvgPlotSettings Clone()
        {
            return (SvgPlotSettings)MemberwiseClone();
        }

        public SvgPlotRect GetWindowRect()
        {
            return SvgPlotRect.FromPoints(WindowX1, WindowY1, WindowX2, WindowY2);
        }

        public void GetPaperMm(out double widthMm, out double heightMm)
        {
            var paper = SvgPaperCatalog.Find(PaperId);
            if (paper.IsCustom)
            {
                widthMm = CustomWidthMm > 1e-6 ? CustomWidthMm : 420;
                heightMm = CustomHeightMm > 1e-6 ? CustomHeightMm : 297;
            }
            else
            {
                widthMm = paper.WidthMm;
                heightMm = paper.HeightMm;
            }

            bool wantPortrait = Orientation == SvgPlotOrientation.Portrait;
            bool wantLandscape = Orientation == SvgPlotOrientation.Landscape
                || Orientation == SvgPlotOrientation.UpsideDown;
            if (wantPortrait && widthMm > heightMm)
            {
                double t = widthMm;
                widthMm = heightMm;
                heightMm = t;
            }
            else if (wantLandscape && heightMm > widthMm)
            {
                double t = widthMm;
                widthMm = heightMm;
                heightMm = t;
            }
        }
    }

    internal struct SvgPlotLayout
    {
        public double PaperWidthMm;
        public double PaperHeightMm;
        public double ViewX;
        public double ViewY;
        public double ViewW;
        public double ViewH;
        public double UnitsPerMm;
        public SvgPlotRect ClipDwg;
        public bool UpsideDown;

        public static SvgPlotLayout Compute(SvgPlotSettings settings, SvgPlotRect content)
        {
            if (!content.IsValid)
                content = SvgPlotRect.FromPoints(0, 0, 100, 100);

            settings.GetPaperMm(out double pw, out double ph);
            double margin = Math.Max(0, settings.MarginMm);
            double printW = Math.Max(1e-6, pw - 2 * margin);
            double printH = Math.Max(1e-6, ph - 2 * margin);
            double cw = Math.Max(1e-6, content.Width);
            double ch = Math.Max(1e-6, content.Height);

            double unitsPerMm;
            if (settings.FitToPaper)
                unitsPerMm = Math.Max(cw / printW, ch / printH);
            else
                unitsPerMm = settings.UnitsPerMm > 1e-12 ? settings.UnitsPerMm : 1.0;

            double vw = pw * unitsPerMm;
            double vh = ph * unitsPerMm;
            double vx;
            double vy;
            if (settings.CenterPlot)
            {
                vx = content.MinX - (vw - cw) * 0.5;
                vy = -content.MaxY - (vh - ch) * 0.5;
            }
            else
            {
                vx = content.MinX - settings.OffsetXMm * unitsPerMm;
                vy = -content.MinY - vh + settings.OffsetYMm * unitsPerMm;
            }

            return new SvgPlotLayout
            {
                PaperWidthMm = pw,
                PaperHeightMm = ph,
                ViewX = vx,
                ViewY = vy,
                ViewW = vw,
                ViewH = vh,
                UnitsPerMm = unitsPerMm,
                ClipDwg = content,
                UpsideDown = settings.Orientation == SvgPlotOrientation.UpsideDown
            };
        }
    }
}
