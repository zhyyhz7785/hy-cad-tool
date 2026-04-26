using System.Windows;
using System.Windows.Controls;

namespace HyCAD.BlenderUI.Controls.Widgets
{
    /// <summary>UI_BTYPE_PROGRESS。</summary>
    public class BlenderProgress : ProgressBar
    {
        static BlenderProgress()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderProgress),
                new FrameworkPropertyMetadata(typeof(BlenderProgress)));
        }
    }
}
