using System;
using System.Collections.Generic;
using HyCADTool.Features.G101.Domain.Geometry;
using HyCADTool.Features.G16.Domain.Tables;

namespace HyCADTool.Features.G16.Domain.Components.Wall
{
    /// <summary>剪力墙连梁 LL 配筋（16G101-1 第78页示意）。</summary>
    public static class LlCouplingBeamBuilder
    {
        public static DetailSketch Build(G16GlobalSettings global, Dictionary<string, object> p)
        {
            int beamW = GetInt(p, "beamWidth", 300);
            int beamH = GetInt(p, "beamHeight", 600);
            int topCount = GetInt(p, "topBarCount", 4);
            int botCount = GetInt(p, "bottomBarCount", 4);
            int stirrupSp = GetInt(p, "stirrupSpacing", 100);
            int sideCount = GetInt(p, "sideBarCount", 0);

            int cover = G16TableLookup.GetCoverThickness(global.EnvironmentClass, 2);
            double laE = G16TableLookup.GetLaE(global);

            var sketch = new DetailSketch { Title = "连梁 LL 配筋（16G101-1 第78页）" };
            double ox = 0, oy = 0;

            sketch.AddRect(ox, oy, beamW, beamH, SketchLayerKind.Concrete);
            double innerL = ox + cover;
            double innerR = ox + beamW - cover;
            double innerB = oy + cover;
            double innerT = oy + beamH - cover;

            for (int i = 0; i < topCount; i++)
            {
                double x = innerL + (innerR - innerL) * i / Math.Max(1, topCount - 1);
                sketch.AddLine(x, innerT - global.RebarDiameter, x, innerT - global.RebarDiameter * 2);
            }
            for (int i = 0; i < botCount; i++)
            {
                double x = innerL + (innerR - innerL) * i / Math.Max(1, botCount - 1);
                sketch.AddLine(x, innerB + global.RebarDiameter, x, innerB + global.RebarDiameter * 2);
            }
            for (double y = innerB; y <= innerT; y += stirrupSp)
                sketch.AddLine(innerL, y, innerR, y);

            sketch.AddLine(innerL, innerB, innerR, innerB);
            sketch.AddLine(innerL, innerT, innerR, innerT);
            sketch.AddLine(innerL, innerB, innerL, innerT);
            sketch.AddLine(innerR, innerB, innerR, innerT);

            if (sideCount > 0)
                sketch.AddText(ox + beamW + 10, oy + beamH / 2, $"侧面筋 {sideCount} 根", 2.5);

            sketch.AddHorizontalDim(ox, ox + beamW, oy - 20, $"b={beamW}");
            sketch.AddVerticalDim(ox + beamW + 15, oy, oy + beamH, $"h={beamH}");
            sketch.AddText(ox, oy - 45, $"箍筋 @ {stirrupSp}；laE={laE:F0}mm", 2.5);
            sketch.AddText(ox, oy - 60, "依据：16G101-1 第78页（示意）", 2.0);
            return sketch;
        }

        private static int GetInt(Dictionary<string, object> p, string key, int def)
            => p.TryGetValue(key, out var v) && v != null ? Convert.ToInt32(v) : def;
    }
}
