using System;
using System.Windows;
using HyCADTool.Refactored.Presentation.ViewModels.Road;

namespace HyCADTool.Refactored.Presentation.Views.Road
{
    /// <summary>
    /// 三单元（缓-圆-缓）交互式平曲线设计窗口。
    ///
    /// 由 <c>RoadAlignmentEditPiCommand</c> 在用户选了「窗口(W)」分支时通过
    /// <c>Autodesk.AutoCAD.ApplicationServices.Application.ShowModalWindow</c>
    /// 打开，承载 <see cref="PiThreeUnitViewModel"/>。
    ///
    /// 职责仅限于：
    /// - 把 VM 作为 DataContext；
    /// - 响应 <see cref="PiThreeUnitViewModel.CloseRequested"/> 关闭窗口并把 DialogResult 回传；
    /// - 让 ESC/回车等默认键走 Confirm/Cancel 命令。
    ///
    /// 所有 AutoCAD Transient 预览、DWG/JSON 落盘都由命令层订阅 VM 事件处理，
    /// 本窗口不直接引用 AutoCAD API，以便 VM 的纯单元测试不会被 WPF Host 污染。
    /// </summary>
    public partial class PiThreeUnitWindow : Window
    {
        public PiThreeUnitWindow()
        {
            InitializeComponent();
        }

        public PiThreeUnitWindow(PiThreeUnitViewModel viewModel) : this()
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            DataContext = viewModel;
            viewModel.CloseRequested += OnCloseRequested;
            viewModel.NonCompliantConfirm = summary => MessageBox.Show(
                this,
                summary,
                "存在未通过的规范项",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning,
                MessageBoxResult.Cancel) == MessageBoxResult.OK;
            Closed += (_, __) => viewModel.CloseRequested -= OnCloseRequested;
        }

        private void OnCloseRequested(object sender, bool? dialogResult)
        {
            try { DialogResult = dialogResult; }
            catch (InvalidOperationException)
            {
                // 当窗口不是 ShowDialog 弹出的（比如 ShowModalWindow）时 DialogResult 会抛；忽略即可。
            }
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is PiThreeUnitViewModel vm && vm.CancelCommand.CanExecute(null))
            {
                vm.CancelCommand.Execute(null);
                return;
            }
            Close();
        }
    }
}
