using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Wpf;

namespace HyCADTool.MarkdownEditor.Views.Controls
{
    public partial class PreviewPanelControl : UserControl
    {
        public event EventHandler TogglePageOrientationRequested;

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

        private void OnTogglePageOrientationClicked(object sender, RoutedEventArgs e)
        {
            TogglePageOrientationRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
