using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HyCAD.BlenderUI.Controls.Widgets
{
    /// <summary>UI_BTYPE_CURVE：值曲线（简化内部点列表）。</summary>
    public class BlenderCurveEditor : Control
    {
        private readonly List<Point> _points = new List<Point> { new Point(0, 0), new Point(1, 1) };
        private int _dragIndex = -1;

        static BlenderCurveEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderCurveEditor),
                new FrameworkPropertyMetadata(typeof(BlenderCurveEditor)));
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var w = ActualWidth;
            var h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)), null, new Rect(0, 0, w, h));

            var ordered = _points.OrderBy(p => p.X).ToList();
            if (ordered.Count < 2) return;
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(ordered[0].X * w, (1 - ordered[0].Y) * h), false, false);
                for (int i = 1; i < ordered.Count; i++)
                    ctx.LineTo(new Point(ordered[i].X * w, (1 - ordered[i].Y) * h), true, false);
            }
            geo.Freeze();
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Colors.LightGray), 1.5), geo);

            foreach (var p in ordered)
            {
                dc.DrawEllipse(new SolidColorBrush(Colors.OrangeRed), null,
                    new Point(p.X * w, (1 - p.Y) * h), 4, 4);
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            var pos = e.GetPosition(this);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            for (int i = 0; i < _points.Count; i++)
            {
                var p = _points[i];
                double x = p.X * w;
                double y = (1 - p.Y) * h;
                if (Math.Abs(pos.X - x) < 8 && Math.Abs(pos.Y - y) < 8)
                {
                    _dragIndex = i;
                    CaptureMouse();
                    return;
                }
            }

            var np = new Point(Math.Max(0, Math.Min(1, pos.X / w)), Math.Max(0, Math.Min(1, 1 - pos.Y / h)));
            _points.Add(np);
            SortPoints();
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragIndex < 0) return;
            var pos = e.GetPosition(this);
            double w = ActualWidth;
            double h = ActualHeight;
            _points[_dragIndex] = new Point(Math.Max(0, Math.Min(1, pos.X / w)), Math.Max(0, Math.Min(1, 1 - pos.Y / h)));
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_dragIndex >= 0)
                SortPoints();
            _dragIndex = -1;
            ReleaseMouseCapture();
        }

        private void SortPoints() => _points.Sort((a, b) => a.X.CompareTo(b.X));
    }
}
