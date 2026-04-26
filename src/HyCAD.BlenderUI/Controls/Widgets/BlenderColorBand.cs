using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HyCAD.BlenderUI.Controls.Widgets
{
    /// <summary>UI_BTYPE_COLORBAND：渐变色标条（简化：固定 4 锚点，可绑 ItemsSource）。</summary>
    public class BlenderColorBand : Control
    {
        static BlenderColorBand()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderColorBand),
                new FrameworkPropertyMetadata(typeof(BlenderColorBand)));
        }

        public static readonly DependencyProperty StopsProperty = DependencyProperty.Register(
            nameof(Stops), typeof(IEnumerable<GradientStop>), typeof(BlenderColorBand),
            new PropertyMetadata(null, OnStopsChanged));

        public IEnumerable<GradientStop> Stops
        {
            get => (IEnumerable<GradientStop>)GetValue(StopsProperty);
            set => SetValue(StopsProperty, value);
        }

        private static void OnStopsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BlenderColorBand b) b.InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var w = ActualWidth;
            var h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            var stops = Stops?.ToList();
            if (stops == null || stops.Count == 0)
            {
                stops = new List<GradientStop>
                {
                    new GradientStop(Colors.Black, 0),
                    new GradientStop(Colors.White, 1),
                };
            }

            var brush = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
            foreach (var s in stops.OrderBy(x => x.Offset))
                brush.GradientStops.Add(s);

            dc.DrawRectangle(brush, new Pen(new SolidColorBrush(Colors.Black), 1), new Rect(0, 0, w, h));
        }
    }
}
