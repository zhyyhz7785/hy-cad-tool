using System.Windows;
using System.Windows.Controls;
using HyCADTool.Infrastructure.Services;
using Microsoft.Win32;

namespace HyCADTool.Features.Settlement
{
    public partial class SettlementWindow : Window
    {
        public bool ShouldDrawTable { get; private set; }

        public SettlementWindow()
        {
            InitializeComponent();
            DataContext = SettlementPanelViewModel.Current ?? new SettlementPanelViewModel();
        }

        public SettlementWindow(SettlementPanelViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
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

        private void BtnDrawTable_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as SettlementPanelViewModel;
            if (vm?.LastResult == null || !vm.LastResult.Success)
            {
                if (vm != null) vm.StatusMessage = "请先执行计算";
                return;
            }
            ShouldDrawTable = true;
            Close();
        }

        private void BtnReport_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as SettlementPanelViewModel;
            if (vm == null) return;

            vm.GenerateReportCommand?.Execute(null);

            if (string.IsNullOrWhiteSpace(vm.ReportText))
            {
                vm.StatusMessage = "请先执行计算后再生成计算书";
                return;
            }

            var reportWin = new Window
            {
                Title = "沉降计算书",
                Width = 700,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2e3440")),
            };

            var dp = new DockPanel { Margin = new Thickness(8) };

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            DockPanel.SetDock(btnPanel, Dock.Top);

            var btnCopy = new Button { Content = "复制到剪贴板", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 4, 0) };
            btnCopy.Click += (s, _) =>
            {
                Clipboard.SetText(vm.ReportText);
                vm.StatusMessage = "计算书已复制到剪贴板";
            };
            btnPanel.Children.Add(btnCopy);
            dp.Children.Add(btnPanel);

            var tb = new TextBox
            {
                Text = vm.ReportText,
                IsReadOnly = true,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2e3440")),
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#d8dee9")),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4c566a")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                TextWrapping = TextWrapping.NoWrap,
            };
            dp.Children.Add(tb);

            reportWin.Content = dp;
            reportWin.ShowDialog();
        }

        private void BtnExportWord_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as SettlementPanelViewModel;
            if (vm == null) return;

            vm.GenerateReportCommand?.Execute(null);

            if (string.IsNullOrWhiteSpace(vm.ReportText))
            {
                vm.StatusMessage = "请先执行计算后再导出";
                return;
            }

            var dlg = new SaveFileDialog
            {
                Title = "导出沉降计算书",
                Filter = "Word 文档 (*.docx)|*.docx",
                FileName = $"沉降计算书_{System.DateTime.Now:yyyyMMdd_HHmm}.docx",
                DefaultExt = ".docx"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var exporter = new WordExportService();
                    exporter.Export(vm.ReportText, dlg.FileName);
                    vm.StatusMessage = $"已导出：{dlg.FileName}";
                }
                catch (System.Exception ex)
                {
                    vm.StatusMessage = $"导出失败：{ex.Message}";
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
