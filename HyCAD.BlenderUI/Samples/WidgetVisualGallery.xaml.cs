using HyCAD.BlenderUI.Controls;
using HyCAD.BlenderUI.Theming;

namespace HyCAD.BlenderUI.Samples
{
    /// <summary>
    /// 阶段 1 视觉回归：渐变底板 / 浮雕输入 / 滚动条拇指 / Tab 顶栏 / Outliner 树线与选中条。
    /// </summary>
    public partial class WidgetVisualGallery : BlenderWindow
    {
        public WidgetVisualGallery()
        {
            InitializeComponent();
            BlenderThemeManager.Apply(BlenderThemeManager.BlenderThemeName.BlenderDark);
        }
    }
}
