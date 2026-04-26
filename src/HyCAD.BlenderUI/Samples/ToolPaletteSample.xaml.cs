using System.Windows;
using HyCAD.BlenderUI.Theming;

namespace HyCAD.BlenderUI.Samples
{
    /// <summary>
    /// ToolPaletteSample（原 DemoWindow）：演示 Blender 风格的「单列工具面板」形态。
    /// 适用于 AutoCAD PaletteSet 宿主 / Blender 右侧 Properties Editor 风格的窄长工具栏。
    /// </summary>
    public partial class ToolPaletteSample : Window
    {
        public ToolPaletteSample()
        {
            InitializeComponent();
            BlenderThemeManager.Apply(BlenderThemeManager.BlenderThemeName.BlenderDark);
        }
    }
}
