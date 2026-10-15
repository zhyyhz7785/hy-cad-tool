using HyCAD.BlenderUI.Controls;
using HyCAD.BlenderUI.Theming;

namespace HyCAD.BlenderUI.Samples
{
    /// <summary>
        /// FullDesignerSample：完整 Editor 体系示范（顶 EditorHeader + 视口 ToolbarHost 上侧/侧面 + 右 NPanel + 底 StatusBar）。
    /// </summary>
    public partial class FullDesignerSample : BlenderWindow
    {
        public FullDesignerSample()
        {
            InitializeComponent();
            BlenderThemeManager.Apply(BlenderThemeManager.BlenderThemeName.BlenderDark);
        }
    }
}
