using System;
using System.Windows;
using HyCAD.BlenderUI.Controls;
using HyCADTool.Features.Road.PlanAlignment.ViewModels;

namespace HyCADTool.Features.Road.PlanAlignment.Views
{
    /// <summary>
    /// <c>hyRoadAlnStation</c> 的 C 分支弹出的 BlenderWindow 宿主。
    /// 仅做 DataContext 绑定 + CloseRequested → RequestClose 的回传。
    /// </summary>
    public partial class StationLabelConfigWindow : BlenderWindow
    {
        public StationLabelConfigWindow()
        {
            InitializeComponent();
            CloseClicked += OnCloseClicked;
        }

        public StationLabelConfigWindow(StationLabelConfigViewModel viewModel) : this()
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
            if (DataContext is StationLabelConfigViewModel vm && vm.CancelCommand.CanExecute(null))
            {
                vm.CancelCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
