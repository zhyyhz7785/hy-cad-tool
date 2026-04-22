using HyCAD.BlenderUI.Controls;
using HyCAD.BlenderUI.Theming;

namespace HyCAD.BlenderUI.Samples
{
    /// <summary>
    /// ThreeLevelPropertyGroupSample：演示单列属性面板中的三级目录/分组结构。
    /// </summary>
    public partial class ThreeLevelPropertyGroupSample : BlenderWindow
    {
        public ThreeLevelPropertyGroupSample()
        {
            InitializeComponent();
            BlenderThemeManager.Apply(BlenderThemeManager.BlenderThemeName.BlenderDark);
        }
    }
}
