using System;
using System.Windows;
using HyCAD.BlenderUI.Controls;
using HyCADTool.Features.Road.PlanAlignment.ViewModels;

namespace HyCADTool.Features.Road.PlanAlignment.Views
{
    /// <summary>
    /// <c>hyRoadAlnDefaults</c> 对应的 BlenderWindow 宿主。
    /// 仅负责 DataContext 绑定与关闭路径（<see cref="AlignmentDefaultsViewModel.CloseRequested"/>
    /// → <see cref="BlenderWindow.RequestClose"/>），不直接触碰持久化 / AutoCAD API。
    /// </summary>
    public partial class AlignmentDefaultsWindow : BlenderWindow
    {
        public AlignmentDefaultsWindow()
        {
            InitializeComponent();
            CloseClicked += OnCloseClicked;
        }

        public AlignmentDefaultsWindow(AlignmentDefaultsViewModel viewModel) : this()
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
            // X 按钮视为取消
            if (DataContext is AlignmentDefaultsViewModel vm && vm.CancelCommand.CanExecute(null))
            {
                vm.CancelCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
