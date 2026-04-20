using System.Windows;
using System.Windows.Controls.Primitives;

namespace HyCAD.BlenderUI.Controls.ScreenView
{
    public class RegionCollapseHandle : ToggleButton
    {
        static RegionCollapseHandle()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(RegionCollapseHandle),
                new FrameworkPropertyMetadata(typeof(RegionCollapseHandle)));
        }
    }
}
