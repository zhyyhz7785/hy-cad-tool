#nullable enable

using System;
using System.Globalization;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Shared.Drawing.ValueObjects;

namespace HyCADTool.Features.TitleBlock.Services
{
    /// <summary>
    /// 图题：主字 + 双下划线 + 比例 + 左十字/方格（与 <see cref="DrawingSheetTitleSpec"/> 一致）。
    /// </summary>
    public static class DrawingSheetTitleDrawer
    {
        /// <returns>写入实体数。</returns>
        public static int Draw(
            Database db,
            Action<Entity> appendAndTag,
            DrawingSheetTitleSpec spec,
            Point3d titleCenterWcs,
            string mainTitle,
            int scaleDenominator,
            double mainTitleHeight,
            ObjectId mainTextStyleId,
            ObjectId scaleTextStyleId,
            double scaleTextHeight,
            string? titleTextLayerNameOverride,
            bool useWhiteColor = false)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (appendAndTag == null) throw new ArgumentNullException(nameof(appendAndTag));
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (string.IsNullOrWhiteSpace(mainTitle)) return 0;
            if (mainTitleHeight <= 1e-9) return 0;

            double h = mainTitleHeight;
            string textLayer = !string.IsNullOrWhiteSpace(titleTextLayerNameOverride)
                ? titleTextLayerNameOverride!.Trim()
                : spec.TitleTextLayerName;
            string decoLayer = spec.TitleDecorationLayerName;

            var main = new DBText
            {
                Position = titleCenterWcs,
                TextString = mainTitle,
                Height = h,
                Layer = textLayer,
                ColorIndex = 256,
                HorizontalMode = TextHorizontalMode.TextCenter,
                VerticalMode = TextVerticalMode.TextVerticalMid,
                AlignmentPoint = titleCenterWcs,
            };
            if (!mainTextStyleId.IsNull)
            {
                main.TextStyleId = mainTextStyleId;
            }
            if (useWhiteColor)
                SetAciWhite(main);
            main.AdjustAlignment(db);
            appendAndTag(main);

            Extents3d ext;
            try
            {
                ext = main.GeometricExtents;
            }
            catch
            {
                return 1;
            }

            double minX = ext.MinPoint.X;
            double maxX = ext.MaxPoint.X;
            double minY = ext.MinPoint.Y;
            double maxY = ext.MaxPoint.Y;
            double midY = 0.5 * (minY + maxY);

            double t1 = spec.UpperLineWidthFactor * h;
            double t2 = spec.LowerLineWidthFactor * h;
            double d12 = spec.DoubleLineSpacingFactor * h;
            double textToLine = spec.TextToUpperLineGapFactor * h;

            double y1c = minY - textToLine - t1 * 0.5;
            double y2c = y1c - 0.5 * t1 - d12 - 0.5 * t2;
            int n = 1;

            n += AddHLine(appendAndTag, decoLayer, minX, maxX, y1c, t1, useWhiteColor);
            n += AddHLine(appendAndTag, decoLayer, minX, maxX, y2c, t2, useWhiteColor);

            if (spec.ShowScale && scaleDenominator > 0)
            {
                string scaleString = string.Format(CultureInfo.InvariantCulture, spec.ScaleFormat, scaleDenominator);
                double sh = scaleTextHeight > 1e-9
                    ? scaleTextHeight
                    : Math.Max(1e-6, spec.ScaleTextHeightFactor * h);
                double gap = spec.ScaleGapFromTextRightFactor * h;
                double yAlign = 0.5 * (y1c + y2c);
                var scalePt = new Point3d(maxX + gap, yAlign, 0);
                var scale = new DBText
                {
                    Position = scalePt,
                    TextString = scaleString,
                    Height = sh,
                    Layer = textLayer,
                    ColorIndex = 256,
                    HorizontalMode = TextHorizontalMode.TextLeft,
                    VerticalMode = TextVerticalMode.TextVerticalMid,
                    AlignmentPoint = scalePt,
                };
                if (!scaleTextStyleId.IsNull) scale.TextStyleId = scaleTextStyleId;
                if (useWhiteColor)
                    SetAciWhite(scale);
                scale.AdjustAlignment(db);
                appendAndTag(scale);
                n++;
            }

            if (spec.ShowCrosshair)
            {
                n += AddCrosshair(appendAndTag, decoLayer, spec, h, minX, midY, useWhiteColor);
            }

            return n;
        }

        private static int AddHLine(
            Action<Entity> append,
            string layer,
            double x0,
            double x1,
            double y,
            double width,
            bool useWhiteColor)
        {
            var pl = new Polyline(2);
            pl.AddVertexAt(0, new Point2d(x0, y), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(x1, y), 0, 0, 0);
            pl.Closed = false;
            pl.Layer = layer;
            pl.ColorIndex = 256;
            pl.ConstantWidth = Math.Max(1e-6, width);
            pl.Elevation = 0;
            if (useWhiteColor) SetAciWhite(pl);
            append(pl);
            return 1;
        }

        private static void SetAciWhite(Entity e)
        {
            if (e == null) return;
            e.Color = Color.FromColorIndex(ColorMethod.ByAci, 7);
        }

        private static int AddCrosshair(
            Action<Entity> append,
            string layer,
            DrawingSheetTitleSpec spec,
            double h,
            double minX,
            double textMidY,
            bool useWhiteColor)
        {
            int n = 0;
            double R = spec.CrosshairArmLengthFactor * h;
            double core = spec.CrosshairCoreHalfFactor * h;
            double cy = textMidY + spec.CrosshairCenterLiftFactor * h;
            if (R < 1e-8) R = 0.2 * h;

            var v0 = new Point3d(minX, cy - R, 0);
            var v1 = new Point3d(minX, cy + R, 0);
            n += AddLine(append, layer, v0, v1, useWhiteColor);
            n += AddLine(append, layer, new Point3d(minX - 2.0 * R, cy, 0), new Point3d(minX, cy, 0), useWhiteColor);

            double s = 2.0 * core;
            if (s < 1e-8) s = 0.12 * h;
            double left = minX - spec.CrosshairOffsetFromTextLeftFactor * h - 0.5 * s;
            double btm = cy - 0.5 * s;
            var sq = new Polyline(4);
            sq.AddVertexAt(0, new Point2d(left, btm), 0, 0, 0);
            sq.AddVertexAt(1, new Point2d(left + s, btm), 0, 0, 0);
            sq.AddVertexAt(2, new Point2d(left + s, btm + s), 0, 0, 0);
            sq.AddVertexAt(3, new Point2d(left, btm + s), 0, 0, 0);
            sq.Closed = true;
            sq.Layer = layer;
            sq.ColorIndex = 256;
            sq.ConstantWidth = 0.0;
            if (useWhiteColor)
                SetAciWhite(sq);
            append(sq);
            n++;
            return n;
        }

        private static int AddLine(Action<Entity> append, string layer, Point3d a, Point3d b, bool useWhiteColor)
        {
            var ln = new Line(a, b) { Layer = layer, ColorIndex = 256 };
            if (useWhiteColor)
                SetAciWhite(ln);
            append(ln);
            return 1;
        }
    }
}
