using System.Windows;
using System.Windows.Controls;
using HyCADTool.Refactored.Presentation.ViewModels;

namespace HyCADTool.Refactored.Presentation.Views
{
    /// <summary>
    /// Blender 风格命令面板（独立面板，通过 <c>HyB</c> 命令打开）。
    /// 左侧垂直图标 Tab（按分类切换）+ 顶部搜索框 + 主区命令列表。
    /// Hy/HyB 均打开此面板：Hy → 跳「设置」Tab，HyB → 停在默认 Tab；过滤走独立「过滤」Tab。
    /// </summary>
    public partial class HyBlenderPanel : UserControl
    {
        private readonly HyBlenderPanelViewModel _vm;

        /// <summary>无参构造（XAML 设计器 / Activator 用）。</summary>
        public HyBlenderPanel() : this(new HyBlenderPanelViewModel())
        {
        }

        /// <summary>生产构造：注入 Blender 面板 VM。</summary>
        public HyBlenderPanel(HyBlenderPanelViewModel vm)
        {
            InitializeComponent();
            _vm = vm ?? new HyBlenderPanelViewModel();
            DataContext = _vm;
        }

        private void OnClearSearchClick(object sender, RoutedEventArgs e)
        {
            if (_vm != null) _vm.SearchText = string.Empty;
            SearchBox?.Focus();
        }
    }
}
