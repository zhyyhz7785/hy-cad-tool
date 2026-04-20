using System;
using System.Windows;
using HyCAD.BlenderUI.Controls;
using HyCADTool.Refactored.Presentation.ViewModels.Road;

namespace HyCADTool.Refactored.Presentation.Views.Road
{
    /// <summary>
    /// 历史还原窗口（hyRoadHistory，M6.3）。
    ///
    /// <para>职责：承载 <see cref="RoadHistoryViewModel"/>，显示图 1 样式的还原点列表；
    /// 响应 <see cref="RoadHistoryViewModel.CloseRequested"/> 关闭窗口。</para>
    ///
    /// <para>真实还原操作由命令层（<c>RoadHistoryCommand</c>）订阅
    /// <see cref="RoadHistoryViewModel.RestoreRequested"/> 事件处理，本 XAML 不引用 AutoCAD API。</para>
    /// </summary>
    public partial class RoadHistoryWindow : BlenderWindow
    {
        public RoadHistoryWindow()
        {
            InitializeComponent();
            CloseClicked += OnCloseClicked;
        }

        public RoadHistoryWindow(RoadHistoryViewModel viewModel) : this()
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

        private void OnCloseRequested(object sender, bool? dialogResult) => RequestClose(dialogResult);

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is RoadHistoryViewModel vm && vm.CancelCommand.CanExecute(null))
            {
                vm.CancelCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
