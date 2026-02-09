using System.Windows.Controls;
using HyCADTool.Refactored.Presentation.ViewModels;

namespace HyCADTool.Refactored.Presentation.Views
{
    /// <summary>
    /// SettingsPanel - HY 设置面板
    /// 使用 MVVM 模式，通过 ViewModel 管理业务逻辑
    /// </summary>
    public partial class SettingsPanel : UserControl
    {
        /// <summary>
        /// 构造函数（支持依赖注入）
        /// </summary>
        public SettingsPanel(SettingsPanelViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        /// <summary>
        /// 无参构造函数（设计器 + PanelManager fallback）
        /// </summary>
        public SettingsPanel()
        {
            InitializeComponent();
        }
    }
}
