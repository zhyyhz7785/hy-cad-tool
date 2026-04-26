using System.Windows;
using System.Windows.Controls.Primitives;

namespace HyCAD.BlenderUI.Controls.ScreenView
{
    public class AreaHeaderFlipButton : ToggleButton
    {
        static AreaHeaderFlipButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AreaHeaderFlipButton),
                new FrameworkPropertyMetadata(typeof(AreaHeaderFlipButton)));
        }
    }
}
