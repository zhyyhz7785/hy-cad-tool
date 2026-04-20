using HyCAD.BlenderUI.Controls;
using HyCAD.BlenderUI.Theming;

namespace HyCAD.BlenderUI.Samples
{
    /// <summary>
    /// MultiEditorSample：4 区域工作区（上 Viewport+Properties / 下 Timeline+Outliner），
    /// 含 3 个 BlenderSplitter（横向 1 + 纵向 2）。
    /// </summary>
    public partial class MultiEditorSample : BlenderWindow
    {
        public MultiEditorSample()
        {
            InitializeComponent();
            BlenderThemeManager.Apply(BlenderThemeManager.BlenderThemeName.BlenderDark);
        }
    }
}
