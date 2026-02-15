using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Wpf;

namespace HyCADTool.MarkdownEditor.Views.Controls
{
    public partial class PreviewPanelControl : UserControl
    {
        public event EventHandler TogglePageOrientationRequested;
        public event EventHandler PreviousPageRequested;
        public event EventHandler NextPageRequested;
        public event EventHandler<int> JumpPageRequested;

        public PreviewPanelControl()
        {
            InitializeComponent();
        }

        public Grid PreviewGrid => PreviewRulerGrid;
        public RowDefinition RulerRowDefinition => RulerRow;
        public ColumnDefinition RulerColumnDefinition => RulerCol;
        public Border RulerCornerElement => RulerCorner;
        public RulerControl HorizontalRuler => PreviewHRuler;
        public RulerControl VerticalRuler => PreviewVRuler;
        public WebView2 PreviewWebViewControl => PreviewWebView;

        public void UpdatePageState(int currentPage, int totalPages)
        {
            int total = Math.Max(1, totalPages);
            int current = Math.Max(1, Math.Min(total, currentPage));
            PageStateText.Text = $"{current}/{total}";
            PageJumpTextBox.Text = current.ToString();
            PrevPageBtn.IsEnabled = current > 1;
            NextPageBtn.IsEnabled = current < total;
        }

        private void OnTogglePageOrientationClicked(object sender, RoutedEventArgs e)
        {
            TogglePageOrientationRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnPrevPageClicked(object sender, RoutedEventArgs e)
        {
            PreviousPageRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnNextPageClicked(object sender, RoutedEventArgs e)
        {
            NextPageRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnJumpPageClicked(object sender, RoutedEventArgs e)
        {
            if (TryParsePage(out int page))
                JumpPageRequested?.Invoke(this, page);
        }

        private void OnPageJumpTextBoxKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter) return;
            if (TryParsePage(out int page))
                JumpPageRequested?.Invoke(this, page);
        }

        private bool TryParsePage(out int page)
        {
            page = 1;
            string text = PageJumpTextBox?.Text?.Trim() ?? "";
            return int.TryParse(text, out page) && page > 0;
        }
    }
}
