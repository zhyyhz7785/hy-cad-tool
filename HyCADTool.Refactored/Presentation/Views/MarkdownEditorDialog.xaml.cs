using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using HyCADTool.Refactored.Domain.Models.Text;
using HyCADTool.Refactored.Presentation.ViewModels;

namespace HyCADTool.Refactored.Presentation.Views
{
    public partial class MarkdownEditorDialog : Window
    {
        public MarkdownEditorViewModel ViewModel { get; }

        /// <summary>新建模式（默认尺寸）</summary>
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
            Loaded += (s, e) => RefreshPreview();
            ViewModel.PropertyChanged += OnPropChanged;
        }

        private void OnPropChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.MarkdownText)
                || e.PropertyName == nameof(ViewModel.ColumnCount)
                || e.PropertyName == nameof(ViewModel.PreviewScale))
            {
                RefreshPreview();
            }
        }

        private void RefreshPreview()
        {
            try
            {
                string html = MarkdownHtmlRenderer.ToInteractiveHtml(
                    ViewModel.MarkdownText ?? "", ViewModel.ColumnCount, ViewModel.PreviewScale);
                PreviewBrowser.NavigateToString(html);
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 从 WebBrowser JS 读取每栏的"字/行"值和段落分配
        /// </summary>
        private void SyncFromPreview()
        {
            try
            {
                // 读取每栏字符数
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

                // 读取每栏段落索引（格式: "0,1,2|3,4,5"）
                var parasResult = PreviewBrowser.InvokeScript("getColParas");
                if (parasResult is string parasStr && !string.IsNullOrEmpty(parasStr))
                {
                    ViewModel.ColumnParagraphIndices = parasStr;
                }
            }
            catch (Exception) { }
        }

        private void OnInsertClick(object sender, RoutedEventArgs e)
        {
            SyncFromPreview();
            DialogResult = true;
            Close();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
