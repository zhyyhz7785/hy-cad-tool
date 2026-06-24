using System;
using System.Collections.Generic;
using HyCADTool.Features.G101.Domain.Geometry;
using HyCADTool.Features.G16.Domain.Tables;

namespace HyCADTool.Features.G16.Domain.Components.Column
{
    /// <summary>框架柱箍筋加密区（16G101-1 第62~66页示意）。</summary>
    public static class KzStirrupZoneBuilder
    {
        public static DetailSketch Build(G16GlobalSettings global, Dictionary<string, object> p)
        {
            int colW = GetInt(p, "colWidth", 600);
            int colH = GetInt(p, "colDepth", 600);
            int zoneH = GetInt(p, "encryptHeight", 500);
            int spEnc = GetInt(p, "spacingEncrypt", 100);
            int spNorm = GetInt(p, "spacingNormal", 150);
            int legCount = GetInt(p, "legCount", 4);

            int cover = G16TableLookup.GetCoverThickness(global.EnvironmentClass, 2);
            int d = global.RebarDiameter;
            double laE = G16TableLookup.GetLaE(global);

            var sketch = new DetailSketch { Title = "KZ 箍筋加密区（16G101-1 第62~66页）" };
            double ox = 0, oy = 0;

            sketch.AddRect(ox, oy, colW, colH, SketchLayerKind.Concrete);
            double innerL = ox + cover;
            double innerR = ox + colW - cover;
            double innerB = oy + cover;
            double innerT = oy + colH - cover;

            for (int i = 0; i < legCount; i++)
            {
                double x = innerL + (innerR - innerL) * i / Math.Max(1, legCount - 1);
                sketch.AddLine(x, innerB, x, innerT);
            }
            sketch.AddLine(innerL, innerB, innerR, innerB);
            sketch.AddLine(innerL, innerT, innerR, innerT);
            sketch.AddLine(innerL, innerB, innerL, innerT);
            sketch.AddLine(innerR, innerB, innerR, innerT);

            double zoneTop = innerT;
            double zoneBot = Math.Max(innerB, zoneTop - zoneH);
            for (double y = zoneBot; y <= zoneTop + 1; y += spEnc)
                sketch.AddLine(innerL, y, innerR, y, SketchLayerKind.Rebar, SketchLineKind.Rebar);

            sketch.AddHorizontalDim(ox, ox + colW, oy - 20, $"bc={colW}");
            sketch.AddVerticalDim(ox + colW + 15, oy, oy + colH, $"hc={colH}");
            sketch.AddVerticalDim(ox - 15, zoneBot, zoneTop, $"Hn={zoneH:F0}");
            sketch.AddText(ox, oy - 45, $"加密区 @ {spEnc}；非加密 @ {spNorm}；laE={laE:F0}mm", 2.5);
            sketch.AddText(ox, oy - 60, "依据：16G101-1 第62~66页（示意）", 2.0);
            return sketch;
        }

        private static int GetInt(Dictionary<string, object> p, string key, int def)
            => p.TryGetValue(key, out var v) && v != null ? Convert.ToInt32(v) : def;
    }
}
