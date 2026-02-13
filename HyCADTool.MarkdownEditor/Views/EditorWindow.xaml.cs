using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using HyCADTool.MarkdownEditor.Html;
using HyCADTool.MarkdownEditor.Models;
using HyCADTool.MarkdownEditor.ViewModels;
using Newtonsoft.Json.Linq;

namespace HyCADTool.MarkdownEditor.Views
{
    public partial class EditorWindow : Window
    {
        public EditorViewModel ViewModel { get; }

        /// <summary>编辑完成后的结果（供 EditorLauncher 读取）</summary>
        public EditorResult Result { get; private set; }

        private bool _editorReady;
        private bool _previewVisible;

        public EditorWindow(EditorInput input)
        {
            ViewModel = new EditorViewModel(input);
            DataContext = ViewModel;
            InitializeComponent();

            Loaded += OnLoaded;
            ViewModel.PropertyChanged += OnPropChanged;
            ViewModel.EditorContentLoadRequested += OnEditorContentLoadRequested;
        }

        #region WebView2 初始化

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "HyCADTool", "WebView2");
                Directory.CreateDirectory(userDataFolder);

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await EditorWebView.EnsureCoreWebView2Async(env);

                EditorWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                string html = VditorHtmlTemplate.Generate(ViewModel.MarkdownText);
                EditorWebView.CoreWebView2.NavigateToString(html);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Vditor 编辑器加载失败：{ex.Message}\n\n请确认已安装 WebView2 Runtime。",
                    "编辑器初始化错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            // 初次刷新隐藏预览（后台计算用）
            RefreshPreview();
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                string json = args.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(json)) return;

                var msg = JObject.Parse(json);
                string type = msg.Value<string>("type");

                switch (type)
                {
                    case "ready":
                        _editorReady = true;
                        break;

                    case "input":
                        string value = msg.Value<string>("value") ?? "";
                        ViewModel.SetMarkdownFromEditor(value);
                        RefreshPreview();
                        break;
                }
            }
            catch { }
        }

        private async void OnEditorContentLoadRequested(string markdown)
        {
            if (!_editorReady || EditorWebView?.CoreWebView2 == null) return;
            try
            {
                string escaped = Newtonsoft.Json.JsonConvert.SerializeObject(markdown ?? "");
                await EditorWebView.CoreWebView2.ExecuteScriptAsync($"setContent({escaped})");
            }
            catch { }
        }

        #endregion

        #region 预览切换

        private void OnTogglePreview(object sender, RoutedEventArgs e)
        {
            _previewVisible = !_previewVisible;
            ApplyPreviewLayout();
            if (_previewVisible)
                RefreshPreview();
        }

        private void ApplyPreviewLayout()
        {
            if (_previewVisible)
            {
                SplitterCol.Width = new GridLength(4, GridUnitType.Pixel);
                PreviewCol.Width = new GridLength(1.2, GridUnitType.Star);
                PreviewSplitter.Visibility = Visibility.Visible;
                PreviewBrowser.Visibility = Visibility.Visible;
                PreviewToggleBtn.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#cccccc"));
            }
            else
            {
                SplitterCol.Width = new GridLength(0);
                PreviewCol.Width = new GridLength(0);
                PreviewSplitter.Visibility = Visibility.Collapsed;
                PreviewBrowser.Visibility = Visibility.Collapsed;
                PreviewToggleBtn.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#858585"));
            }
        }

        #endregion

        #region 预览刷新

        private void OnPropChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.ColumnCount)
                || e.PropertyName == nameof(ViewModel.PreviewScale)
                || e.PropertyName == "SpacingChanged")
            {
                RefreshPreview();
            }
        }

        /// <summary>刷新预览 HTML（无论预览是否可见都执行，供后台计算分栏数据）</summary>
        private void RefreshPreview()
        {
            try
            {
                // WebBrowser 在 Collapsed 时 NavigateToString 仍可执行
                // 但 InvokeScript 需要文档已加载，在 OnConfirmClick 中会临时显示
                var config = ViewModel.BuildConfig();
                string html = PreviewHtmlRenderer.ToInteractiveHtml(
                    ViewModel.MarkdownText ?? "", ViewModel.ColumnCount, ViewModel.PreviewScale, config);
                PreviewBrowser.NavigateToString(html);
            }
            catch { }
        }

        /// <summary>从预览 JS 读取分栏数据（每栏字符数 + 段落分配）</summary>
        private void SyncFromPreview()
        {
            try
            {
                var charsResult = PreviewBrowser.InvokeScript("getColChars");
                if (charsResult is string csv && !string.IsNullOrEmpty(csv))
                {
                    var values = csv.Split(',')
                        .Select(s => int.TryParse(s.Trim(), out int v) ? v : 0)
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
            catch { }
        }

        #endregion

        #region 按钮事件

        private async void OnConfirmClick(object sender, RoutedEventArgs e)
        {
            // 1. 从 Vditor 获取最新 Markdown
            await SyncMarkdownFromEditorAsync();

            // 2. 临时显示 PreviewBrowser 确保 JS 可执行
            bool wasHidden = PreviewBrowser.Visibility != Visibility.Visible;
            if (wasHidden)
            {
                PreviewBrowser.Visibility = Visibility.Visible;
                PreviewBrowser.Width = 1;
                PreviewBrowser.Height = 1;
            }

            // 3. 刷新分栏预览
            RefreshPreview();
            await Task.Delay(300);

            // 4. 从预览读取每栏字符数和段落分配
            SyncFromPreview();

            // 5. 恢复隐藏
            if (wasHidden)
            {
                PreviewBrowser.Visibility = Visibility.Collapsed;
                PreviewBrowser.Width = double.NaN;
                PreviewBrowser.Height = double.NaN;
            }

            // 6. 构建结果
            Result = new EditorResult
            {
                Confirmed = true,
                Markdown = ViewModel.MarkdownText,
                Config = ViewModel.BuildConfig(),
                CharsPerColumn = ViewModel.CharsPerColumn,
                ColumnParagraphIndices = ViewModel.ColumnParagraphIndices
            };

            DialogResult = true;
            Close();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Result = new EditorResult { Confirmed = false };
            DialogResult = false;
            Close();
        }

        private async Task SyncMarkdownFromEditorAsync()
        {
            if (!_editorReady || EditorWebView?.CoreWebView2 == null) return;
            try
            {
                string result = await EditorWebView.CoreWebView2.ExecuteScriptAsync("getContent()");
                if (!string.IsNullOrEmpty(result) && result != "null")
                {
                    string md = Newtonsoft.Json.JsonConvert.DeserializeObject<string>(result);
                    if (md != null)
                        ViewModel.SetMarkdownFromEditor(md);
                }
            }
            catch { }
        }

        #endregion
    }
}
