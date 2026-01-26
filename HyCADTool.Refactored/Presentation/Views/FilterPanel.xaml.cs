using System.Windows.Controls;
using HyCADTool.Refactored.Presentation.ViewModels;

namespace HyCADTool.Refactored.Presentation.Views
{
    /// <summary>
    /// FilterPanel 的代码隐藏
    /// 注意：此面板采用 MVVM 模式，所有业务逻辑在 FilterPanelViewModel 中
    /// </summary>
    public partial class FilterPanel : UserControl
    {
        /// <summary>
        /// 无参构造函数（手动创建 ViewModel）
        /// 注意：推荐使用带参构造函数通过 DI 注入 ViewModel
        /// </summary>
        public FilterPanel()
        {
            InitializeComponent();
            // 手动创建 ViewModel（后备方案）
            DataContext = new FilterPanelViewModel();
        }

        /// <summary>
        /// 支持 DI 注入 ViewModel 的构造函数（推荐方式）
        /// </summary>
        public FilterPanel(FilterPanelViewModel viewModel)
        {
            InitializeComponent();
            // 通过 DI 注入的 ViewModel
            DataContext = viewModel;
        }
    }
}

