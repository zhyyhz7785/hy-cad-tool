using System;
using System.Collections.Generic;
using HyCADTool.Features.G101.Domain.Geometry;
using HyCADTool.Features.G101.Domain.Tables;

namespace HyCADTool.Features.G101.Domain.Components.Stair
{
    /// <summary>
    /// BT 型楼梯板配筋构造剖面（22G101-2 P2-10）。
    /// 梯板 = 低端平板 + 踏步段。
    /// </summary>
    public static class BtStairBuilder
    {
        public static DetailSketch Build(G101GlobalSettings global, Dictionary<string, object> p)
        {
            int lowPlateLen = GetInt(p, "lowPlateLen", 900);
            int n = GetInt(p, "stepCount", 10);
            int h = GetInt(p, "riseH", 150);
            int b = GetInt(p, "runB", 280);
            int t = GetInt(p, "slabT", 120);
            int ln = GetInt(p, "ln", 3700);
            int distSp = GetInt(p, "distSpacing", 250);

            int cover = G101TableLookup.GetCoverThickness(global.EnvironmentClass, 0);
            int d = global.RebarDiameter;
            double negLen = ln / 4.0;
            double lab = G101TableLookup.GetLab(global);
            double anchor035 = 0.35 * lab;

            var sketch = new DetailSketch { Title = "BT 型楼梯板配筋（22G101-2 P2-10）" };

            double ox = 0, oy = 0;
            double stepRun = n * b;
            double totalRun = lowPlateLen + stepRun;
            double totalRise = n * h;

            sketch.AddRect(ox, oy, lowPlateLen, t, SketchLayerKind.Concrete);

            var outline = new SketchPolyline { Closed = false, Layer = SketchLayerKind.Concrete };
            outline.Points.Add(new SketchPoint2(ox + lowPlateLen, oy + t));
            for (int i = 0; i < n; i++)
            {
                outline.Points.Add(new SketchPoint2(ox + lowPlateLen + i * b, oy + t + (i + 1) * h));
                outline.Points.Add(new SketchPoint2(ox + lowPlateLen + (i + 1) * b, oy + t + (i + 1) * h));
            }
            outline.Points.Add(new SketchPoint2(ox + totalRun, oy + t));
            outline.Points.Add(new SketchPoint2(ox + lowPlateLen, oy + t));
            sketch.Polylines.Add(outline);

            double botY = oy + cover;
            sketch.AddLine(ox + cover, botY, ox + totalRun - cover, botY + totalRise);

            double topY = oy + totalRise + t - cover;
            sketch.AddLine(ox + cover, topY, ox + cover + negLen, topY);
            sketch.AddLine(ox + totalRun - negLen, topY, ox + totalRun - cover, topY);
            sketch.AddText(ox + cover, topY + 12, $"ln/4={negLen:F0}", 2.5);

            for (double x = ox + cover; x < totalRun; x += distSp)
            {
                double ratio = x <= ox + lowPlateLen
                    ? 0
                    : (x - ox - lowPlateLen) / stepRun;
                double y1 = oy + cover + ratio * totalRise;
                sketch.AddLine(x, y1, x, y1 + 30);
            }

            sketch.AddHorizontalDim(ox, ox + lowPlateLen, oy - 18, "低端平板");
            sketch.AddHorizontalDim(ox + lowPlateLen, ox + totalRun, oy - 18, "踏步段");
            sketch.AddHorizontalDim(ox, ox + totalRun, oy - 35, $"跨度={totalRun:F0}");
            sketch.AddText(ox, oy - 55, $"t={t}  h={h}  b={b}  n={n}  ln={ln}", 2.5);
            sketch.AddText(ox, oy - 70, $"上部筋铰接锚固 0.35lab={anchor035:F0}mm", 2.5);
            sketch.AddText(ox, oy - 85, "依据：22G101-2 P2-10", 2.0);
            return sketch;
        }

        private static int GetInt(Dictionary<string, object> p, string key, int def)
            => p.TryGetValue(key, out var v) && v != null ? Convert.ToInt32(v) : def;
    }
}
