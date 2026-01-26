using System.Windows.Controls;
using HyCADTool.Refactored.Presentation.ViewModels;

namespace HyCADTool.Refactored.Presentation.Views
{
    /// <summary>
    /// ReinPanel - 钢筋配置面板
    /// 使用 MVVM 模式，通过 ViewModel 管理业务逻辑
    /// </summary>
    public partial class ReinPanel : UserControl
    {
        /// <summary>
        /// 构造函数（支持依赖注入）
        /// </summary>
        /// <param name="viewModel">钢筋面板 ViewModel</param>
        public ReinPanel(ReinPanelViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        /// <summary>
        /// 无参构造函数（用于设计器）
        /// </summary>
        public ReinPanel()
        {
            InitializeComponent();
        }
    }
}

