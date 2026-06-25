using System.Windows;
using HyCADTool.Features.Tables.ViewModels;

namespace HyCADTool.Features.Tables.Views
{
    public partial class TableEditorWindow : Window
    {
        public TableEditorWindow(TableEditorViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? new TableEditorViewModel();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
