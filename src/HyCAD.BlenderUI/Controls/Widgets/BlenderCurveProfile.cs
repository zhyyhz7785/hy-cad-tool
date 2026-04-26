using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HyCAD.BlenderUI.Controls.Widgets
{
    /// <summary>UI_BTYPE_CURVEPROFILE：轮廓曲线（简化为半圆可调）。</summary>
    public class BlenderCurveProfile : Control
    {
        private Point _handle = new Point(0.5, 0.8);

        static BlenderCurveProfile()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderCurveProfile),
                new FrameworkPropertyMetadata(typeof(BlenderCurveProfile)));
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28)), null, new Rect(0, 0, w, h));

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(0, h), false, false);
                ctx.QuadraticBezierTo(new Point(_handle.X * w, _handle.Y * h), new Point(w, 0), true, false);
            }
            geo.Freeze();
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Colors.LightSteelBlue), 2), geo);
            dc.DrawEllipse(new SolidColorBrush(Colors.White), null, new Point(_handle.X * w, _handle.Y * h), 5, 5);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Drag(e.GetPosition(this));
            CaptureMouse();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.LeftButton == MouseButtonState.Pressed)
                Drag(e.GetPosition(this));
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            ReleaseMouseCapture();
        }

        private void Drag(Point pos)
        {
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;
            _handle = new Point(Math.Max(0.05, Math.Min(0.95, pos.X / w)), Math.Max(0.05, Math.Min(0.95, pos.Y / h)));
            InvalidateVisual();
        }
    }
}
