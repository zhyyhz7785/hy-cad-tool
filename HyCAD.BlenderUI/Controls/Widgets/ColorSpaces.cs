using System;
using System.Windows.Media;

namespace HyCAD.BlenderUI.Controls.Widgets
{
    /// <summary>RGB ↔ HSV（Blender 色盘与 HSV 方块共用）。</summary>
    internal static class ColorSpaces
    {
        public static void RgbToHsv(Color rgb, out double h, out double s, out double v)
        {
            double r = rgb.R / 255.0, g = rgb.G / 255.0, b = rgb.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            v = max;
            s = max <= 1e-9 ? 0 : delta / max;

            if (delta < 1e-9)
            {
                h = 0;
                return;
            }

            if (max == r) h = 60 * (((g - b) / delta) % 6);
            else if (max == g) h = 60 * (((b - r) / delta) + 2);
            else h = 60 * (((r - g) / delta) + 4);

            if (h < 0) h += 360;
        }

        public static Color HsvToRgb(double h, double s, double v)
        {
            h = ((h % 360) + 360) % 360;
            s = Math.Max(0, Math.Min(1, s));
            v = Math.Max(0, Math.Min(1, v));

            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60 % 2) - 1));
            double m = v - c;

            double rp = 0, gp = 0, bp = 0;
            if (h < 60) { rp = c; gp = x; }
            else if (h < 120) { rp = x; gp = c; }
            else if (h < 180) { gp = c; bp = x; }
            else if (h < 240) { gp = x; bp = c; }
            else if (h < 300) { rp = x; bp = c; }
            else { rp = c; bp = x; }

            return Color.FromRgb(
                (byte)Math.Round((rp + m) * 255),
                (byte)Math.Round((gp + m) * 255),
                (byte)Math.Round((bp + m) * 255));
        }
    }
}
