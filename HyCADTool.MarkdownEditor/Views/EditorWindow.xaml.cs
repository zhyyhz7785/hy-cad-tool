using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Navigation;
using System.Windows.Controls.Primitives;
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
        private bool _outlineVisible = true;
        private bool _bottomPanelVisible;
        private double _bottomPanelHeight = 140;
        private VditorJsHelper _js;
        private const int WM_MOUSEWHEEL = 0x020A;
        private readonly DispatcherTimer _previewRulerSyncTimer;

        public EditorWindow(EditorInput input)
        {
            ViewModel = new EditorViewModel(input);
            DataContext = ViewModel;
            InitializeComponent();
            ApplyOutlineLayout();

            _previewRulerSyncTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _previewRulerSyncTimer.Tick += OnPreviewRulerSyncTimerTick;

            Loaded += OnLoaded;
            Closed += OnClosed;
            ViewModel.PropertyChanged += OnPropChanged;
            ViewModel.EditorContentLoadRequested += OnEditorContentLoadRequested;
        }

        #region WebView2 初始化

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;
            PreviewBrowser.LoadCompleted += OnPreviewBrowserLoadCompleted;
            try
            {
                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "HyCADTool", "WebView2");
                Directory.CreateDirectory(userDataFolder);

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await EditorWebView.EnsureCoreWebView2Async(env);
                _js = new VditorJsHelper(script => EditorWebView.CoreWebView2.ExecuteScriptAsync(script));

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
            UpdateRulerScale();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            ComponentDispatcher.ThreadPreprocessMessage -= OnThreadPreprocessMessage;
            PreviewBrowser.LoadCompleted -= OnPreviewBrowserLoadCompleted;
            _previewRulerSyncTimer.Stop();
        }

        private void OnPreviewBrowserLoadCompleted(object sender, NavigationEventArgs e)
        {
            SyncRulerExtentFromPreview();
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

        private static System.Windows.Media.SolidColorBrush BrushFromHex(string hex) =>
            new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));

        private void OnToggleOutline(object sender, RoutedEventArgs e)
        {
            _outlineVisible = !_outlineVisible;
            ApplyOutlineLayout();
        }

        private void ApplyOutlineLayout()
        {
            if (_outlineVisible)
            {
                OutlineCol.Width = new GridLength(220, GridUnitType.Pixel);
                OutlineSplitterCol.Width = new GridLength(4, GridUnitType.Pixel);
                OutlineDivider.Visibility = Visibility.Visible;
                OutlineToggleBtn.Foreground = BrushFromHex("#cccccc");
            }
            else
            {
                OutlineCol.Width = new GridLength(0);
                OutlineSplitterCol.Width = new GridLength(0);
                OutlineDivider.Visibility = Visibility.Collapsed;
                OutlineToggleBtn.Foreground = BrushFromHex("#858585");
            }
        }

        private void OnToggleBottomPanel(object sender, RoutedEventArgs e)
        {
            _bottomPanelVisible = !_bottomPanelVisible;
            ApplyBottomPanelLayout();
            ViewModel.StatusText = _bottomPanelVisible ? "底部面板：已打开" : "底部面板：关闭";
            BottomPanelToggleBtn.Foreground = BrushFromHex(_bottomPanelVisible ? "#cccccc" : "#858585");
        }

        private void ApplyBottomPanelLayout()
        {
            bool show = _bottomPanelVisible && _previewVisible;
            BottomPanelSplitterRow.Height = show
                ? new GridLength(4, GridUnitType.Pixel)
                : new GridLength(0, GridUnitType.Pixel);
            BottomPanelRow.Height = show
                ? new GridLength(Math.Max(80, _bottomPanelHeight), GridUnitType.Pixel)
                : new GridLength(0, GridUnitType.Pixel);
            BottomPanelSplitter.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            BottomPanelHost.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnBottomPanelSplitterDragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (BottomPanelRow.ActualHeight > 0)
            {
                _bottomPanelHeight = BottomPanelRow.ActualHeight;
            }
        }

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
                PreviewRulerGrid.Visibility = Visibility.Visible;
                _previewRulerSyncTimer.Start();
                SyncRulerExtentFromPreview();
                ApplyBottomPanelLayout();
                PreviewToggleBtn.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#cccccc"));
            }
            else
            {
                SplitterCol.Width = new GridLength(0);
                PreviewCol.Width = new GridLength(0);
                PreviewSplitter.Visibility = Visibility.Collapsed;
                PreviewRulerGrid.Visibility = Visibility.Collapsed;
                _previewRulerSyncTimer.Stop();
                ApplyBottomPanelLayout();
                PreviewToggleBtn.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#858585"));
            }
        }

        private double GetDpiScale()
        {
            try
            {
                var src = System.Windows.PresentationSource.FromVisual(this);
                if (src?.CompositionTarget != null)
                    return src.CompositionTarget.TransformToDevice.M22;
            }
            catch { }
            return 1.0;
        }

        private void UpdateRulerScale()
        {
            // 与 WebBrowser(IE) 的 DPI 口径补偿保持一致
            double x = Math.Max(0.1, ViewModel.PreviewScale);
            double dpiScale = GetDpiScale();
            double ppm = x / Math.Max(0.5, dpiScale);
            PreviewHRuler.PixelsPerMm = ppm;
            PreviewVRuler.PixelsPerMm = ppm;
            PreviewHRuler.SegmentCount = 1;
            UpdateRulerExtentByConfig(dpiScale);
        }

        private void UpdateRulerExtentByConfig(double dpiScale)
        {
            // 先按配置给出初值；后续由 getPaperSize() 实时覆盖（支持纸面拖拽）
            var cfg = ViewModel.BuildConfig();
            double wDip = (cfg.PageWidthMm * Math.Max(0.1, ViewModel.PreviewScale)) / Math.Max(0.5, dpiScale);
            double hDip = (cfg.PageHeightMm * Math.Max(0.1, ViewModel.PreviewScale)) / Math.Max(0.5, dpiScale);
            PreviewHRuler.Width = Math.Max(1, wDip);
            PreviewVRuler.Height = Math.Max(1, hDip);
        }

        private void OnPreviewRulerSyncTimerTick(object sender, EventArgs e)
        {
            SyncRulerExtentFromPreview();
        }

        private void SyncRulerExtentFromPreview()
        {
            if (!_previewVisible || PreviewBrowser.Visibility != Visibility.Visible) return;
            try
            {
                var result = PreviewBrowser.InvokeScript("getPaperSize");
                if (!(result is string csv) || string.IsNullOrWhiteSpace(csv)) return;
                var parts = csv.Split(',');
                if (parts.Length != 2) return;
                if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pxW)) return;
                if (!double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pxH)) return;

                double dpiScale = GetDpiScale();
                PreviewHRuler.Width = Math.Max(1, pxW / Math.Max(0.5, dpiScale));
                PreviewVRuler.Height = Math.Max(1, pxH / Math.Max(0.5, dpiScale));
            }
            catch
            {
                // 页面加载中时 InvokeScript 可能失败，忽略即可
            }
        }

        private void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            if (handled) return;
            if (!_previewVisible || PreviewRulerGrid.Visibility != Visibility.Visible) return;

            if (msg.message == WM_MOUSEWHEEL)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
                if (!PreviewRulerGrid.IsMouseOver) return;

                int wheelDelta = (short)((msg.wParam.ToInt64() >> 16) & 0xffff);
                if (wheelDelta == 0) return;

                double step = wheelDelta > 0 ? 0.1 : -0.1;
                ViewModel.PreviewScale = Math.Max(0.1, Math.Min(3.0, ViewModel.PreviewScale + step));
                handled = true;
            }
        }

        #endregion

        #region 预览刷新

        private void OnPropChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.ColumnCount)
                || e.PropertyName == nameof(ViewModel.ColumnGutter)
                || e.PropertyName == nameof(ViewModel.TextSize)
                || e.PropertyName == nameof(ViewModel.TextXScale)
                || e.PropertyName == nameof(ViewModel.DrawScale)
                || e.PropertyName == nameof(ViewModel.PagePreset)
                || e.PropertyName == nameof(ViewModel.PreviewScale)
                || e.PropertyName == "SpacingChanged")
            {
                RefreshPreview();
            }
            if (e.PropertyName == nameof(ViewModel.PreviewScale))
            {
                UpdateRulerScale();
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

        private async Task RunMenuActionAsync(Func<Task> action)
        {
            if (!_editorReady || _js == null) return;
            try { await action(); } catch { }
        }

        private async void OnMenuUndo(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.UndoAsync());

        private async void OnMenuRedo(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.RedoAsync());

        private async void OnMenuCut(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.CutAsync());

        private async void OnMenuCopy(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.CopyAsync());

        private async void OnMenuPaste(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.PasteAsync());

        private async void OnMenuSelectAll(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.SelectAllAsync());

        private async void OnMenuFindReplace(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.FindReplaceAsync());

        private async void OnMenuH1(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.InsertHeadingAsync(1));

        private async void OnMenuH2(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.InsertHeadingAsync(2));

        private async void OnMenuH3(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.InsertHeadingAsync(3));

        private async void OnMenuH4(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.InsertHeadingAsync(4));

        private async void OnMenuParagraph(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.InsertParagraphAsync());

        private async void OnMenuQuote(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.InsertQuoteAsync());

        private async void OnMenuOrderedList(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.InsertOrderedListAsync());

        private async void OnMenuUnorderedList(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.InsertUnorderedListAsync());

        private async void OnMenuTaskList(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.InsertTaskListAsync());

        private async void OnMenuTable(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.InsertTableAsync());

        private async void OnMenuCodeBlock(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.InsertCodeBlockAsync());

        private async void OnMenuMathBlock(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.InsertMathBlockAsync());

        private async void OnMenuToc(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.InsertTocAsync());

        private async void OnMenuFootnote(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.InsertFootnoteAsync());

        private async void OnMenuHorizontalRule(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.InsertHorizontalRuleAsync());

        private async void OnMenuBold(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.ToggleBoldAsync());

        private async void OnMenuItalic(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.ToggleItalicAsync());

        private async void OnMenuStrikethrough(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.ToggleStrikethroughAsync());

        private async void OnMenuInlineCode(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.ToggleInlineCodeAsync());

        private async void OnMenuUnderline(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.ToggleUnderlineAsync());

        private async void OnMenuHighlight(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.ToggleHighlightAsync());

        private async void OnMenuSuperscript(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.ToggleSuperscriptAsync());

        private async void OnMenuSubscript(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.ToggleSubscriptAsync());

        private async void OnMenuComment(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.ToggleCommentAsync());

        private async void OnMenuInlineMath(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.ToggleInlineMathAsync());

        private async void OnMenuLink(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.InsertLinkAsync());

        private async void OnMenuClearFormatting(object sender, RoutedEventArgs e) =>
            await RunMenuActionAsync(() => _js.ClearFormattingAsync());

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
