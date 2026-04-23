using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HyCADTool.Refactored.Presentation.ViewModels;

namespace HyCADTool.Refactored.Presentation.Views.Preferences
{
    public partial class LayerSettingsView : UserControl
    {
        public LayerSettingsView()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is HySettingsViewModel) return;
            for (var n = (DependencyObject)this; n != null; n = VisualTreeHelper.GetParent(n) ?? LogicalTreeHelper.GetParent(n) as DependencyObject)
            {
                if (n is FrameworkElement fe && fe.DataContext is HySettingsViewModel root)
                {
                    DataContext = root;
                    return;
                }
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
        }

        private void LayerGrid_OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            if (DataContext is HySettingsViewModel vm)
                vm.Settings?.SavePublic();
        }
    }
}
