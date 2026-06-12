using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using HyCADTool.Features.G101.Domain.Catalog;
using HyCADTool.Features.G101.ViewModels;

namespace HyCADTool.Features.G101.Views
{
    public partial class G101Panel : UserControl
    {
        public G101Panel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext == null)
                DataContext = G101PanelViewModel.Current;
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is G101PanelViewModel vm && e.NewValue is CatalogTreeItemVm node && !node.IsGroup)
                vm.SelectedNode = node;
        }
    }
}
