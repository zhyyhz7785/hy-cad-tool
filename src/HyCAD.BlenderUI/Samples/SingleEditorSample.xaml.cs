using HyCAD.BlenderUI.Controls;
using HyCAD.BlenderUI.Theming;

namespace HyCAD.BlenderUI.Samples
{
    /// <summary>
    /// SingleEditorSample：BlenderWindow + 单 PropertyEditor 多 Tab 互斥切换。
    /// 对应生产形态：PiThreeUnitWindow。
    /// </summary>
    public partial class SingleEditorSample : BlenderWindow
    {
        public SingleEditorSample()
        {
            InitializeComponent();
            BlenderThemeManager.Apply(BlenderThemeManager.BlenderThemeName.BlenderDark);
        }
    }
}
