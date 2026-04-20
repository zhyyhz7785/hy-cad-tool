using HyCAD.BlenderUI.Controls;
using HyCAD.BlenderUI.Theming;

namespace HyCAD.BlenderUI.Samples
{
    /// <summary>
    /// WorkbenchSample：顶部 WorkspaceTabBar 切换三种 layout（Layout / Modeling / Sculpting）。
    /// </summary>
    public partial class WorkbenchSample : BlenderWindow
    {
        public WorkbenchSample()
        {
            InitializeComponent();
            BlenderThemeManager.Apply(BlenderThemeManager.BlenderThemeName.BlenderDark);
        }
    }
}
