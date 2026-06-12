using System;
using System.Collections.Generic;
using HyCADTool.Features.G101.Domain.Geometry;
using HyCADTool.Features.G101.Domain.Tables;

namespace HyCADTool.Features.G101.Domain.Components.Stair
{
    /// <summary>
    /// AT 型楼梯板配筋构造剖面（22G101-2 P2-10）。
    /// </summary>
    public static class AtStairBuilder
    {
        public static DetailSketch Build(G101GlobalSettings global, Dictionary<string, object> p)
        {
            int n = GetInt(p, "stepCount", 12);
            int h = GetInt(p, "riseH", 150);
            int b = GetInt(p, "runB", 280);
            int t = GetInt(p, "slabT", 120);
            int ln = GetInt(p, "ln", 3360);
            int distSp = GetInt(p, "distSpacing", 250);

            int cover = G101TableLookup.GetCoverThickness(global.EnvironmentClass, 0);
            int d = global.RebarDiameter;
            double negLen = ln / 4.0;

            var sketch = new DetailSketch { Title = "AT 型楼梯板配筋（22G101-2 P2-10）" };

            double ox = 0, oy = 0;
            double totalRun = n * b;
            double totalRise = n * h;

            // 踏步轮廓（折线）
            var outline = new SketchPolyline { Closed = false, Layer = SketchLayerKind.Concrete };
            outline.Points.Add(new SketchPoint2(ox, oy));
            for (int i = 0; i < n; i++)
            {
                outline.Points.Add(new SketchPoint2(ox + i * b, oy + (i + 1) * h));
                outline.Points.Add(new SketchPoint2(ox + (i + 1) * b, oy + (i + 1) * h));
            }
            outline.Points.Add(new SketchPoint2(ox + totalRun, oy));
            outline.Points.Add(new SketchPoint2(ox, oy));
            sketch.Polylines.Add(outline);

            // 梯板下缘斜线
            sketch.AddLine(ox, oy + t, ox + totalRun, oy + totalRise + t,
                SketchLayerKind.Concrete, SketchLineKind.Solid);

            // 下部纵筋（沿板底）
            double botY = oy + cover;
            sketch.AddLine(ox + cover, botY, ox + totalRun - cover, botY + totalRise);

            // 上部支座负筋（低、高端各 ln/4）
            double topY = oy + totalRise + t - cover;
            sketch.AddLine(ox + cover, topY, ox + cover + negLen, topY);
            sketch.AddText(ox + cover, topY + 10, $"低端 ln/4={negLen:F0}", 2.5);
            double topX = ox + totalRun - negLen;
            sketch.AddLine(topX, topY, ox + totalRun - cover, topY);
            sketch.AddText(topX - 20, topY + 10, $"高端 ln/4={negLen:F0}", 2.5);

            // 分布筋示意
            for (double x = ox + cover; x < totalRun; x += distSp)
            {
                double ratio = (x - ox) / totalRun;
                double y1 = botY + ratio * totalRise;
                sketch.AddLine(x, y1, x, y1 + 30, SketchLayerKind.Rebar, SketchLineKind.Rebar);
            }

            sketch.AddHorizontalDim(ox, ox + totalRun, oy - 25, $"L={totalRun:F0}");
            sketch.AddVerticalDim(ox - 20, oy, oy + totalRise, $"H={totalRise}");
            sketch.AddText(ox, oy - 45, $"t={t}  h={h}  b={b}  n={n}  d={d}", 2.5);
            sketch.AddText(ox, oy - 60, "依据：22G101-2 P2-10", 2.0);
            return sketch;
        }

        private static int GetInt(Dictionary<string, object> p, string key, int def)
            => p.TryGetValue(key, out var v) && v != null ? Convert.ToInt32(v) : def;
    }
}
