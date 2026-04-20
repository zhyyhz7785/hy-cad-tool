using HyCAD.BlenderUI.Controls;
using HyCAD.BlenderUI.Theming;

namespace HyCAD.BlenderUI.Samples
{
    /// <summary>
    /// SplitDesignerSample：BlenderWindow + 3 区横向分屏（左参数 / 中预览 / 右校验）+ 2 个 BlenderSplitter。
    /// 对应生产形态：CrossSectionDesignerWindow（升级版，加入 Splitter）。
    /// </summary>
    public partial class SplitDesignerSample : BlenderWindow
    {
        public SplitDesignerSample()
        {
            InitializeComponent();
            BlenderThemeManager.Apply(BlenderThemeManager.BlenderThemeName.BlenderDark);
        }
    }
}
