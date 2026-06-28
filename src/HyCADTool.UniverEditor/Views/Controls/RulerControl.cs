using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HyCADTool.UniverEditor.Views.Controls
{
    /// <summary>
    /// 毫米标尺控件 — 固定 20mm 大刻度（20, 40, 60...），4mm 小刻度。
    /// OriginOffsetPx：mm=0 刻度在屏幕上的像素位置（随 Univer 画布 scroll/zoom 联动）。
    /// </summary>
    public class RulerControl : FrameworkElement
    {
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(RulerControl),
                new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PixelsPerMmProperty =
            DependencyProperty.Register(nameof(PixelsPerMm), typeof(double), typeof(RulerControl),
                new FrameworkPropertyMetadata(96.0 / 25.4, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty OriginOffsetPxProperty =
            DependencyProperty.Register(nameof(OriginOffsetPx), typeof(double), typeof(RulerControl),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SegmentCountProperty =
            DependencyProperty.Register(nameof(SegmentCount), typeof(int), typeof(RulerControl),
                new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty RulerBackgroundProperty =
            DependencyProperty.Register(nameof(RulerBackground), typeof(Brush), typeof(RulerControl),
                new FrameworkPropertyMetadata(FallbackBr(0x3b, 0x42, 0x52), FrameworkPropertyMetadataOptions.AffectsRender, OnBrushChanged));

        public static readonly DependencyProperty LabelBrushProperty =
            DependencyProperty.Register(nameof(LabelBrush), typeof(Brush), typeof(RulerControl),
                new FrameworkPropertyMetadata(FallbackBr(0xd8, 0xde, 0xe9), FrameworkPropertyMetadataOptions.AffectsRender, OnBrushChanged));

        public static readonly DependencyProperty MajorTickBrushProperty =
            DependencyProperty.Register(nameof(MajorTickBrush), typeof(Brush), typeof(RulerControl),
                new FrameworkPropertyMetadata(FallbackBr(0x8b, 0x94, 0x9e), FrameworkPropertyMetadataOptions.AffectsRender, OnBrushChanged));

        public static readonly DependencyProperty MinorTickBrushProperty =
            DependencyProperty.Register(nameof(MinorTickBrush), typeof(Brush), typeof(RulerControl),
                new FrameworkPropertyMetadata(FallbackBr(0x4c, 0x56, 0x6a), FrameworkPropertyMetadataOptions.AffectsRender, OnBrushChanged));

        public static readonly DependencyProperty RulerBorderBrushProperty =
            DependencyProperty.Register(nameof(RulerBorderBrush), typeof(Brush), typeof(RulerControl),
                new FrameworkPropertyMetadata(FallbackBr(0x4c, 0x56, 0x6a), FrameworkPropertyMetadataOptions.AffectsRender, OnBrushChanged));

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public double PixelsPerMm
        {
            get => (double)GetValue(PixelsPerMmProperty);
            set => SetValue(PixelsPerMmProperty, value);
        }

        /// <summary>mm=0 在标尺上的像素位置（含 scroll 补偿）。</summary>
        public double OriginOffsetPx
        {
            get => (double)GetValue(OriginOffsetPxProperty);
            set => SetValue(OriginOffsetPxProperty, value);
        }

        public int SegmentCount
        {
            get => (int)GetValue(SegmentCountProperty);
            set => SetValue(SegmentCountProperty, value);
        }

        public Brush RulerBackground
        {
            get => (Brush)GetValue(RulerBackgroundProperty);
            set => SetValue(RulerBackgroundProperty, value);
        }

        public Brush LabelBrush
        {
            get => (Brush)GetValue(LabelBrushProperty);
            set => SetValue(LabelBrushProperty, value);
        }

        public Brush MajorTickBrush
        {
            get => (Brush)GetValue(MajorTickBrushProperty);
            set => SetValue(MajorTickBrushProperty, value);
        }

        public Brush MinorTickBrush
        {
            get => (Brush)GetValue(MinorTickBrushProperty);
            set => SetValue(MinorTickBrushProperty, value);
        }

        public Brush RulerBorderBrush
        {
            get => (Brush)GetValue(RulerBorderBrushProperty);
            set => SetValue(RulerBorderBrushProperty, value);
        }

        private const double MinorMm = 4;
        private static readonly Typeface TF = new Typeface("Segoe UI");
        private const double FontSize = 8;
        private const double Thickness = 24;

        private readonly Dictionary<string, FormattedText> _textCache = new Dictionary<string, FormattedText>();
        private double _cachedDpi = -1;
        private Pen _majorPen;
        private Pen _minorPen;
        private Pen _borderPen;

        private static SolidColorBrush FallbackBr(byte r, byte g, byte b)
        {
            var br = new SolidColorBrush(Color.FromRgb(r, g, b));
            br.Freeze();
            return br;
        }

        private static void OnBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RulerControl ruler)
            {
                ruler._majorPen = null;
                ruler._minorPen = null;
                ruler._borderPen = null;
                ruler._textCache.Clear();
            }
        }

        private Pen GetOrCreatePen(ref Pen cache, Brush brush)
        {
            if (cache != null)
                return cache;
            cache = new Pen(brush, 1);
            if (cache.CanFreeze)
                cache.Freeze();
            return cache;
        }

        protected override void OnRender(DrawingContext dc)
        {
            bool horizontal = Orientation == Orientation.Horizontal;
            double len = horizontal ? ActualWidth : ActualHeight;
            double thick = horizontal ? ActualHeight : ActualWidth;
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            if (Math.Abs(_cachedDpi - dpi) > 0.0001)
            {
                _textCache.Clear();
                _cachedDpi = dpi;
            }

            double ppm = Math.Max(0.01, PixelsPerMm);
            double origin = OriginOffsetPx;
            double minorPx = MinorMm * ppm;

            Brush bgBr = RulerBackground;
            Brush lblBr = LabelBrush;
            Pen majorPen = GetOrCreatePen(ref _majorPen, MajorTickBrush);
            Pen minorPen = GetOrCreatePen(ref _minorPen, MinorTickBrush);
            Pen bdrPen = GetOrCreatePen(ref _borderPen, RulerBorderBrush);

            dc.DrawRectangle(bgBr, null, new Rect(RenderSize));

            if (horizontal)
                dc.DrawLine(bdrPen, new Point(0, thick - 0.5), new Point(len, thick - 0.5));
            else
                dc.DrawLine(bdrPen, new Point(thick - 0.5, 0), new Point(thick - 0.5, len));

            if (len <= 0 || minorPx <= 0)
                return;

            int startMinor = (int)Math.Floor(-origin / minorPx);
            int endMinor = (int)Math.Ceiling((len - origin) / minorPx);

            for (int i = startMinor; i <= endMinor; i++)
            {
                double px = origin + i * minorPx;
                if (px < -0.5 || px > len + 0.5)
                    continue;

                bool isMajor = (i % 5) == 0;
                double tickLen = isMajor ? thick * 0.6 : thick * 0.25;
                var pen = isMajor ? majorPen : minorPen;
                double snap = Math.Round(px) + 0.5;

                if (horizontal)
                {
                    dc.DrawLine(pen, new Point(snap, thick), new Point(snap, thick - tickLen));
                    if (i == 0)
                    {
                        var zeroFt = MkTxt("0", lblBr, dpi);
                        dc.DrawText(zeroFt, new Point(px + 2, 1));
                    }
                    else if (isMajor)
                    {
                        int mmVal = i * 4;
                        var ft = MkTxt(mmVal.ToString(CultureInfo.InvariantCulture), lblBr, dpi);
                        dc.DrawText(ft, new Point(px + 2, 1));
                    }
                }
                else
                {
                    dc.DrawLine(pen, new Point(thick, snap), new Point(thick - tickLen, snap));
                    if (i == 0)
                    {
                        var zeroFt = MkTxt("0", lblBr, dpi);
                        dc.DrawText(zeroFt, new Point(1, px + 1));
                    }
                    else if (isMajor)
                    {
                        int mmVal = i * 4;
                        var ft = MkTxt(mmVal.ToString(CultureInfo.InvariantCulture), lblBr, dpi);
                        dc.DrawText(ft, new Point(1, px - ft.Height * 0.5));
                    }
                }
            }
        }

        private FormattedText MkTxt(string s, Brush br, double dpi)
        {
            string key = $"{dpi:0.####}|{s}";
            if (_textCache.TryGetValue(key, out var hit))
                return hit;

            var ft = new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, TF, FontSize, br, dpi);
            _textCache[key] = ft;
            return ft;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return Orientation == Orientation.Horizontal
                ? new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, Thickness)
                : new Size(Thickness, double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
        }
    }
}
