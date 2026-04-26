using System.Windows;
using System.Windows.Controls;

namespace HyCAD.BlenderUI.Controls
{
    /// <summary>
    /// 左侧竖直图标按钮列（对应 Blender 3D View 左侧 T 键 Toolbar）。
    /// 默认宽度走 <c>Metric_ToolbarWidth</c>；ItemsSource 装任意可点击 UIElement（典型为 Button/ToggleButton）。
    /// </summary>
    public class Toolbar : ItemsControl
    {
        static Toolbar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(Toolbar),
                new FrameworkPropertyMetadata(typeof(Toolbar)));
        }
    }
}
