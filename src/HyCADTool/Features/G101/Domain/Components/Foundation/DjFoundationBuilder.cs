using System;
using System.Collections.Generic;
using HyCADTool.Features.G101.Domain.Geometry;
using HyCADTool.Features.G101.Domain.Tables;

namespace HyCADTool.Features.G101.Domain.Components.Foundation
{
    /// <summary>
    /// 独立基础 DJj/DJz 底板配筋构造（22G101-3 P2-11、P2-14 缩减 10%）。
    /// </summary>
    public static class DjFoundationBuilder
    {
        public static DetailSketch Build(G101GlobalSettings global, Dictionary<string, object> p)
        {
            int a = GetInt(p, "lengthA", 2400);
            int b = GetInt(p, "widthB", 2400);
            int h = GetInt(p, "heightH", 600);
            int col = GetInt(p, "colSize", 600);
            int spX = GetInt(p, "spacingX", 200);
            int spY = GetInt(p, "spacingY", 200);
            bool plan = GetBool(p, "drawPlan", true);
            bool section = GetBool(p, "drawSection", true);

            int cover = G101TableLookup.GetCoverThickness(global.EnvironmentClass, 2);
            double laE = G101TableLookup.GetLaE(global);
            var sketch = new DetailSketch { Title = "DJ 底板配筋（22G101-3 P2-11）" };

            double offsetX = 0;
            if (plan)
            {
                DrawPlan(sketch, offsetX, 0, a, b, col, spX, spY, cover, a >= 2500);
                offsetX += a + 400;
            }

            if (section)
                DrawSection(sketch, offsetX, 0, a, h, col, cover, laE, global.RebarDiameter);

            sketch.AddText(0, -30, $"依据：22G101-3 P2-11；laE={laE:F0}mm（{G101TableLookup.RefLa.Display}）");
            return sketch;
        }

        private static void DrawPlan(DetailSketch s, double ox, double oy, int a, int b, int col, int spX, int spY, int cover, bool reduce10)
        {
            s.AddRect(ox, oy, a, b);
            s.AddRect(ox + (a - col) / 2.0, oy + (b - col) / 2.0, col, col, SketchLayerKind.Concrete);

            double innerLeft = ox + cover;
            double innerRight = ox + a - cover;
            double innerBottom = oy + cover;
            double innerTop = oy + b - cover;
            double fullLenX = innerRight - innerLeft;
            double fullLenY = innerTop - innerBottom;
            bool reduceX = reduce10 && a >= 2500;
            bool reduceY = reduce10 && b >= 2500;

            int rowIdx = 0;
            for (double y = innerBottom; y <= innerTop + 1; y += spY, rowIdx++)
            {
                bool outerRow = rowIdx == 0 || y + spY > innerTop + 0.5;
                GetBarSpan(innerLeft, fullLenX, reduceX && !outerRow, rowIdx, out double x0, out double len);
                s.AddLine(x0, y, x0 + len, y);
            }

            int colIdx = 0;
            for (double x = innerLeft; x <= innerRight + 1; x += spX, colIdx++)
            {
                bool outerCol = colIdx == 0 || x + spX > innerRight + 0.5;
                GetBarSpan(innerBottom, fullLenY, reduceY && !outerCol, colIdx, out double y0, out double len);
                s.AddLine(x, y0, x, y0 + len);
            }

            s.AddHorizontalDim(ox, ox + a, oy - 20, $"A={a}");
            s.AddVerticalDim(ox - 20, oy, oy + b, $"B={b}");
            if (reduce10)
                s.AddText(ox, oy + b + 15, "P2-14：内部筋0.9L交错，四边最外侧筋不缩短", 2.5);
        }

        /// <summary>P2-14：内部筋 0.9 倍长度，奇偶行交错靠端布置。</summary>
        private static void GetBarSpan(double origin, double fullLen, bool shorten, int index, out double start, out double len)
        {
            if (!shorten)
            {
                start = origin;
                len = fullLen;
                return;
            }
            len = fullLen * 0.9;
            start = index % 2 == 0
                ? origin
                : origin + (fullLen - len);
        }

        private static void DrawSection(DetailSketch s, double ox, double oy, int a, int h, int col, int cover, double laE, int d)
        {
            s.AddRect(ox, oy, a, h);
            s.AddRect(ox + (a - col) / 2.0, oy + h - col, col, col, SketchLayerKind.Concrete);

            double yBot = oy + cover;
            s.AddLine(ox + cover, yBot, ox + a - cover, yBot);

            double hook = G101TableLookup.GetStirrupHookLength(d);
            s.AddLine(ox + cover, yBot, ox + cover, yBot + laE);
            s.AddLine(ox + cover, yBot + laE, ox + cover + hook, yBot + laE);

            s.AddVerticalDim(ox - 15, oy, oy + h, $"H={h}");
            s.AddText(ox + a + 10, oy + h / 2.0, $"laE={laE:F0}", 2.5);
        }

        private static int GetInt(Dictionary<string, object> p, string key, int def)
            => p.TryGetValue(key, out var v) && v != null ? Convert.ToInt32(v) : def;

        private static bool GetBool(Dictionary<string, object> p, string key, bool def)
            => p.TryGetValue(key, out var v) && v is bool b ? b : def;
    }
}
