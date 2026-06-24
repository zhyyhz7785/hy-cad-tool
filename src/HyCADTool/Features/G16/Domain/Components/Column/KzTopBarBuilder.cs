using System;
using System.Collections.Generic;
using HyCADTool.Features.G101.Domain.Geometry;
using HyCADTool.Features.G16.Domain.Tables;

namespace HyCADTool.Features.G16.Domain.Components.Column
{
    /// <summary>边柱/角柱柱顶纵筋构造（16G101-1 第66~67页示意）。</summary>
    public static class KzTopBarBuilder
    {
        public static DetailSketch Build(G16GlobalSettings global, Dictionary<string, object> p)
        {
            int colW = GetInt(p, "colWidth", 600);
            int colH = GetInt(p, "colDepth", 600);
            int barCount = GetInt(p, "barCount", 8);
            int spacing = GetInt(p, "barSpacing", 150);
            double extendLen = GetInt(p, "extendLen", 1200);

            int cover = G16TableLookup.GetCoverThickness(global.EnvironmentClass, 2);
            double laE = G16TableLookup.GetLaE(global);
            double bendD = G16TableLookup.GetBendDiameter(global.RebarGrade, global.RebarDiameter);

            var sketch = new DetailSketch { Title = "KZ 边柱/角柱柱顶纵筋（16G101-1 第66~67页）" };
            double ox = 0, oy = 0;
            double totalH = colH + extendLen + 100;

            sketch.AddRect(ox, oy, colW, colH, SketchLayerKind.Concrete);
            double barStartX = ox + cover + (colW - 2 * cover - (barCount - 1) * spacing) / 2.0;
            double colTop = oy + colH - cover;

            for (int i = 0; i < barCount; i++)
            {
                double x = barStartX + i * spacing;
                sketch.AddLine(x, oy + cover, x, colTop);
                double hook = Math.Min(extendLen, laE);
                sketch.AddLine(x, colTop, x, colTop + hook);
                sketch.AddLine(x, colTop + hook, x - bendD / 2, colTop + hook);
            }

            sketch.AddVerticalDim(ox + colW + 15, oy, oy + colH, $"hc={colH}");
            sketch.AddVerticalDim(ox - 15, colTop, colTop + extendLen, $"伸出≥laE");
            sketch.AddText(ox, oy - 25, $"laE={laE:F0}mm；弯弧D={bendD:F0}mm", 2.5);
            sketch.AddText(ox, oy - 40, "依据：16G101-1 第66~67页（示意）", 2.0);
            return sketch;
        }

        private static int GetInt(Dictionary<string, object> p, string key, int def)
            => p.TryGetValue(key, out var v) && v != null ? Convert.ToInt32(v) : def;
    }
}
