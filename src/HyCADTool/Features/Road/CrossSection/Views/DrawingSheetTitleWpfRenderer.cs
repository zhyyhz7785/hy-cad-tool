#nullable enable

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using HyCADTool.Shared.Drawing.ValueObjects;

namespace HyCADTool.Features.Road.CrossSection.Views
{
    /// <summary>
    /// 与 <see cref="DrawingSheetTitleDrawer"/> 比例系数一致的 WPF 图题带（顶栏居中）。
    /// </summary>
    internal static class DrawingSheetTitleWpfRenderer
    {
        private const double MainFontPxFallback = 14.0;
        private const double WpfScale = 2.0;

        public static void DrawTopBand(
            Canvas canvas,
            double canvasWidth,
            DrawingSheetTitleSpec spec,
            string? title,
            int? scaleDenominator)
        {
            if (canvas == null) return;
            if (string.IsNullOrWhiteSpace(title)) return;

            spec ??= DrawingSheetTitleSpec.RoadCrossSectionDefault;
            // MainTextHeightModel / ScaleTextHeightModel 为纸面 mm（与 ActualTextHeight 的纸面值一致），此处仅作预览像素缩放
            double h = spec.MainTextHeightModel > 1e-6
                ? spec.MainTextHeightModel * WpfScale
                : MainFontPxFallback;
            var mainBrush = CrossSectionPreviewRenderer.AnnotationBrush;
            var decoBrush = CrossSectionPreviewRenderer.AnnotationBrush;

            var mainTb = new TextBlock
            {
                Text = title,
                Foreground = mainBrush,
                FontSize = h,
                FontWeight = FontWeights.SemiBold,
            };
            mainTb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            mainTb.Arrange(new Rect(mainTb.DesiredSize));
            double textW = mainTb.DesiredSize.Width;
            double textH = mainTb.DesiredSize.Height;

            string? scaleString = (spec.ShowScale && scaleDenominator is int sd && sd > 0)
                ? string.Format(CultureInfo.InvariantCulture, spec.ScaleFormat, sd)
                : null;
            double scaleFont = spec.ScaleTextHeightModel > 1e-6
                ? spec.ScaleTextHeightModel * WpfScale
                : spec.ScaleTextHeightFactor * h;
            var scaleTb = new TextBlock
            {
                Text = scaleString ?? string.Empty,
                Foreground = mainBrush,
                FontSize = scaleFont,
            };
            scaleTb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double scaleW = string.IsNullOrEmpty(scaleString) ? 0 : scaleTb.DesiredSize.Width;
            double scaleH = string.IsNullOrEmpty(scaleString) ? 0 : scaleTb.DesiredSize.Height;

            double t1 = spec.UpperLineWidthFactor * h;
            double t2 = spec.LowerLineWidthFactor * h;
            double d12 = spec.DoubleLineSpacingFactor * h;
            double textToLine = spec.TextToUpperLineGapFactor * h;
            double scaleGap = spec.ScaleGapFromTextRightFactor * h;

            double R = spec.CrosshairArmLengthFactor * h;
            double chOffset = spec.ShowCrosshair ? (spec.CrosshairOffsetFromTextLeftFactor * h + 2.0 * R) : 0;

            double underW = textW;
            double blockW = chOffset + underW + (string.IsNullOrEmpty(scaleString) ? 0 : (scaleGap + scaleW));
            double x0 = Math.Max(8, 0.5 * (canvasWidth - blockW));

            double yText = 8;
            double yLine1 = yText + textH + textToLine + 0.5 * t1;
            double yLine2 = yLine1 + 0.5 * t1 + d12 + 0.5 * t2;
            double yMid = 0.5 * (yLine1 + yLine2);

            double xText = x0 + chOffset;
            Canvas.SetLeft(mainTb, xText);
            Canvas.SetTop(mainTb, yText);
            canvas.Children.Add(mainTb);

            if (t1 > 0.1)
            {
                var line1 = new System.Windows.Shapes.Rectangle
                {
                    Width = underW,
                    Height = t1,
                    Fill = decoBrush,
                };
                Canvas.SetLeft(line1, xText);
                Canvas.SetTop(line1, yLine1 - 0.5 * t1);
                canvas.Children.Add(line1);
            }
            if (t2 > 0.1)
            {
                var line2 = new System.Windows.Shapes.Rectangle
                {
                    Width = underW,
                    Height = t2,
                    Fill = decoBrush,
                };
                Canvas.SetLeft(line2, xText);
                Canvas.SetTop(line2, yLine2 - 0.5 * t2);
                canvas.Children.Add(line2);
            }

            if (!string.IsNullOrEmpty(scaleString))
            {
                Canvas.SetLeft(scaleTb, xText + underW + scaleGap);
                Canvas.SetTop(scaleTb, yMid - 0.5 * scaleH);
                canvas.Children.Add(scaleTb);
            }

            if (spec.ShowCrosshair)
            {
                double minX = xText;
                double midY = 0.5 * (2 * yText + textH) + spec.CrosshairCenterLiftFactor * h;
                double s = 2.0 * spec.CrosshairCoreHalfFactor * h;
                double left = minX - spec.CrosshairOffsetFromTextLeftFactor * h - 0.5 * s;
                if (R > 0.5)
                {
                    canvas.Children.Add(new Line
                    {
                        X1 = minX, Y1 = midY - R, X2 = minX, Y2 = midY + R,
                        Stroke = decoBrush, StrokeThickness = 0.8,
                    });
                    canvas.Children.Add(new Line
                    {
                        X1 = minX - 2.0 * R, Y1 = midY, X2 = minX, Y2 = midY,
                        Stroke = decoBrush, StrokeThickness = 0.8,
                    });
                }
                if (s > 0.5)
                {
                    var r = new System.Windows.Shapes.Rectangle
                    {
                        Width = s,
                        Height = s,
                        Stroke = decoBrush,
                        StrokeThickness = 0.6,
                        Fill = Brushes.Transparent,
                    };
                    Canvas.SetLeft(r, left);
                    Canvas.SetTop(r, midY - 0.5 * s);
                    canvas.Children.Add(r);
                }
            }
        }
    }
}
