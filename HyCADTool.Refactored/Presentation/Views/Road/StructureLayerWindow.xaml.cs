using System;
using System.Windows;
using System.Windows.Controls;
using HyCAD.BlenderUI.Controls;
using HyCADTool.Refactored.Presentation.ViewModels.Road;

namespace HyCADTool.Refactored.Presentation.Views.Road
{
    /// <summary>
    /// 结构层定义窗口（hyRoadStructureLayer，M7.3）。
    ///
    /// <para>TreeView 的 <c>SelectedItem</c> 不可双向绑定（WPF 限制），这里用 routed event 转发到 VM。</para>
    /// </summary>
    public partial class StructureLayerWindow : BlenderWindow
    {
        public StructureLayerWindow()
        {
            InitializeComponent();
            CloseClicked += OnCloseClicked;
        }

        public StructureLayerWindow(StructureLayerViewModel viewModel) : this()
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
            if (DataContext is StructureLayerViewModel vm && vm.CancelCommand.CanExecute(null))
            {
                vm.CancelCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void SchemeTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is StructureLayerViewModel vm)
            {
                vm.SelectedNode = e.NewValue;
            }
        }
    }
}
