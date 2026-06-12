using System;
using System.Collections.Generic;
using HyCADTool.Features.G101.Domain.Geometry;
using HyCADTool.Features.G101.Domain.Tables;

namespace HyCADTool.Features.G101.Domain.Components.Foundation
{
    /// <summary>
    /// 双柱普通独立基础 DJj/DJz 底部与顶部配筋（22G101-3 P2-12）。
    /// </summary>
    public static class DoubleColumnDjBuilder
    {
        public static DetailSketch Build(G101GlobalSettings global, Dictionary<string, object> p)
        {
            int a = GetInt(p, "lengthA", 3600);
            int b = GetInt(p, "widthB", 2400);
            int h = GetInt(p, "heightH", 600);
            int colW = GetInt(p, "colWidth", 600);
            int colSpacing = GetInt(p, "colSpacing", 2400);
            int bottomSp = GetInt(p, "bottomSpacing", 200);
            int topBarCount = GetInt(p, "topBarCount", 11);
            int topSp = GetInt(p, "topSpacing", 100);

            int cover = G101TableLookup.GetCoverThickness(global.EnvironmentClass, 2);
            double laE = G101TableLookup.GetLaE(global);

            var sketch = new DetailSketch { Title = "双柱独基配筋（22G101-3 P2-12）" };
            double ox = 0, oy = 0;

            sketch.AddRect(ox, oy, a, h);
            double col1X = ox + (a - colSpacing) / 2.0 - colW / 2.0;
            double col2X = col1X + colSpacing;
            sketch.AddRect(col1X, oy + h - colW, colW, colW, SketchLayerKind.Concrete);
            sketch.AddRect(col2X, oy + h - colW, colW, colW, SketchLayerKind.Concrete);

            double yBot = oy + cover;
            double yTop = oy + h - cover - 30;

            for (double x = ox + cover; x <= ox + a - cover + 1; x += bottomSp)
                sketch.AddLine(x, yBot, x, yBot + laE * 0.3);

            double extendX = System.Math.Max(a - 2 * cover, colSpacing + colW);
            double extendY = b - 2 * cover;
            bool xLarger = extendX >= extendY;
            for (double y = oy + cover; y <= oy + b - cover + 1; y += bottomSp)
            {
                double len = xLarger ? extendY : extendX;
                sketch.AddLine(ox + cover, y, ox + cover + len, y);
            }

            double midX = ox + a / 2.0;
            double topZoneHalf = (topBarCount - 1) * topSp / 2.0;
            for (int i = 0; i < topBarCount; i++)
            {
                double x = midX - topZoneHalf + i * topSp;
                sketch.AddLine(x, yTop, x, oy + h - cover);
            }

            for (double x = midX - topZoneHalf - 100; x <= midX + topZoneHalf + 100; x += topSp * 2)
                sketch.AddLine(x, yTop - 15, x, yTop - 5);

            sketch.AddHorizontalDim(ox, ox + a, oy - 20, $"A={a}");
            sketch.AddVerticalDim(ox - 18, oy, oy + h, $"H={h}");
            sketch.AddText(ox, oy - 40, $"T: {topBarCount}根@{topSp}（双柱中心线对称）", 2.5);
            sketch.AddText(ox, oy - 55, xLarger ? "底筋：X向伸出大者在下（P2-12 注2）" : "底筋：Y向伸出大者在下", 2.0);
            sketch.AddText(ox, oy - 70, "依据：22G101-3 P2-12", 2.0);
            return sketch;
        }

        private static int GetInt(Dictionary<string, object> p, string key, int def)
            => p.TryGetValue(key, out var v) && v != null ? Convert.ToInt32(v) : def;
    }
}
