using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace HyCAD.BlenderUI.Controls
{
    /// <summary>环形 Pie 菜单占位（Popup + 圆盘 + 8 方位标签）。</summary>
    public class BlenderPieMenu : Popup
    {
        public BlenderPieMenu()
        {
            AllowsTransparency = false;
            StaysOpen = false;
            Placement = PlacementMode.MousePoint;

            var canvas = new Canvas { Width = 160, Height = 160 };
            double cx = 80, cy = 80, r = 52;
            for (int i = 0; i < 8; i++)
            {
                double ang = (i / 8.0) * 2 * Math.PI - Math.PI / 2;
                var tb = new TextBlock
                {
                    Text = (i + 1).ToString(),
                    Foreground = Brushes.WhiteSmoke,
                    FontSize = 11,
                };
                Canvas.SetLeft(tb, cx + Math.Cos(ang) * r - 6);
                Canvas.SetTop(tb, cy + Math.Sin(ang) * r - 8);
                canvas.Children.Add(tb);
            }

            Child = new Border
            {
                Width = 160,
                Height = 160,
                CornerRadius = new CornerRadius(80),
                Background = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10)),
                BorderThickness = new Thickness(1),
                Child = canvas,
            };
        }
    }
}
