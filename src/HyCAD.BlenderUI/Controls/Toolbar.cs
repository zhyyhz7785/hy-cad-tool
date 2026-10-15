using System.Windows;
using System.Windows.Controls;

namespace HyCAD.BlenderUI.Controls
{
    /// <summary>
    /// 视口工具栏（对应 Blender 3D View 的 T 键 Tools 区）。
    /// <see cref="Position"/> 默认 <see cref="ToolbarPosition.Top"/>：横向图标条；
    /// <see cref="ToolbarPosition.Side"/>：左侧纵向图标条。
    /// 条带厚度走 <c>Metric_ToolbarWidth</c>；Items 装任意可点击 UIElement（典型为 Button/ToggleButton）。
    /// 需要随位置换槽（上侧行 / 左侧列）时放进 <see cref="ToolbarHost"/>。
    /// </summary>
    public class Toolbar : ItemsControl
    {
        static Toolbar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(Toolbar),
                new FrameworkPropertyMetadata(typeof(Toolbar)));
        }

        public Toolbar()
        {
            Loaded += (_, __) => ApplyOrientation();
        }

        public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
            nameof(Position), typeof(ToolbarPosition), typeof(Toolbar),
            new FrameworkPropertyMetadata(
                ToolbarPosition.Top,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                OnPositionChanged));

        /// <summary>摆放位置。默认上侧；侧面为经典 Blender 左 T。</summary>
        public ToolbarPosition Position
        {
            get => (ToolbarPosition)GetValue(PositionProperty);
            set => SetValue(PositionProperty, value);
        }

        private static void OnPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Toolbar tb)
                tb.ApplyOrientation();
        }

        private void ApplyOrientation()
        {
            var factory = new FrameworkElementFactory(typeof(StackPanel));
            factory.SetValue(StackPanel.OrientationProperty,
                Position == ToolbarPosition.Side ? Orientation.Vertical : Orientation.Horizontal);
            factory.SetValue(Panel.IsItemsHostProperty, true);
            ItemsPanel = new ItemsPanelTemplate { VisualTree = factory };

            if (Parent is DockPanel)
            {
                DockPanel.SetDock(this,
                    Position == ToolbarPosition.Side ? Dock.Left : Dock.Top);
            }
        }
    }
}
