using System;
using System.Collections.Generic;
using HyCADTool.Features.G101.Domain.Geometry;
using HyCADTool.Features.G101.Domain.Tables;
using HyCADTool.Features.G16.Domain.Tables;

namespace HyCADTool.Features.G16.Domain.Components.Tables
{
    /// <summary>16G101-1 第53页 lab/labE/la/ζ_a 三表落图。</summary>
    public static class Page53AnchorTableBuilder
    {
        public static DetailSketch Build(G16GlobalSettings global, Dictionary<string, object> p)
        {
            double th = GetD(p, "textHeight", 250);
            bool draw1 = GetB(p, "drawTable1", true);
            bool draw2 = GetB(p, "drawTable2", true);
            bool draw3 = GetB(p, "drawTable3", true);
            bool highlight = GetB(p, "highlightSelection", true);

            var sketch = new DetailSketch { Title = "16G101-1 第53页 受拉钢筋锚固长度查表" };
            double yTop = 0;
            const double gap = 600;

            if (draw1)
            {
                var h1 = DrawTable1(sketch, 0, yTop, th, global, highlight);
                yTop -= h1 + gap;
            }

            if (draw2 || draw3)
            {
                double t2w = MeasureTable2Width(th);
                double t2h = 0;
                double t3h = 0;
                if (draw2)
                    t2h = DrawTable2(sketch, 0, yTop, th);
                if (draw3)
                    t3h = DrawTable3(sketch, draw2 ? t2w + gap : 0, yTop, th);
                if (draw2)
                    DrawFootnotes(sketch, 0, yTop - Math.Max(t2h, t3h) - th * 0.6, th);
            }

            return sketch;
        }

        private static double DrawTable1(DetailSketch sketch, double ox, double oy, double th,
            G16GlobalSettings global, bool highlight)
        {
            double steelW = th * 5.5;
            double rowW = th * 4.5;
            double colW = th * 1.35;
            var cols = G16Page53Tables.ConcreteColumns;
            int dataCols = cols.Length;
            int dataRows = G16Page53Tables.LabRows.Count;
            double tableW = steelW + rowW + dataCols * colW;
            double headerH = th * 1.6;
            double rowH = th * 1.35;
            double tableH = headerH + dataRows * rowH;

            var colWidths = new double[2 + dataCols];
            colWidths[0] = steelW;
            colWidths[1] = rowW;
            for (int i = 0; i < dataCols; i++)
                colWidths[2 + i] = colW;

            DrawGrid(sketch, ox, oy, tableW, tableH, colWidths, headerH, rowH, dataRows);

            FillCell(sketch, ox, oy - headerH, steelW + rowW, headerH, th,
                "受拉钢筋的基本锚固长度 lab、labE", center: true);
            double cx = ox + steelW + rowW;
            for (int i = 0; i < dataCols; i++)
                FillCell(sketch, cx + i * colW, oy - headerH, colW, headerH, th * 0.85, cols[i], center: true);

            Page53LabRow highlightRow = null;
            if (highlight)
            {
                highlightRow = G16Page53Tables.FindLabRow(
                    G16Page53Tables.MapRebarGrade(global.RebarGrade),
                    global.SeismicGrade == SeismicGrade.Grade4
                        ? Page53LabRowKind.Grade4OrNonSeismic
                        : G16Page53Tables.MapSeismicRow(global.SeismicGrade));
            }
            int colHighlight = G16Page53Tables.ConcreteColumnIndex(global.ConcreteGrade);

            double y = oy - headerH;
            foreach (var row in G16Page53Tables.LabRows)
            {
                if (!string.IsNullOrEmpty(row.SteelLabel))
                    FillCell(sketch, ox, y - rowH * 3, steelW, rowH * 3, th * 0.75, row.SteelLabel, valignTop: true);
                FillCell(sketch, ox + steelW, y - rowH, rowW, rowH, th * 0.72, row.RowLabel);
                cx = ox + steelW + rowW;
                for (int i = 0; i < dataCols; i++)
                {
                    if (highlight && row == highlightRow && i == colHighlight)
                        sketch.AddRect(cx + i * colW + 15, y - rowH + 15, colW - 30, rowH - 30, SketchLayerKind.Rebar);
                    FillCell(sketch, cx + i * colW, y - rowH, colW, rowH, th * 0.8, row.Values[i], center: true);
                }
                y -= rowH;
            }

            return tableH;
        }

