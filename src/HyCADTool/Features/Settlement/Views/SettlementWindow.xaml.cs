using System.Windows;
using System.Windows.Controls;
using HyCADTool.Features.Export.Services;
using HyCADTool.Features.Settlement.ViewModels;
using Microsoft.Win32;

namespace HyCADTool.Features.Settlement.Views
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
                Background = FindThemeBrush("Brush_WindowBack"),
                Foreground = FindThemeBrush("Brush_TextPrimary"),
            };

            var dp = new DockPanel { Margin = new Thickness(8) };

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            DockPanel.SetDock(btnPanel, Dock.Top);

            var btnCopy = new Button
            {
                Content = "复制到剪贴板",
                Padding = new Thickness(12, 4, 12, 4),
                Margin = new Thickness(0, 0, 4, 0),
            };
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
                FontSize = FindThemeDouble("Metric_FontHeader", 12),
                Background = FindThemeBrush("Brush_InputBack"),
                Foreground = FindThemeBrush("Brush_TextPrimary"),
                BorderBrush = FindThemeBrush("Brush_InputBorder"),
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

        private System.Windows.Media.Brush FindThemeBrush(string key)
        {
            return TryFindResource(key) as System.Windows.Media.Brush
                ?? System.Windows.Media.Brushes.Transparent;
        }

        private double FindThemeDouble(string key, double fallback)
        {
            return TryFindResource(key) is double value ? value : fallback;
        }
    }
}
