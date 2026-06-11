using System.Windows.Controls;
using HyCADTool.Shell.ViewModels;

namespace HyCADTool.Shell.Views
{
    /// <summary>
    /// FilterPanel.xaml 的交互逻辑。
    /// 独立的过滤器面板 UserControl，同时作为 HyBlenderPanel 的「过滤」Tab 内嵌视图。
    /// 嵌入时 DataContext 由外部（<see cref="HyBlenderPanelViewModel.FilterVm"/>）注入，
    /// 独立使用时走带参构造注入（Autofac 或手动传入）。
    /// </summary>
    public partial class FilterPanel : UserControl
    {
        /// <summary>
        /// 无参构造：供 XAML 作为子视图嵌入时使用，由外部 DataContext 注入 ViewModel。
        /// 不再自动 new FilterPanelViewModel()（否则会被上层 Binding 覆盖，浪费创建）。
        /// </summary>
        public FilterPanel()
        {
            InitializeComponent();
        }

        /// <summary>带参构造：Autofac 或手动独立使用时注入 VM。</summary>
        public FilterPanel(FilterPanelViewModel vm) : this()
        {
            DataContext = vm;
        }
    }
}
