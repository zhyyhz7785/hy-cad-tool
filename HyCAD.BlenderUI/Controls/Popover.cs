using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace HyCAD.BlenderUI.Controls
{
    /// <summary>
    /// Blender 风格 Popover：Popup 包装，Header 栏 + Content 面板；可拖拽触发器控件作为定位锚。
    /// </summary>
    public class Popover : HeaderedContentControl
    {
        static Popover()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(Popover),
                new FrameworkPropertyMetadata(typeof(Popover)));
        }

        public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
            nameof(IsOpen), typeof(bool), typeof(Popover),
            new FrameworkPropertyMetadata(false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public static readonly DependencyProperty PlacementTargetProperty = DependencyProperty.Register(
            nameof(PlacementTarget), typeof(UIElement), typeof(Popover),
            new PropertyMetadata(null));

        public UIElement PlacementTarget
        {
            get => (UIElement)GetValue(PlacementTargetProperty);
            set => SetValue(PlacementTargetProperty, value);
        }

        public static readonly DependencyProperty PlacementProperty = DependencyProperty.Register(
            nameof(Placement), typeof(PlacementMode), typeof(Popover),
            new PropertyMetadata(PlacementMode.Bottom));

        public PlacementMode Placement
        {
            get => (PlacementMode)GetValue(PlacementProperty);
            set => SetValue(PlacementProperty, value);
        }
    }
}
