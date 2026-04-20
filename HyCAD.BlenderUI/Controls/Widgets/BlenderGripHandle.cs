using System.Windows;
using System.Windows.Controls.Primitives;

namespace HyCAD.BlenderUI.Controls.Widgets
{
    /// <summary>UI_BTYPE_GRIP。</summary>
    public class BlenderGripHandle : Thumb
    {
        static BlenderGripHandle()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderGripHandle),
                new FrameworkPropertyMetadata(typeof(BlenderGripHandle)));
        }
    }
}
