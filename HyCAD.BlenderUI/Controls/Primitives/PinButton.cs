using System.Windows;
using System.Windows.Controls.Primitives;

namespace HyCAD.BlenderUI.Controls.Primitives
{
    /// <summary>
    /// Blender 风格图钉按钮：ToggleButton 子类，Checked=已钉住。
    /// </summary>
    public class PinButton : ToggleButton
    {
        static PinButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(PinButton),
                new FrameworkPropertyMetadata(typeof(PinButton)));
        }
    }
}
