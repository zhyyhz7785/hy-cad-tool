using System.Windows;
using System.Windows.Controls;
using HyCADTool.Features.Road.Plan.ViewModels;

namespace HyCADTool.Features.Road.Plan.Views
{
    /// <summary>
    /// 项目树面板（045 / M3–M4）。见 <see cref="RoadProjectTreeViewModel"/> 与 045 §9。
    ///
    /// 宿主：独立 <c>PaletteSet</c>，由 <c>hyRoadTree</c> 命令拉起（M6 交付）。
    /// M3 骨架 + 搜索 + 状态栏；M4 增加双击打开 / 右键菜单 / 选中联动（由 VM + <see cref="IRoadTreeInteractionHandler"/> 承担）。
    /// </summary>
    public partial class RoadProjectTreePanel : UserControl
    {
        public RoadProjectTreePanel()
        {
            InitializeComponent();
        }

        /// <summary>当前绑定 VM 的便利访问。</summary>
        public RoadProjectTreeViewModel ViewModel
        {
            get => DataContext as RoadProjectTreeViewModel;
            set => DataContext = value;
        }

        /// <summary>
        /// 当选中节点变化时，把 TreeView 选中项同步回 VM（<see cref="RoadProjectTreeViewModel.SelectedNode"/>）。
        /// TreeView.SelectedItem 只读，需在 code-behind 里回灌；VM setter 再联动 Highlight。
        /// </summary>
        private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var vm = ViewModel;
            if (vm == null) return;
            vm.SelectedNode = e.NewValue as RoadTreeNode;
        }

        /// <summary>「刷新」按钮：让 VM 重建一次树（从最新 <see cref="RoadProjectTreeViewModel.Project"/>）。</summary>
        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            ViewModel?.Rebuild();
        }

        /// <summary>
        /// TreeViewItem 双击：调用 VM.OpenCommand（若 CanExecute 为 false 则默默忽略）。
        /// <para>注意：<c>e.Handled = true</c> 阻止事件冒泡到父 TreeViewItem，避免双击一个叶子同时触发到其所有祖先。</para>
        /// </summary>
        private void OnTreeItemDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!(sender is TreeViewItem tvi)) return;
            if (!(tvi.DataContext is RoadTreeNode node)) return;

            var vm = ViewModel;
            if (vm == null) return;

            if (vm.OpenCommand.CanExecute(node))
            {
                vm.OpenCommand.Execute(node);
                e.Handled = true;
            }
        }
    }
}
