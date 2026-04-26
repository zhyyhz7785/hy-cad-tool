using System;
using System.Windows;
using HyCAD.BlenderUI.Controls;
using HyCADTool.Features.Road.Intersections.ViewModels;

namespace HyCADTool.Features.Road.Intersections.Views
{
    /// <summary>
    /// 人行横道绘制前的参数窗（BlenderWindow）。
    /// </summary>
    public partial class CrosswalkDrawParamsWindow : BlenderWindow
    {
        public CrosswalkDrawParamsWindow()
        {
            InitializeComponent();
            CloseClicked += OnCloseClicked;
        }

        public CrosswalkDrawParamsWindow(CrosswalkDrawParamsViewModel viewModel) : this()
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
            if (DataContext is CrosswalkDrawParamsViewModel vm && vm.CancelCommand.CanExecute(null))
            {
                vm.CancelCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
