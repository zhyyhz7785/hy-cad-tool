using System;
using System.Collections.Generic;
using HyCADTool.Features.G101.Domain.Geometry;
using HyCADTool.Features.G101.Domain.Tables;

namespace HyCADTool.Features.G101.Domain.Components.Foundation
{
    /// <summary>
    /// 柱纵向钢筋在基础中构造（22G101-3 P2-10）。
    /// 分直锚 / 弯折两种情形（基础高度与 laE 关系）。
    /// </summary>
    public static class ColumnDowelBuilder
    {
        public static DetailSketch Build(G101GlobalSettings global, Dictionary<string, object> p)
        {
            int h = GetInt(p, "foundationH", 500);
            int colW = GetInt(p, "colWidth", 600);
            int count = GetInt(p, "barCount", 8);
            int spacing = GetInt(p, "barSpacing", 150);

            int cover = G101TableLookup.GetCoverThickness(global.EnvironmentClass, 2);
            int d = global.RebarDiameter;
            double laE = G101TableLookup.GetLaE(global);
            double bendD = G101TableLookup.GetBendDiameter(global.RebarGrade, d);
            bool straightAnchor = h >= laE + cover;

            var sketch = new DetailSketch
            {
                Title = straightAnchor
                    ? "柱插筋直锚（22G101-3 P2-10）"
                    : "柱插筋弯折锚固（22G101-3 P2-10）"
            };

            double ox = 0, oy = 0;
            double totalW = colW + 200;
            sketch.AddRect(ox, oy, totalW, h, SketchLayerKind.Concrete);
            sketch.AddRect(ox + 100, oy + h - colW, colW, colW, SketchLayerKind.Concrete);

            double barStartX = ox + 100 + (colW - (count - 1) * spacing) / 2.0;
            double barTopY = oy + h - cover;

            for (int i = 0; i < count; i++)
            {
                double x = barStartX + i * spacing;
                if (straightAnchor)
                {
                    sketch.AddLine(x, oy + cover, x, barTopY);
                }
                else
                {
                    double bendLen = laE - (h - 2 * cover) + bendD / 2;
                    sketch.AddLine(x, oy + cover, x, oy + h - cover - bendLen);
                    sketch.AddLine(x, oy + h - cover - bendLen, x + bendLen, oy + h - cover - bendLen);
                    sketch.AddLine(x + bendLen, oy + h - cover - bendLen, x + bendLen, barTopY);
                }
            }

            sketch.AddVerticalDim(ox - 15, oy, oy + h, $"H={h}");
            sketch.AddText(ox, oy - 25,
                straightAnchor
                    ? $"H≥laE+c={laE + cover:F0}mm → 直锚 laE={laE:F0}mm"
                    : $"H<laE+c → 底部弯折，弯弧D={bendD:F0}mm",
                2.5);
            sketch.AddText(ox, oy - 40, "依据：22G101-3 P2-10", 2.0);
            return sketch;
        }

        private static int GetInt(Dictionary<string, object> p, string key, int def)
            => p.TryGetValue(key, out var v) && v != null ? Convert.ToInt32(v) : def;
    }
}
