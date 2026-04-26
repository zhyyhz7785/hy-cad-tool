using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HyCAD.BlenderUI.Controls.Widgets
{
    /// <summary>UI_BTYPE_HSVCIRCLE：色相环 + 内圈 S/V。</summary>
    public class BlenderHsvCircle : Control
    {
        private WriteableBitmap _bitmap;
        private int _lastSize;

        static BlenderHsvCircle()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderHsvCircle),
                new FrameworkPropertyMetadata(typeof(BlenderHsvCircle)));
        }

        public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(
            nameof(SelectedColor), typeof(Color), typeof(BlenderHsvCircle),
            new FrameworkPropertyMetadata(Colors.Red, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (_, __) => { }));

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double size = Math.Min(ActualWidth, ActualHeight);
            if (size <= 2) return;
            int n = Math.Max(32, (int)size);
            if (_bitmap == null || _lastSize != n)
            {
                _bitmap = new WriteableBitmap(n, n, 96, 96, PixelFormats.Bgra32, null);
                _lastSize = n;
            }

            int stride = n * 4;
            var pixels = new byte[stride * n];
            double cx = n / 2.0;
            double cy = n / 2.0;
            double rMax = Math.Min(cx, cy) - 2;

            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                double dx = x - cx;
                double dy = y - cy;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                double ang = (Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360;
                double s = Math.Min(1, dist / rMax);
                var col = dist > rMax ? Colors.Transparent : ColorSpaces.HsvToRgb(ang, s, 1);
                int i = y * stride + x * 4;
                if (dist <= rMax)
                {
                    pixels[i + 0] = col.B;
                    pixels[i + 1] = col.G;
                    pixels[i + 2] = col.R;
                    pixels[i + 3] = 255;
                }
            }

            _bitmap.WritePixels(new Int32Rect(0, 0, n, n), pixels, stride, 0);
            dc.DrawImage(_bitmap, new Rect(0, 0, size, size));
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Colors.Black), 1), new Point(size / 2, size / 2), rMax, rMax);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Pick(e.GetPosition(this));
        }

        private void Pick(Point pos)
        {
            double size = Math.Min(ActualWidth, ActualHeight);
            double cx = size / 2;
            double cy = size / 2;
            double dx = pos.X - cx;
            double dy = pos.Y - cy;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            double rMax = Math.Min(cx, cy) - 2;
            if (rMax <= 0) return;
            double s = Math.Max(0, Math.Min(1, dist / rMax));
            double ang = (Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360;
            SelectedColor = ColorSpaces.HsvToRgb(ang, s, 1);
            InvalidateVisual();
        }
    }
}
