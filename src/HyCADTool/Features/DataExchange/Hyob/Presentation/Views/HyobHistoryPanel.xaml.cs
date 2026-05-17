using System.Windows.Controls;
using System.Windows.Input;
using HyCADTool.Features.DataExchange.Hyob.Presentation.ViewModels;

namespace HyCADTool.Features.DataExchange.Hyob.Presentation.Views
{
    /// <summary>
    /// hyob 历史面板视图（M10）。承载 <see cref="HyobHistoryPanelViewModel"/>，由 <c>hyobP</c> 命令通过
    /// PaletteSet 拉起。Ctrl+左键点击 commit 行 → pin 为 A/B 端做双 commit 对比。
    /// </summary>
    public partial class HyobHistoryPanel : UserControl
    {
        public HyobHistoryPanel()
        {
            InitializeComponent();
        }

        public HyobHistoryPanelViewModel ViewModel
        {
            get => DataContext as HyobHistoryPanelViewModel;
            set => DataContext = value;
        }

        /// <summary>
        /// PreviewMouseLeftButtonDown：捕获 Ctrl + 点击，pin 选中的 commit 为 A 或 B 端。
        /// 第一次 Ctrl+ 点击 → 设 A；第二次 → 设 B；第三次以后循环替换 A。
        /// 不阻断默认选中事件，所以普通点击仍然只更新 SelectedItem。
        /// </summary>
        private void OnGridPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;

            var item = FindAncestor<DataGridRow>(e.OriginalSource as System.Windows.DependencyObject);
            if (!(item?.Item is HyobHistoryItem hi)) return;
            var vm = ViewModel;
            if (vm == null) return;

            if (vm.CompareLeft == null) vm.CompareLeft = hi;
            else if (vm.CompareRight == null) vm.CompareRight = hi;
            else
            {
                vm.CompareLeft = vm.CompareRight;
                vm.CompareRight = hi;
            }
        }

        private static T FindAncestor<T>(System.Windows.DependencyObject d) where T : System.Windows.DependencyObject
        {
            while (d != null && !(d is T)) d = System.Windows.Media.VisualTreeHelper.GetParent(d);
            return d as T;
        }
    }
}
