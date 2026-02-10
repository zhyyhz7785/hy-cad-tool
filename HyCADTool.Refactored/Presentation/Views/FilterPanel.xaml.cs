using System.Windows.Controls;
using HyCADTool.Refactored.Presentation.ViewModels;

namespace HyCADTool.Refactored.Presentation.Views
{
    /// <summary>
    /// FilterPanel.xaml 的交互逻辑
    /// 独立的过滤器面板 UserControl
    /// </summary>
    public partial class FilterPanel : UserControl
    {
        public FilterPanel()
        {
            InitializeComponent();
            // 设置 DataContext 为独立的 FilterPanelViewModel
            DataContext = new FilterPanelViewModel();
        }
    }
}
