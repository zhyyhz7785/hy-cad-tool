using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HyCAD.BlenderUI.Controls.Widgets
{
    /// <summary>UI_BTYPE_HSVCUBE：S/V 平面 + H 条（简化位图采样）。</summary>
    public class BlenderHsvCube : Control
    {
        private WriteableBitmap _bitmap;
        private int _lastW, _lastH;
        private double _hue;

        static BlenderHsvCube()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderHsvCube),
                new FrameworkPropertyMetadata(typeof(BlenderHsvCube)));
        }

        public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(
            nameof(SelectedColor), typeof(Color), typeof(BlenderHsvCube),
            new FrameworkPropertyMetadata(Colors.Red, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorChanged));

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BlenderHsvCube c)
            {
                ColorSpaces.RgbToHsv(c.SelectedColor, out var h, out _, out _);
                c._hue = h;
                c.InvalidateVisual();
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            ColorSpaces.RgbToHsv(SelectedColor, out _hue, out _, out _);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            int iw = Math.Max(2, (int)w);
            int ih = Math.Max(2, (int)h);
            if (_bitmap == null || _lastW != iw || _lastH != ih)
            {
                _bitmap = new WriteableBitmap(iw, ih, 96, 96, PixelFormats.Bgra32, null);
                _lastW = iw;
                _lastH = ih;
            }

            int stride = iw * 4;
            var pixels = new byte[stride * ih];
            for (int y = 0; y < ih; y++)
            {
                double v = 1.0 - (y / (double)Math.Max(1, ih - 1));
                for (int x = 0; x < iw; x++)
                {
                    double s = x / (double)Math.Max(1, iw - 1);
                    var col = ColorSpaces.HsvToRgb(_hue, s, v);
                    int i = y * stride + x * 4;
                    pixels[i + 0] = col.B;
                    pixels[i + 1] = col.G;
                    pixels[i + 2] = col.R;
                    pixels[i + 3] = 255;
                }
            }

            _bitmap.WritePixels(new Int32Rect(0, 0, iw, ih), pixels, stride, 0);
            dc.DrawImage(_bitmap, new Rect(0, 0, w, h));
            dc.DrawRectangle(null, new Pen(new SolidColorBrush(Colors.Black), 1), new Rect(0, 0, w, h));
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Pick(e.GetPosition(this));
            CaptureMouse();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.LeftButton == MouseButtonState.Pressed)
                Pick(e.GetPosition(this));
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            ReleaseMouseCapture();
        }

        private void Pick(Point pos)
        {
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;
            double s = Math.Max(0, Math.Min(1, pos.X / w));
            double v = Math.Max(0, Math.Min(1, 1.0 - pos.Y / h));
            SelectedColor = ColorSpaces.HsvToRgb(_hue, s, v);
        }
    }
}
