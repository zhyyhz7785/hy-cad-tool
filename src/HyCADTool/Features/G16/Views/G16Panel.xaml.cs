using System.Windows;
using System.Windows.Controls;
using HyCADTool.Features.G16.ViewModels;

namespace HyCADTool.Features.G16.Views
{
    public partial class G16Panel : UserControl
    {
        public G16Panel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var vm = G16PanelViewModel.Current;
            if (vm == null) return;
            vm.RefreshCatalogTree();
            DataContext = vm;
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is G16PanelViewModel vm && e.NewValue is G16CatalogTreeItemVm node && !node.IsGroup)
                vm.SelectedNode = node;
        }
    }
}
