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
            if (DataContext == null)
                DataContext = G16PanelViewModel.Current;
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is G16PanelViewModel vm && e.NewValue is G16CatalogTreeItemVm node && !node.IsGroup)
                vm.SelectedNode = node;
        }
    }
}
