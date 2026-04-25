using System.Windows.Controls;

namespace HyCADTool.Features.BaseRein
{
    /// <summary>
    /// BaseReinPanel.xaml 的交互逻辑
    /// </summary>
    public partial class BaseReinPanel : UserControl
    {
        /// <summary>
        /// 无参构造函数（用于设计器或 PanelManager 的 Activator.CreateInstance）
        /// </summary>
        public BaseReinPanel()
        {
            InitializeComponent();
            // DataContext 将由 DI 注入或手动设置
        }

        /// <summary>
        /// DI 注入的构造函数
        /// </summary>
        /// <param name="viewModel">注入的 ViewModel</param>
        public BaseReinPanel(BaseReinPanelViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}

