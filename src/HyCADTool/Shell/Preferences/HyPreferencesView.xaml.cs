using System.Windows.Controls;

namespace HyCADTool.Presentation.Views
{
    /// <summary>
    /// HyB 面板「设置」Tab 的主视图（Blender Preferences 风格）。
    /// 一级目录 + 二级 Expander 分组 + 底部按钮栏。
    /// 具体子分组控件骨架在 Views/Preferences 子目录下，通过 ContentTemplate 选择。
    /// </summary>
    public partial class HyPreferencesView : UserControl
    {
        public HyPreferencesView()
        {
            InitializeComponent();
        }
    }
}
