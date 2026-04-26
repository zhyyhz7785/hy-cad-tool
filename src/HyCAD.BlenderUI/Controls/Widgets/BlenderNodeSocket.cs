using System.Windows;
using System.Windows.Controls.Primitives;

namespace HyCAD.BlenderUI.Controls.Widgets
{
    /// <summary>UI_BTYPE_NODE_SOCKET。</summary>
    public class BlenderNodeSocket : ToggleButton
    {
        static BlenderNodeSocket()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderNodeSocket),
                new FrameworkPropertyMetadata(typeof(BlenderNodeSocket)));
        }
    }
}
