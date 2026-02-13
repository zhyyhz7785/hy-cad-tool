using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using HyCADTool.Refactored.Domain.Models.Text;
using HyCADTool.Refactored.Presentation.ViewModels;

namespace HyCADTool.Refactored.Presentation.Views
{
    /// <summary>
    /// 回退版 Markdown 编辑器（TextBox + WebBrowser IE11 预览）
    /// 当 WebView2 编辑器不可用时使用
    /// </summary>
    public partial class MarkdownEditorDialog : Window
    {
        public MarkdownEditorViewModel ViewModel { get; }

        /// <summary>新建模式</summary>
        public MarkdownEditorDialog()
        {
            ViewModel = new MarkdownEditorViewModel();
            DataContext = ViewModel;
            InitializeComponent();
            Init();
        }

        /// <summary>二次编辑模式</summary>
        public MarkdownEditorDialog(string markdown, DesignSpecConfig config)
        {
            ViewModel = new MarkdownEditorViewModel(markdown, config);
            DataContext = ViewModel;
            InitializeComponent();
            Init();
        }

        private void Init()
        {
            Loaded += OnLoaded;
            ViewModel.PropertyChanged += OnPropChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshPreview();
        }

        #region 预览刷新

        private void OnPropChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.MarkdownText)
                || e.PropertyName == nameof(ViewModel.ColumnCount)
                || e.PropertyName == nameof(ViewModel.PreviewScale)
                || e.PropertyName == "SpacingChanged")
            {
                RefreshPreview();
            }
        }

        private void RefreshPreview()
        {
            try
            {
                var config = ViewModel.BuildConfig();
                string html = MarkdownHtmlRenderer.ToInteractiveHtml(
                    ViewModel.MarkdownText ?? "", ViewModel.ColumnCount, ViewModel.PreviewScale, config);
                PreviewBrowser.NavigateToString(html);
            }
            catch (System.Exception) { }
        }

        private void SyncFromPreview()
        {
            try
            {
                var charsResult = PreviewBrowser.InvokeScript("getColChars");
                if (charsResult is string csv && !string.IsNullOrEmpty(csv))
                {
                    var values = csv.Split(',')
                        .Select(s => { int v; return int.TryParse(s.Trim(), out v) ? v : 0; })
                        .Where(v => v > 0)
                        .ToArray();
                    if (values.Length > 0)
                        ViewModel.CharsPerColumn = values;
                }

                var parasResult = PreviewBrowser.InvokeScript("getColParas");
                if (parasResult is string parasStr && !string.IsNullOrEmpty(parasStr))
                {
                    ViewModel.ColumnParagraphIndices = parasStr;
                }
            }
            catch (System.Exception) { }
        }

        #endregion

        #region 按钮事件

        private void OnInsertClick(object sender, RoutedEventArgs e)
        {
            RefreshPreview();
            // 短暂等待预览渲染
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => { }));

            SyncFromPreview();
            ViewModel.ExecuteInsert();
            DialogResult = true;
            Close();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion
    }
}
