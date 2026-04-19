using System;
using System.Windows;
using HyCAD.BlenderUI.Controls;
using HyCADTool.Refactored.Presentation.ViewModels.Road;

namespace HyCADTool.Refactored.Presentation.Views.Road
{
    /// <summary>
    /// Alignment Sub-Entity 全表窗口（hyRoadAlnTable）。
    ///
    /// 职责：
    /// - 承载 <see cref="AlignmentTableViewModel"/>，显示段表 / 几何点表；
    /// - 响应 <see cref="AlignmentTableViewModel.CloseRequested"/> 关闭窗口；
    /// - 标题栏 X 按钮（<see cref="BlenderWindow.CloseClicked"/>）走 CloseCommand，确保窗口保持正常关闭流程。
    ///
    /// 高亮预览由命令层（<c>RoadAlignmentTableCommand</c>）订阅
    /// <see cref="AlignmentTableViewModel.SegmentSelectionChanged"/> 事件处理，本 XAML 不引用 AutoCAD API。
    /// </summary>
    public partial class AlignmentTableWindow : BlenderWindow
    {
        public AlignmentTableWindow()
        {
            InitializeComponent();
            CloseClicked += OnCloseClicked;
        }

        public AlignmentTableWindow(AlignmentTableViewModel viewModel) : this()
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            DataContext = viewModel;
            viewModel.CloseRequested += OnCloseRequested;
            Closed += (_, __) =>
            {
                viewModel.CloseRequested -= OnCloseRequested;
                CloseClicked -= OnCloseClicked;
            };
        }

        private void OnCloseRequested(object sender, bool? dialogResult)
            => RequestClose(dialogResult);

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is AlignmentTableViewModel vm && vm.CloseCommand.CanExecute(null))
            {
                vm.CloseCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
