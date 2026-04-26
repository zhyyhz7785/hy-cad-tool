using System.Windows;
using System.Windows.Controls;

namespace HyCAD.BlenderUI.Controls.Widgets
{
    /// <summary>UI_BTYPE_DECORATOR：属性行右侧小装饰。</summary>
    public class BlenderDecorator : Button
    {
        static BlenderDecorator()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderDecorator),
                new FrameworkPropertyMetadata(typeof(BlenderDecorator)));
        }
    }
}