        private static double DrawTable2(DetailSketch sketch, double ox, double oy, double th)
        {
            double w = MeasureTable2Width(th);
            double lineH = th * 1.35;
            int lines = G16Page53Tables.LaFormulaLines.Length;
            double h = lineH * lines + th * 0.5;
            sketch.AddRect(ox, oy - h, w, h, SketchLayerKind.Concrete);
            double y = oy - th * 0.4;
            foreach (var line in G16Page53Tables.LaFormulaLines)
            {
                sketch.AddText(ox + th * 0.3, y, line, th * 0.85);
                y -= lineH;
            }
            return h;
        }

        private static double MeasureTable2Width(double th) => th * 28;

        private static double DrawTable3(DetailSketch sketch, double ox, double oy, double th)
        {
            double condW = th * 12;
            double valW = th * 3;
            double width = condW + valW;
            double headerH = th * 1.4;
            double rowH = th * 1.35;
            int rows = G16Page53Tables.ZetaRows.Count;
            double h = headerH + rows * rowH;

            DrawGrid(sketch, ox, oy, width, h, new[] { condW, valW }, headerH, rowH, rows);
            FillCell(sketch, ox, oy - headerH, condW, headerH, th * 0.85, "锚固长度修正系数 ζa", center: true);
            FillCell(sketch, ox + condW, oy - headerH, valW, headerH, th * 0.85, "ζa", center: true);

            double y = oy - headerH;
            foreach (var z in G16Page53Tables.ZetaRows)
            {
                FillCell(sketch, ox, y - rowH, condW, rowH, th * 0.75, z.Condition);
                FillCell(sketch, ox + condW, y - rowH, valW, rowH, th * 0.85, z.Value, center: true);
                y -= rowH;
            }
            return h;
        }

        private static void DrawFootnotes(DetailSketch sketch, double ox, double oy, double th)
        {
            double lineH = th * 1.25;
            double y = oy;
            foreach (var line in G16Page53Tables.FootnoteLines)
            {
                sketch.AddText(ox, y, line, th * 0.7);
                y -= lineH;
            }
        }

        private static void DrawGrid(DetailSketch sketch, double ox, double oy, double w, double h,
            double[] colWidths, double headerH, double rowH, int dataRows)
        {
            sketch.AddRect(ox, oy - h, w, h, SketchLayerKind.Concrete);
            double x = ox;
            foreach (var cw in colWidths)
            {
                x += cw;
                if (x < ox + w - 0.5)
                    sketch.AddLine(x, oy, x, oy - h, SketchLayerKind.Concrete, SketchLineKind.Solid);
            }

            double yLine = oy - headerH;
            sketch.AddLine(ox, yLine, ox + w, yLine, SketchLayerKind.Concrete, SketchLineKind.Solid);
            for (int r = 0; r < dataRows; r++)
            {
                yLine -= rowH;
                sketch.AddLine(ox, yLine, ox + w, yLine, SketchLayerKind.Concrete, SketchLineKind.Solid);
            }
        }

        private static void FillCell(DetailSketch sketch, double x, double y, double w, double h,
            double textH, string text, bool center = false, bool valignTop = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            double tx = center ? x + w * 0.5 - text.Length * textH * 0.28 : x + textH * 0.15;
            double ty = valignTop ? y + h - textH * 1.1 : y + h * 0.5 - textH * 0.35;
            sketch.AddText(tx, ty, text, textH);
        }

        private static double GetD(Dictionary<string, object> p, string key, double def)
            => p.TryGetValue(key, out var v) && v != null ? Convert.ToDouble(v) : def;

        private static bool GetB(Dictionary<string, object> p, string key, bool def)
            => p.TryGetValue(key, out var v) && v != null ? Convert.ToBoolean(v) : def;
    }
}
