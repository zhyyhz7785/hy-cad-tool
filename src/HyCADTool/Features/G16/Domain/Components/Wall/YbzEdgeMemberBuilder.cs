using System;
using System.Collections.Generic;
using HyCADTool.Features.G101.Domain.Geometry;
using HyCADTool.Features.G16.Domain.Tables;

namespace HyCADTool.Features.G16.Domain.Components.Wall
{
    /// <summary>约束边缘构件 YBZ/GBZ（16G101-1 第75~76页示意）。</summary>
    public static class YbzEdgeMemberBuilder
    {
        public static DetailSketch Build(G16GlobalSettings global, Dictionary<string, object> p)
        {
            int wallT = GetInt(p, "wallThickness", 300);
            int edgeW = GetInt(p, "edgeWidth", 300);
            int edgeH = GetInt(p, "edgeHeight", 2520);
            int vBarCount = GetInt(p, "vBarCount", 12);
            int hSpacing = GetInt(p, "hSpacing", 150);
            int vSpacing = GetInt(p, "vSpacing", 150);

            int cover = G16TableLookup.GetCoverThickness(global.EnvironmentClass, 1);
            double laE = G16TableLookup.GetLaE(global);

            var sketch = new DetailSketch { Title = "约束边缘构件 YBZ（16G101-1 第75~76页）" };
            double ox = 0, oy = 0;

            sketch.AddRect(ox, oy, wallT + edgeW, edgeH, SketchLayerKind.Concrete);
            sketch.AddRect(ox + wallT, oy, edgeW, edgeH, SketchLayerKind.Concrete);

            double innerL = ox + wallT + cover;
            double innerR = ox + wallT + edgeW - cover;
            double innerB = oy + cover;
            double innerT = oy + edgeH - cover;

            for (int i = 0; i < vBarCount; i++)
            {
                double x = innerL + (innerR - innerL) * i / Math.Max(1, vBarCount - 1);
                sketch.AddLine(x, innerB, x, innerT);
            }
            for (double y = innerB; y <= innerT; y += hSpacing)
                sketch.AddLine(innerL, y, innerR, y);

            sketch.AddLine(innerL, innerB, innerR, innerB);
            sketch.AddLine(innerL, innerT, innerR, innerT);
            sketch.AddLine(innerL, innerB, innerL, innerT);
            sketch.AddLine(innerR, innerB, innerR, innerT);

            sketch.AddHorizontalDim(ox + wallT, ox + wallT + edgeW, oy - 20, $"bc={edgeW}");
            sketch.AddVerticalDim(ox - 15, oy, oy + edgeH, $"h={edgeH}");
            sketch.AddText(ox, oy - 45, $"竖向 {vBarCount} 根 @ {vSpacing}；水平 @ {hSpacing}；laE={laE:F0}mm", 2.5);
            sketch.AddText(ox, oy - 60, "依据：16G101-1 第75~76页（示意）", 2.0);
            return sketch;
        }

        private static int GetInt(Dictionary<string, object> p, string key, int def)
            => p.TryGetValue(key, out var v) && v != null ? Convert.ToInt32(v) : def;
    }
}
