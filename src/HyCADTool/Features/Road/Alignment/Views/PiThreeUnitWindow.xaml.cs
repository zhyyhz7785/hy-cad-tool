using System;
using System.Windows;
using HyCAD.BlenderUI.Controls;
using HyCADTool.Features.Road.PlanAlignment.ViewModels;

namespace HyCADTool.Features.Road.PlanAlignment.Views
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
    /// - 让 ESC/回车等默认键走 Confirm/Cancel 命令；
    /// - 标题栏 X 按钮点击（<see cref="BlenderWindow.CloseClicked"/>）走 CancelCommand。
    ///
    /// 所有 AutoCAD Transient 预览、DWG/JSON 落盘都由命令层订阅 VM 事件处理，
    /// 本窗口不直接引用 AutoCAD API，以便 VM 的纯单元测试不会被 WPF Host 污染。
    /// </summary>
    public partial class PiThreeUnitWindow : BlenderWindow
    {
        public PiThreeUnitWindow()
        {
            InitializeComponent();
            CloseClicked += OnCloseClicked;
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
            Closed += (_, __) =>
            {
                viewModel.CloseRequested -= OnCloseRequested;
                CloseClicked -= OnCloseClicked;
            };
        }

        private void OnCloseRequested(object sender, bool? dialogResult)
        {
            // 走 BlenderWindow 提供的安全关闭：兼容 ShowModalWindow 与 ShowDialog 两条路径
            RequestClose(dialogResult);
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            // 标题栏 X 按钮：优先走 CancelCommand 让 VM 决定 DialogResult；
            // VM 不可用或拒绝时 BlenderWindow 兜底直接 Close()。
            if (DataContext is PiThreeUnitViewModel vm && vm.CancelCommand.CanExecute(null))
            {
                vm.CancelCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
