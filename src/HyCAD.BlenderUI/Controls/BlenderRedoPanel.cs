using System.Windows;
using System.Windows.Controls;

namespace HyCAD.BlenderUI.Controls
{
    /// <summary>F9 最近操作条占位（左下角 Dock）。</summary>
    public class BlenderRedoPanel : ContentControl
    {
        static BlenderRedoPanel()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderRedoPanel),
                new FrameworkPropertyMetadata(typeof(BlenderRedoPanel)));
        }
    }
}
