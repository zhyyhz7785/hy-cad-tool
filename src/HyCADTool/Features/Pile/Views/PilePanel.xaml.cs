using System.Windows.Controls;
using HyCADTool.Features.Pile.ViewModels;

namespace HyCADTool.Features.Pile.Views
{
    /// <summary>
    /// PilePanel - 桩基布置面板
    /// 使用 MVVM 模式，通过 PilePanelViewModel 管理业务逻辑
    /// </summary>
    public partial class PilePanel : UserControl
    {
        /// <summary>
        /// 构造函数（支持依赖注入）
        /// </summary>
        public PilePanel(PilePanelViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        /// <summary>
        /// 无参构造函数（设计器 + PanelManager fallback）
        /// </summary>
        public PilePanel()
        {
            InitializeComponent();
        }
    }
}
