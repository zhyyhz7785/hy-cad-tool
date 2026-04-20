using System.Windows;
using System.Windows.Controls;

namespace HyCAD.BlenderUI.Controls.ScreenView
{
    /// <summary>Area 角手柄（拆分/合并入口占位）。</summary>
    public class AreaCornerHandle : Button
    {
        static AreaCornerHandle()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AreaCornerHandle),
                new FrameworkPropertyMetadata(typeof(AreaCornerHandle)));
        }
    }
}
