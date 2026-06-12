using System;
using System.Collections.Generic;
using HyCADTool.Features.G101.Domain.Geometry;
using HyCADTool.Features.G101.Domain.Tables;

namespace HyCADTool.Features.G101.Domain.Components.Foundation
{
    /// <summary>
    /// 条形基础底板 TJBj/TJBp 配筋构造（22G101-3 P2-20/2-21，减短 10% P2-22）。
    /// </summary>
    public static class TjbFoundationBuilder
    {
        public static DetailSketch Build(G101GlobalSettings global, Dictionary<string, object> p)
        {
            int widthB = GetInt(p, "widthB", 2000);
            int h1 = GetInt(p, "heightH1", 300);
            int h2 = GetInt(p, "heightH2", 250);
            bool isSlope = GetBool(p, "isSlope", true);
            int segLen = GetInt(p, "segmentLen", 6000);
            int mainSp = GetInt(p, "mainSpacing", 150);
            int distSp = GetInt(p, "distSpacing", 250);
            int beamW = GetInt(p, "beamWidth", 400);
            bool drawPlan = GetBool(p, "drawPlan", true);
            bool drawSection = GetBool(p, "drawSection", true);

            int cover = G101TableLookup.GetCoverThickness(global.EnvironmentClass, 2);
            int d = global.RebarDiameter;
            double hook = G101TableLookup.GetStirrupHookLength(d);

            var sketch = new DetailSketch { Title = "TJB 底板配筋（22G101-3 P2-20/21）" };
            double ox = 0;

            if (drawSection)
            {
                DrawSection(sketch, ox, 0, widthB, h1, h2, isSlope, cover, hook, mainSp, distSp);
                ox += widthB + 500;
            }

            if (drawPlan)
                DrawPlanSegment(sketch, ox, 0, segLen, widthB, beamW, mainSp, distSp, cover, segLen >= 2500);

            sketch.AddText(0, -35, "依据：22G101-3 P2-20/21；P2-22 减短10%（端部第一根不缩短）");
            sketch.AddText(0, -50, "P2-20 注：梁宽内不设分布筋；交接处分布筋搭接150mm", 2.0);
            return sketch;
        }

        private static void DrawSection(DetailSketch s, double ox, double oy, int b, int h1, int h2, bool isSlope,
            int cover, double hook, int mainSp, int distSp)
        {
            if (isSlope)
            {
                var pl = new SketchPolyline { Closed = true, Layer = SketchLayerKind.Concrete };
                pl.Points.Add(new SketchPoint2(ox, oy));
                pl.Points.Add(new SketchPoint2(ox + b, oy));
                pl.Points.Add(new SketchPoint2(ox + b, oy + h2));
                pl.Points.Add(new SketchPoint2(ox, oy + h1));
                s.Polylines.Add(pl);
            }
            else
            {
                s.AddRect(ox, oy, b, h1);
            }

            double yBot = oy + cover;
            for (double x = ox + cover; x <= ox + b - cover + 1; x += mainSp)
            {
                s.AddLine(x, yBot, x, yBot + hook);
                s.AddLine(x, yBot + hook, x + hook * 0.7, yBot + hook);
            }

            for (double x = ox + cover + distSp / 2.0; x <= ox + b - cover; x += distSp)
                s.AddLine(x, yBot + 15, x, yBot + 35);

            s.AddVerticalDim(ox - 15, oy, oy + (isSlope ? h1 : h1), isSlope ? $"h1={h1}/h2={h2}" : $"h={h1}");
            s.AddHorizontalDim(ox, ox + b, oy - 18, $"b={b}");
        }

        private static void DrawPlanSegment(DetailSketch s, double ox, double oy, int len, int b, int beamW,
            int mainSp, int distSp, int cover, bool reduce10)
        {
            s.AddRect(ox, oy, len, b);
            double beamX = ox + (len - beamW) / 2.0;
            s.AddRect(beamX, oy, beamW, b, SketchLayerKind.Concrete);

            double innerLeft = ox + cover;
            double innerRight = ox + len - cover;
            double innerBottom = oy + cover;
            double innerTop = oy + b - cover;
            double fullMain = innerRight - innerLeft;

            int idx = 0;
            for (double y = innerBottom; y <= innerTop + 1; y += mainSp, idx++)
            {
                if (y >= beamX - 1 && y <= beamX + beamW + 1) continue;
                bool outer = idx == 0 || y + mainSp > innerTop + 0.5;
                bool shorten = reduce10 && !outer && idx != 1;
                double barLen = shorten ? fullMain * 0.9 : fullMain;
                double x0 = shorten && idx % 2 == 0 ? innerLeft + (fullMain - barLen) : innerLeft;
                s.AddLine(x0, y, x0 + barLen, y);
            }

            for (double x = innerLeft; x <= innerRight; x += distSp)
            {
                if (x >= beamX && x <= beamX + beamW) continue;
                s.AddLine(x, innerBottom, x, innerTop);
            }

            s.AddHorizontalDim(ox, ox + len, oy - 20, $"L={len}");
        }

        private static int GetInt(Dictionary<string, object> p, string key, int def)
            => p.TryGetValue(key, out var v) && v != null ? Convert.ToInt32(v) : def;

        private static bool GetBool(Dictionary<string, object> p, string key, bool def)
            => p.TryGetValue(key, out var v) && v is bool b ? b : def;
    }
}
