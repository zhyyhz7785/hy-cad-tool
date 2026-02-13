using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HyCADTool.MarkdownEditor.Views.Controls
{
    /// <summary>
    /// 毫米标尺控件 — 固定 20mm 大刻度（20, 40, 60...），4mm 小刻度。
    /// PixelsPerMm 随缩放变化：缩放越大，同样 20mm 在屏幕上占的像素越多（放大镜效果）。
    /// </summary>
    public class RulerControl : FrameworkElement
    {
        // ── 依赖属性 ──

        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(RulerControl),
                new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PixelsPerMmProperty =
            DependencyProperty.Register(nameof(PixelsPerMm), typeof(double), typeof(RulerControl),
                new FrameworkPropertyMetadata(4.8, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SegmentCountProperty =
            DependencyProperty.Register(nameof(SegmentCount), typeof(int), typeof(RulerControl),
                new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        /// <summary>每毫米对应的像素数（随 Ctrl+滚轮缩放而变）</summary>
        public double PixelsPerMm
        {
            get => (double)GetValue(PixelsPerMmProperty);
            set => SetValue(PixelsPerMmProperty, value);
        }

        /// <summary>
        /// 水平标尺分段数量（对应预览栏数）。每段独立显示 20,40,60...
        /// 垂直标尺忽略该值。
        /// </summary>
        public int SegmentCount
        {
            get => (int)GetValue(SegmentCountProperty);
            set => SetValue(SegmentCountProperty, value);
        }

        // ── 固定刻度 ──
        private const double MAJOR_MM = 20;   // 大刻度间隔 = 20mm
        private const double MINOR_MM = 4;    // 小刻度间隔 = 4mm（每大格 5 小格）

        // ── 颜色（Cursor 深色主题） ──
        private static readonly Pen MajorPen = Freeze(new Pen(Br(0x6e, 0x76, 0x81), 1));
        private static readonly Pen MinorPen = Freeze(new Pen(Br(0x30, 0x36, 0x3d), 1));
        private static readonly SolidColorBrush LblBr = Br(0x8b, 0x94, 0x9e);
        private static readonly SolidColorBrush BgBr  = Br(0x16, 0x1b, 0x22);
        private static readonly Pen BdrPen = Freeze(new Pen(Br(0x30, 0x36, 0x3d), 1));
        private static readonly Typeface TF = new Typeface("Segoe UI");
        private const double FONT = 8;
        private const double THICKNESS = 24;

        static RulerControl() { LblBr.Freeze(); BgBr.Freeze(); }
        private static SolidColorBrush Br(byte r, byte g, byte b)
        {
            var b2 = new SolidColorBrush(Color.FromRgb(r, g, b));
            b2.Freeze();
            return b2;
        }
        private static Pen Freeze(Pen p) { p.Freeze(); return p; }

        protected override void OnRender(DrawingContext dc)
        {
            bool h = Orientation == Orientation.Horizontal;
            double len = h ? ActualWidth : ActualHeight;   // 标尺总长（像素）
            double t = h ? ActualHeight : ActualWidth;     // 标尺厚度（像素，= 20）
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            double ppm = Math.Max(0.01, PixelsPerMm);

            // 背景
            dc.DrawRectangle(BgBr, null, new Rect(RenderSize));

            // 底边线
            if (h) dc.DrawLine(BdrPen, new Point(0, t - 0.5), new Point(len, t - 0.5));
            else   dc.DrawLine(BdrPen, new Point(t - 0.5, 0), new Point(t - 0.5, len));

            if (len <= 0) return;

            double minorPx = MINOR_MM * ppm;   // 小刻度像素间距（随缩放而变）
            double majorPx = MAJOR_MM * ppm;   // 大刻度像素间距
            int segments = Math.Max(1, SegmentCount);

            if (h)
            {
                double segLen = len / segments;
                for (int seg = 0; seg < segments; seg++)
                {
                    double segStart = seg * segLen;
                    double segEnd = seg == segments - 1 ? len : segStart + segLen;
                    if (seg > 0)
                    {
                        double sepX = Math.Round(segStart) + 0.5;
                        dc.DrawLine(BdrPen, new Point(sepX, 0), new Point(sepX, t));
                    }

                    // 分段起点数字（交接处必须有数字）
                    var zeroFt = MkTxt("0", LblBr, dpi);
                    dc.DrawText(zeroFt, new Point(segStart + 2, 1));

                    int minorCount = (int)Math.Floor(segLen / minorPx);
                    for (int i = 1; i <= minorCount; i++)
                    {
                        double localPx = i * minorPx;
                        double px = segStart + localPx;
                        if (px > segEnd + 0.5) break;

                        bool isMajor = (i % 5) == 0; // 4mm * 5 = 20mm
                        double tickH = isMajor ? t * 0.6 : t * 0.25;
                        var pen = isMajor ? MajorPen : MinorPen;
                        double snap = Math.Round(px) + 0.5;
                        dc.DrawLine(pen, new Point(snap, t), new Point(snap, t - tickH));

                        if (isMajor)
                        {
                            int mmVal = i * 4;
                            var ft = MkTxt(mmVal.ToString(), LblBr, dpi);
                            dc.DrawText(ft, new Point(px + 2, 1));
                        }
                    }

                    // 分段终点数字（交接处必须有数字）
                    int endMm = (int)Math.Round(segLen / ppm);
                    if (endMm > 0)
                    {
                        var endFt = MkTxt(endMm.ToString(), LblBr, dpi);
                        // 给下一段起点 "0" 预留 10px，避免重叠
                        double exMax = seg == segments - 1 ? segEnd - endFt.Width - 2 : segEnd - endFt.Width - 10;
                        double ex = Math.Max(segStart + 2, exMax);
                        dc.DrawText(endFt, new Point(ex, 1));
                    }
                }

                return;
            }

            // 垂直标尺：整段连续
            var zeroV = MkTxt("0", LblBr, dpi);
            dc.DrawText(zeroV, new Point(1, 1));

            int vMinorCount = (int)Math.Floor(len / minorPx);
            for (int i = 1; i <= vMinorCount; i++)
            {
                double px = i * minorPx;
                bool isMajor = (i % 5) == 0; // 4mm * 5 = 20mm
                double tickH = isMajor ? t * 0.6 : t * 0.25;
                var pen = isMajor ? MajorPen : MinorPen;
                double snap = Math.Round(px) + 0.5;

                dc.DrawLine(pen, new Point(t, snap), new Point(t - tickH, snap));
                if (!isMajor) continue;

                int mmVal = i * 4;
                var ft = MkTxt(mmVal.ToString(), LblBr, dpi);
                // 不旋转，避免在 20px 厚度下被裁切
                dc.DrawText(ft, new Point(1, px - ft.Height * 0.5));
            }

            int vEndMm = (int)Math.Round(len / ppm);
            if (vEndMm > 0)
            {
                var endFt = MkTxt(vEndMm.ToString(), LblBr, dpi);
                dc.DrawText(endFt, new Point(1, Math.Max(1, len - endFt.Height - 1)));
            }
        }

        private static FormattedText MkTxt(string s, Brush br, double dpi) =>
            new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, TF, FONT, br, dpi);

        protected override Size MeasureOverride(Size a)
        {
            return Orientation == Orientation.Horizontal
                ? new Size(double.IsInfinity(a.Width) ? 0 : a.Width, THICKNESS)
                : new Size(THICKNESS, double.IsInfinity(a.Height) ? 0 : a.Height);
        }
    }
}
