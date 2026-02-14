using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Interop;
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
        private bool _previewVisible = true;
        private bool _outlineVisible = true;
        private bool _bottomPanelVisible = true;
        private double _bottomPanelHeight = 160;
        private VditorJsHelper _js;
        private const int WM_MOUSEWHEEL = 0x020A;
        private readonly DispatcherTimer _rulerSyncTimer;
        private readonly DispatcherTimer _previewRefreshDebounceTimer;
        private const int PreviewRefreshDebounceMs = 180;
        private double _renderedPreviewScale = 1.0;
        private bool _updatingFileList;
        private bool _showFileTab = true;
        private const double MinPreviewVisibleWidth = 280;
        private const double DefaultPreviewPanelWidth = 420;
        private const double MinOutlinePanelWidth = 120;
        private const double DefaultOutlinePanelWidth = 220;
        private const double OutlineSplitterWidth = 4;
        private const double RulerThickness = 24;
        private const double DefaultEditorMinWidth = 180;
        private const double DefaultRatioLeft = 1.0;
        private const double DefaultRatioEditor = 2.0;
        private const double DefaultRatioPreview = 4.0;
        private bool _isDarkTheme = true;
        private bool _rulerVisible = true;
        private double _outlinePanelWidth = DefaultOutlinePanelWidth;
        private double _previewPanelWidth = DefaultPreviewPanelWidth;
        private bool _defaultWorkspaceLayoutApplied;

        public EditorWindow(EditorInput input)
        {
            ViewModel = new EditorViewModel(input);
            DataContext = ViewModel;
            InitializeComponent();

            _rulerSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _rulerSyncTimer.Tick += (_, __) => SyncRulerFromPaper();
            _previewRefreshDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PreviewRefreshDebounceMs) };
            _previewRefreshDebounceTimer.Tick += (_, __) =>
            {
                _previewRefreshDebounceTimer.Stop();
                RefreshPreview();
            };

            ApplyTheme(_isDarkTheme);
            UpdateWindowCaptionButtons();
            ApplyOutlineLayout();
            ApplyLeftPanelTab();

            Loaded += OnLoaded;
            Closed += OnClosed;
            StateChanged += OnWindowStateChanged;
            ViewModel.PropertyChanged += OnPropChanged;
            ViewModel.EditorContentLoadRequested += OnEditorContentLoadRequested;
        }

        #region WebView2 初始化

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;
            try
            {
                // 并行：1) 确保本地缓存 2) 初始化 WebView2 环境
                var cacheTask = VditorCacheManager.EnsureCachedAsync(msg =>
                    Dispatcher.Invoke(() => ViewModel.StatusText = msg));

                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "HyCADTool", "WebView2");
                Directory.CreateDirectory(userDataFolder);
                var envTask = CoreWebView2Environment.CreateAsync(null, userDataFolder);

                await Task.WhenAll(cacheTask, envTask);

                var env = envTask.Result;
                await EditorWebView.EnsureCoreWebView2Async(env);

                // 设置本地虚拟主机映射（如果缓存就绪）
                string cdnBase = null;
                if (VditorCacheManager.IsCached)
                {
                    EditorWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        "vditor.local", VditorCacheManager.CacheDir,
                        CoreWebView2HostResourceAccessKind.Allow);
                    cdnBase = "https://vditor.local";
                }

                _js = new VditorJsHelper(script => EditorWebView.CoreWebView2.ExecuteScriptAsync(script));
                EditorWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                string html = VditorHtmlTemplate.Generate(ViewModel.MarkdownText, cdnBase);
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

            // 初次刷新
            RefreshPreview();
            RefreshOutline();
            RefreshFileList();
            ApplyDefaultWorkspaceLayout(false);

            // 布局就绪后，让 A2 图纸占满 90% 预览区
            Dispatcher.BeginInvoke(new Action(() =>
            {
                FitPaperToPreviewArea();
                UpdateRulerScale();
            }), DispatcherPriority.Loaded);
        }

        private void OnClosed(object sender, EventArgs e)
        {
            ComponentDispatcher.ThreadPreprocessMessage -= OnThreadPreprocessMessage;
            _rulerSyncTimer.Stop();
            _previewRefreshDebounceTimer.Stop();
            StateChanged -= OnWindowStateChanged;
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

        private System.Windows.Media.Brush ThemeBrush(string key)
        {
            if (TryFindResource(key) is System.Windows.Media.Brush brush)
                return brush;
            return BrushFromHex("#c9d1d9");
        }

        private void SetThemeColor(string key, string hex)
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            var brush = new System.Windows.Media.SolidColorBrush(color);
            brush.Freeze();
            Resources[key] = brush;
        }

        private void SetThemeColor(object key, string hex)
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            var brush = new System.Windows.Media.SolidColorBrush(color);
            brush.Freeze();
            Resources[key] = brush;
        }

        private void ApplyTheme(bool dark)
        {
            _isDarkTheme = dark;
            if (dark)
            {
                SetThemeColor("ThemeBgRootBrush", "#111418");
                SetThemeColor("ThemeBgPanelBrush", "#161b22");
                SetThemeColor("ThemeBgSurfaceBrush", "#1c2128");
                SetThemeColor("ThemeBgInputBrush", "#0d1117");
                SetThemeColor("ThemeBorderBrush", "#24292e");
                SetThemeColor("ThemeBorderStrongBrush", "#30363d");
                SetThemeColor("ThemeTextPrimaryBrush", "#c9d1d9");
                SetThemeColor("ThemeTextSecondaryBrush", "#8b949e");
                SetThemeColor("ThemeTextMutedBrush", "#6e7681");
                SetThemeColor("ThemeAccentBrush", "#58a6ff");
                SetThemeColor("ThemeAccentHoverBrush", "#79c0ff");
                SetThemeColor("ThemeOnAccentBrush", "#ffffff");
                SetThemeColor("ThemeHoverBrush", "#30363d");
                SetThemeColor("ThemeSelectionBrush", "#263545");

                SetThemeColor(SystemColors.MenuBrushKey, "#1c2128");
                SetThemeColor(SystemColors.MenuTextBrushKey, "#c9d1d9");
                SetThemeColor(SystemColors.MenuHighlightBrushKey, "#30363d");
                SetThemeColor(SystemColors.HighlightBrushKey, "#30363d");
                SetThemeColor(SystemColors.HighlightTextBrushKey, "#ffffff");
                SetThemeColor(SystemColors.MenuBarBrushKey, "#161b22");
                SetThemeColor(SystemColors.WindowBrushKey, "#1c2128");
                SetThemeColor(SystemColors.WindowTextBrushKey, "#c9d1d9");
            }
            else
            {
                SetThemeColor("ThemeBgRootBrush", "#f5f7fa");
                SetThemeColor("ThemeBgPanelBrush", "#eef2f6");
                SetThemeColor("ThemeBgSurfaceBrush", "#ffffff");
                SetThemeColor("ThemeBgInputBrush", "#ffffff");
                SetThemeColor("ThemeBorderBrush", "#d0d7de");
                SetThemeColor("ThemeBorderStrongBrush", "#b6c2cf");
                SetThemeColor("ThemeTextPrimaryBrush", "#24292f");
                SetThemeColor("ThemeTextSecondaryBrush", "#57606a");
                SetThemeColor("ThemeTextMutedBrush", "#6e7781");
                SetThemeColor("ThemeAccentBrush", "#0969da");
                SetThemeColor("ThemeAccentHoverBrush", "#1f6feb");
                SetThemeColor("ThemeOnAccentBrush", "#ffffff");
                SetThemeColor("ThemeHoverBrush", "#dde6ef");
                SetThemeColor("ThemeSelectionBrush", "#dbeafe");

                SetThemeColor(SystemColors.MenuBrushKey, "#ffffff");
                SetThemeColor(SystemColors.MenuTextBrushKey, "#24292f");
                SetThemeColor(SystemColors.MenuHighlightBrushKey, "#dde6ef");
                SetThemeColor(SystemColors.HighlightBrushKey, "#dde6ef");
                SetThemeColor(SystemColors.HighlightTextBrushKey, "#24292f");
                SetThemeColor(SystemColors.MenuBarBrushKey, "#eef2f6");
                SetThemeColor(SystemColors.WindowBrushKey, "#ffffff");
                SetThemeColor(SystemColors.WindowTextBrushKey, "#24292f");
            }

            ApplyLeftPanelTab();
            ApplyOutlineLayout();
            ApplyPreviewLayout();
            ApplyRulerLayout();
        }

        private void OnThemeDark(object sender, RoutedEventArgs e)
        {
            ApplyTheme(true);
            ViewModel.StatusText = "主题：黑色为主";
        }

        private void OnThemeLight(object sender, RoutedEventArgs e)
        {
            ApplyTheme(false);
            ViewModel.StatusText = "主题：白色为主";
        }

        private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (e.ClickCount == 2)
            {
                ToggleMaxRestore();
                return;
            }

            try { DragMove(); } catch { }
        }

        private void OnTitleBarMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Right) return;
            Point screenPoint = PointToScreen(e.GetPosition(this));
            SystemCommands.ShowSystemMenu(this, screenPoint);
        }

        private void OnMinimizeWindowClick(object sender, RoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
        }

        private void OnMaxRestoreWindowClick(object sender, RoutedEventArgs e)
        {
            ToggleMaxRestore();
        }

        private void ToggleMaxRestore()
        {
            if (ResizeMode == ResizeMode.NoResize || ResizeMode == ResizeMode.CanMinimize) return;

            if (WindowState == WindowState.Maximized)
                SystemCommands.RestoreWindow(this);
            else
                SystemCommands.MaximizeWindow(this);
        }

        private void OnWindowStateChanged(object sender, EventArgs e)
        {
            UpdateWindowCaptionButtons();
            if (WindowState == WindowState.Maximized)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyDefaultWorkspaceLayout(true);
                    FitPaperToPreviewArea();
                }), DispatcherPriority.Loaded);
            }
        }

        private void UpdateWindowCaptionButtons()
        {
            if (MaxRestoreGlyph == null || MaxRestoreWindowBtn == null) return;

            bool isMaximized = WindowState == WindowState.Maximized;
            MaxRestoreGlyph.Text = isMaximized ? "\uE923" : "\uE922";
            MaxRestoreWindowBtn.ToolTip = isMaximized ? "还原" : "最大化";
        }

        private void OnToggleOutline(object sender, RoutedEventArgs e)
        {
            _outlineVisible = !_outlineVisible;
            ApplyOutlineLayout();
            RelayoutPreviewIfNeeded();
        }

        private void OnShowFileTab(object sender, RoutedEventArgs e)
        {
            _showFileTab = true;
            ApplyLeftPanelTab();
            RelayoutPreviewIfNeeded();
        }

        private void OnShowOutlineTab(object sender, RoutedEventArgs e)
        {
            _showFileTab = false;
            ApplyLeftPanelTab();
            RelayoutPreviewIfNeeded();
        }

        private void ApplyLeftPanelTab()
        {
            if (FileTree == null || OutlineList == null || FileTabBtn == null || OutlineTabBtn == null) return;

            FileTree.Visibility = _showFileTab ? Visibility.Visible : Visibility.Collapsed;
            OutlineList.Visibility = _showFileTab ? Visibility.Collapsed : Visibility.Visible;

            FileTabBtn.Foreground = _showFileTab ? ThemeBrush("ThemeTextPrimaryBrush") : ThemeBrush("ThemeTextSecondaryBrush");
            OutlineTabBtn.Foreground = _showFileTab ? ThemeBrush("ThemeTextSecondaryBrush") : ThemeBrush("ThemeTextPrimaryBrush");
        }

        /// <summary>
        /// 左侧布局切换后，强制重绘预览，规避 WebBrowser 在重排后偶发空白。
        /// </summary>
        private void RelayoutPreviewIfNeeded()
        {
            if (!_previewVisible) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                EnsurePreviewColumnVisible();
                RefreshPreview();
                SyncRulerFromPaper();
            }), DispatcherPriority.Background);
        }

        private void EnsurePreviewColumnVisible()
        {
            if (!_previewVisible) return;
            if (PreviewCol == null) return;

            bool tooNarrowByActual = PreviewCol.ActualWidth > 0 && PreviewCol.ActualWidth < MinPreviewVisibleWidth;
            bool collapsed = PreviewCol.Width.Value <= 0;

            if (tooNarrowByActual || collapsed)
            {
                double target = Math.Max(MinPreviewVisibleWidth, _previewPanelWidth);
                PreviewCol.Width = new GridLength(target, GridUnitType.Pixel);
            }
        }

        private void ApplyOutlineLayout()
        {
            if (OutlineCol == null || OutlineSplitterCol == null || OutlineSplitter == null || OutlineToggleBtn == null)
                return;

            if (_outlineVisible)
            {
                OutlineCol.Width = new GridLength(Math.Max(MinOutlinePanelWidth, _outlinePanelWidth), GridUnitType.Pixel);
                OutlineSplitterCol.Width = new GridLength(OutlineSplitterWidth, GridUnitType.Pixel);
                OutlineSplitter.Visibility = Visibility.Visible;
                OutlineToggleBtn.Foreground = ThemeBrush("ThemeTextPrimaryBrush");
            }
            else
            {
                if (OutlineCol.ActualWidth > 0)
                {
                    _outlinePanelWidth = Math.Max(MinOutlinePanelWidth, OutlineCol.ActualWidth);
                }

                OutlineCol.Width = new GridLength(0, GridUnitType.Pixel);
                OutlineSplitterCol.Width = new GridLength(0, GridUnitType.Pixel);
                OutlineSplitter.Visibility = Visibility.Collapsed;
                OutlineToggleBtn.Foreground = ThemeBrush("ThemeTextSecondaryBrush");
            }
        }

        private void OnOutlineSplitterDragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (OutlineCol?.ActualWidth > 0)
            {
                _outlinePanelWidth = Math.Max(MinOutlinePanelWidth, OutlineCol.ActualWidth);
            }
        }

        private void OnPreviewSplitterDragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (PreviewCol?.ActualWidth > 0)
            {
                _previewPanelWidth = Math.Max(MinPreviewVisibleWidth, PreviewCol.ActualWidth);
                PreviewCol.Width = new GridLength(_previewPanelWidth, GridUnitType.Pixel);
            }
        }

        private void OnToggleBottomPanel(object sender, RoutedEventArgs e)
        {
            _bottomPanelVisible = !_bottomPanelVisible;
            ApplyBottomPanelLayout();
            ViewModel.StatusText = _bottomPanelVisible ? "底部面板：已打开" : "底部面板：关闭";
            BottomPanelToggleBtn.Foreground = _bottomPanelVisible
                ? ThemeBrush("ThemeTextPrimaryBrush")
                : ThemeBrush("ThemeTextSecondaryBrush");
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

        private void OnToggleRuler(object sender, RoutedEventArgs e)
        {
            _rulerVisible = !_rulerVisible;
            ApplyRulerLayout();
            ViewModel.StatusText = _rulerVisible ? "标线：已显示" : "标线：已隐藏";
        }

        private void OnTogglePageOrientation(object sender, RoutedEventArgs e)
        {
            ViewModel.IsLandscape = !ViewModel.IsLandscape;
            ViewModel.StatusText = $"图纸方向：{ViewModel.PageOrientationLabel}";
        }

        private void ApplyPreviewLayout()
        {
            if (SplitterCol == null || PreviewCol == null || PreviewSplitter == null || PreviewRulerGrid == null || PreviewToggleBtn == null)
                return;

            if (_previewVisible)
            {
                SplitterCol.Width = new GridLength(4, GridUnitType.Pixel);
                PreviewCol.MinWidth = MinPreviewVisibleWidth;
                PreviewCol.Width = new GridLength(Math.Max(MinPreviewVisibleWidth, _previewPanelWidth), GridUnitType.Pixel);
                PreviewSplitter.Visibility = Visibility.Visible;
                PreviewRulerGrid.Visibility = Visibility.Visible;
                _rulerSyncTimer?.Start();
                EnsurePreviewColumnVisible();
                ApplyRulerLayout();
                ApplyBottomPanelLayout();
                PreviewToggleBtn.Foreground = ThemeBrush("ThemeTextPrimaryBrush");
            }
            else
            {
                if (PreviewCol.ActualWidth > 0)
                {
                    _previewPanelWidth = Math.Max(MinPreviewVisibleWidth, PreviewCol.ActualWidth);
                }

                SplitterCol.Width = new GridLength(0);
                PreviewCol.MinWidth = 0;
                PreviewCol.Width = new GridLength(0);
                PreviewSplitter.Visibility = Visibility.Collapsed;
                PreviewRulerGrid.Visibility = Visibility.Collapsed;
                _rulerSyncTimer?.Stop();
                ApplyRulerLayout();
                ApplyBottomPanelLayout();
                PreviewToggleBtn.Foreground = ThemeBrush("ThemeTextSecondaryBrush");
            }
        }

        private void ApplyRulerLayout()
        {
            if (RulerRow == null || RulerCol == null || RulerCorner == null || PreviewHRuler == null || PreviewVRuler == null || RulerToggleBtn == null)
                return;

            bool show = _previewVisible && _rulerVisible;
            RulerRow.Height = show ? new GridLength(RulerThickness, GridUnitType.Pixel) : new GridLength(0, GridUnitType.Pixel);
            RulerCol.Width = show ? new GridLength(RulerThickness, GridUnitType.Pixel) : new GridLength(0, GridUnitType.Pixel);

            var rulerVisibility = show ? Visibility.Visible : Visibility.Collapsed;
            RulerCorner.Visibility = rulerVisibility;
            PreviewHRuler.Visibility = rulerVisibility;
            PreviewVRuler.Visibility = rulerVisibility;

            RulerToggleBtn.Foreground = _rulerVisible
                ? ThemeBrush("ThemeTextPrimaryBrush")
                : ThemeBrush("ThemeTextSecondaryBrush");
        }

        private void ApplyDefaultWorkspaceLayout(bool force)
        {
            if (_defaultWorkspaceLayoutApplied && !force) return;
            if (OutlineCol == null || EditorCol == null || PreviewCol == null || SplitterCol == null || PreviewSplitter == null) return;

            double ratioSum = DefaultRatioLeft + DefaultRatioEditor + DefaultRatioPreview;
            double windowWidth = ActualWidth > 0 ? ActualWidth : Width;
            double minRequired = MinOutlinePanelWidth + DefaultEditorMinWidth + MinPreviewVisibleWidth;
            double splitterReserve = OutlineSplitterWidth + 4 + 24;
            double available = Math.Max(minRequired, windowWidth - splitterReserve);
            double unit = available / ratioSum;

            _outlinePanelWidth = Math.Max(MinOutlinePanelWidth, unit * DefaultRatioLeft);
            double editorMinWidth = Math.Max(DefaultEditorMinWidth, unit * DefaultRatioEditor * 0.55);
            _previewPanelWidth = Math.Max(MinPreviewVisibleWidth, unit * DefaultRatioPreview);
            _bottomPanelHeight = Math.Max(_bottomPanelHeight, 160);

            _outlineVisible = true;
            _previewVisible = true;
            _bottomPanelVisible = true;

            ApplyOutlineLayout();
            ApplyPreviewLayout();
            EditorCol.MinWidth = editorMinWidth;
            EditorCol.Width = new GridLength(1, GridUnitType.Star);
            ApplyBottomPanelLayout();
            UpdateRulerScale();

            _defaultWorkspaceLayoutApplied = true;
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

        /// <summary>
        /// 标尺 ppm 与 HTML 图纸使用同一个 PreviewScale：
        /// HTML 中 paper width = PageWidthMm * PreviewScale (px)
        /// 标尺 ppm = PreviewScale（每毫米对应多少 WPF DIP）
        /// 但 WebBrowser 渲染时使用的是 物理像素，而 WPF 标尺使用 DIP，
        /// 因此需要除以 DPI 才能让标尺刻度与 WebBrowser 内的图纸对齐。
        /// </summary>
        private void UpdateRulerScale()
        {
            if (PreviewHRuler == null || PreviewVRuler == null) return;

            double scale = Math.Max(0.1, ViewModel.PreviewScale);
            double dpiScale = GetDpiScale();
            // WebBrowser 内像素 = CSS px = 物理像素；WPF 标尺 = DIP
            // 1 mm 在 HTML 中 = scale 个 CSS px = scale 个物理像素 = scale/dpiScale 个 DIP
            double ppm = scale / Math.Max(0.5, dpiScale);
            PreviewHRuler.PixelsPerMm = Math.Max(0.01, ppm);
            PreviewVRuler.PixelsPerMm = Math.Max(0.01, ppm);
            PreviewHRuler.SegmentCount = 1;
        }

        /// <summary>
        /// 根据预览区实际宽度，计算让图纸占 90% 区域的 PreviewScale。
        /// </summary>
        private void FitPaperToPreviewArea()
        {
            if (PreviewBrowser == null || !_previewVisible) return;
            double areaWidth = PreviewBrowser.ActualWidth;
            if (areaWidth <= 0) areaWidth = PreviewRulerGrid.ActualWidth - 24; // 减去左侧标尺宽
            if (areaWidth <= 100) return;

            double dpiScale = GetDpiScale();
            double pageMm = Math.Max(100, ViewModel.PageWidthMm);
            // 目标：paperPx = pageMm * scale; WPF 区域 = areaWidth DIP = areaWidth * dpi 物理像素
            // paperPx = 0.9 * areaWidth * dpi  →  scale = 0.9 * areaWidth * dpi / pageMm
            double targetScale = 0.9 * areaWidth * dpiScale / pageMm;
            targetScale = Math.Max(0.1, Math.Min(5.0, targetScale));
            ViewModel.PreviewScale = targetScale;
        }

        /// <summary>定时器回调：布局变化后重刷标尺</summary>
        private void SyncRulerFromPaper()
        {
            if (!_previewVisible || PreviewRulerGrid.Visibility != Visibility.Visible) return;
            UpdateRulerScale();
        }

        private void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            if (handled) return;
            if (!_previewVisible || PreviewRulerGrid.Visibility != Visibility.Visible) return;

            if (msg.message == WM_MOUSEWHEEL)
            {
                if (!IsMouseWheelInsidePreviewArea(msg.lParam)) return;
                if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;

                int wheelDelta = (short)((msg.wParam.ToInt64() >> 16) & 0xffff);
                if (wheelDelta == 0) return;

                // 平滑缩放：支持高分辨率滚轮/触控板，连续性更好
                double cur = ViewModel.PreviewScale;
                double step = wheelDelta / 120.0;
                double factor = Math.Pow(1.06, step);
                ViewModel.PreviewScale = Math.Max(0.1, Math.Min(5.0, cur * factor));
                handled = true;
            }
        }

        private bool IsMouseWheelInsidePreviewArea(IntPtr lParam)
        {
            if (PreviewRulerGrid == null || PreviewRulerGrid.Visibility != Visibility.Visible) return false;

            try
            {
                long lp = lParam.ToInt64();
                int screenX = (short)(lp & 0xFFFF);
                int screenY = (short)((lp >> 16) & 0xFFFF);
                Point screenPoint = new Point(screenX, screenY);
                Point local = PreviewRulerGrid.PointFromScreen(screenPoint);

                return local.X >= 0
                    && local.Y >= 0
                    && local.X <= PreviewRulerGrid.ActualWidth
                    && local.Y <= PreviewRulerGrid.ActualHeight;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region 大纲

        private static readonly Regex HeadingRegex = new Regex(@"^(#{1,6})\s+(.+)$", RegexOptions.Multiline);

        private void RefreshOutline()
        {
            OutlineList.Items.Clear();
            string md = ViewModel.MarkdownText;
            if (string.IsNullOrWhiteSpace(md)) return;

            foreach (Match m in HeadingRegex.Matches(md))
            {
                int level = m.Groups[1].Value.Length;
                string title = m.Groups[2].Value.Trim();
                string indent = new string(' ', (level - 1) * 2);
                var item = new ListBoxItem
                {
                    Content = indent + title,
                    Tag = title,
                    Padding = new Thickness(4 + (level - 1) * 12, 2, 4, 2),
                    FontSize = level <= 2 ? 12 : 11,
                    Foreground = level == 1
                        ? ThemeBrush("ThemeTextPrimaryBrush")
                        : level == 2
                            ? ThemeBrush("ThemeTextSecondaryBrush")
                            : ThemeBrush("ThemeTextMutedBrush"),
                };
                OutlineList.Items.Add(item);
            }
        }

        private async void OnOutlineSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OutlineList.SelectedItem is not ListBoxItem item) return;
            if (item.Tag is not string heading || string.IsNullOrWhiteSpace(heading)) return;
            if (!_editorReady || EditorWebView?.CoreWebView2 == null) return;

            try
            {
                string escaped = Newtonsoft.Json.JsonConvert.SerializeObject(heading);
                await EditorWebView.CoreWebView2.ExecuteScriptAsync($"scrollToHeading({escaped})");
            }
            catch { }
        }

        #endregion

        #region 文件树

        private void RefreshFileList()
        {
            _updatingFileList = true;
            try
            {
                FileTree.Items.Clear();
                string current = ViewModel.CurrentFilePath;
                if (string.IsNullOrWhiteSpace(current)) return;

                string rootDir = Path.GetDirectoryName(current);
                if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir)) return;

                var rootNode = BuildTreeNode(rootDir, current);
                rootNode.IsExpanded = true;
                FileTree.Items.Add(rootNode);
            }
            catch { }
            finally { _updatingFileList = false; }
        }

        private TreeViewItem BuildTreeNode(string dirPath, string currentFile)
        {
            string dirName = Path.GetFileName(dirPath);
            if (string.IsNullOrWhiteSpace(dirName)) dirName = dirPath;
            var node = new TreeViewItem
            {
                Header = "\U0001F4C1 " + dirName,
                Tag = dirPath,
                IsExpanded = false,
                Foreground = ThemeBrush("ThemeTextPrimaryBrush"),
            };

            // 子文件夹
            try
            {
                foreach (var sub in Directory.GetDirectories(dirPath)
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                {
                    if (Path.GetFileName(sub).StartsWith(".")) continue;
                    node.Items.Add(BuildTreeNode(sub, currentFile));
                }
            }
            catch { }

            // .md 文件
            try
            {
                foreach (var file in Directory.GetFiles(dirPath, "*.md", SearchOption.TopDirectoryOnly)
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                {
                    bool isCurrent = string.Equals(file, currentFile, StringComparison.OrdinalIgnoreCase);
                    node.Items.Add(new TreeViewItem
                    {
                        Header = "\U0001F4C4 " + Path.GetFileName(file),
                        Tag = file,
                        ToolTip = file,
                        Foreground = isCurrent
                            ? ThemeBrush("ThemeAccentBrush")
                            : ThemeBrush("ThemeTextPrimaryBrush"),
                        IsSelected = isCurrent,
                    });
                }
            }
            catch { }

            // 如果包含当前文件，展开到该路径
            if (!string.IsNullOrWhiteSpace(currentFile) &&
                currentFile.StartsWith(dirPath, StringComparison.OrdinalIgnoreCase))
                node.IsExpanded = true;

            return node;
        }

        private string GetSelectedTreePath()
        {
            if (FileTree.SelectedItem is TreeViewItem ti && ti.Tag is string p) return p;
            return null;
        }

        private void OnFileTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) { }

        /// <summary>双击文件节点 → 加载到编辑器</summary>
        private void OnFileTreeDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var path = GetSelectedTreePath();
            if (path != null && File.Exists(path))
                OpenMdFile(path);
        }

        private void OpenMdFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            if (string.Equals(ViewModel.CurrentFilePath, path, StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                string content = File.ReadAllText(path, System.Text.Encoding.UTF8);
                ViewModel.MarkdownText = content;
                ViewModel.CurrentFilePath = path;
                ViewModel.StatusText = $"已打开: {Path.GetFileName(path)}";
                OnEditorContentLoadRequested(content);
                RefreshPreview();
                RefreshOutline();
                RefreshFileList();
            }
            catch (Exception ex) { ViewModel.StatusText = $"打开失败: {ex.Message}"; }
        }

        #region 文件树右键菜单

        private void OnCtxOpen(object sender, RoutedEventArgs e)
        {
            var path = GetSelectedTreePath();
            if (path != null && File.Exists(path)) OpenMdFile(path);
            else if (path != null && Directory.Exists(path) && FileTree.SelectedItem is TreeViewItem ti)
                ti.IsExpanded = !ti.IsExpanded;
        }

        private void OnCtxNewFile(object sender, RoutedEventArgs e)
        {
            string dir = ResolveDir(GetSelectedTreePath());
            if (dir == null) return;
            string name = PromptInput("新建文件", "请输入文件名：", "新文档.md");
            if (string.IsNullOrWhiteSpace(name)) return;
            if (!name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) name += ".md";
            string full = Path.Combine(dir, name);
            if (File.Exists(full)) { MessageBox.Show("文件已存在。", "提示"); return; }
            File.WriteAllText(full, $"# {Path.GetFileNameWithoutExtension(name)}\n", System.Text.Encoding.UTF8);
            ViewModel.StatusText = $"已新建: {name}";
            RefreshFileList();
            OpenMdFile(full);
        }

        private void OnCtxNewFolder(object sender, RoutedEventArgs e)
        {
            string dir = ResolveDir(GetSelectedTreePath());
            if (dir == null) return;
            string name = PromptInput("新建文件夹", "请输入文件夹名：", "新文件夹");
            if (string.IsNullOrWhiteSpace(name)) return;
            string full = Path.Combine(dir, name);
            if (Directory.Exists(full)) { MessageBox.Show("文件夹已存在。", "提示"); return; }
            Directory.CreateDirectory(full);
            ViewModel.StatusText = $"已新建文件夹: {name}";
            RefreshFileList();
        }

        private void OnCtxRename(object sender, RoutedEventArgs e)
        {
            var path = GetSelectedTreePath();
            if (string.IsNullOrWhiteSpace(path)) return;
            bool isFile = File.Exists(path);
            bool isDir = Directory.Exists(path);
            if (!isFile && !isDir) return;
            string oldName = Path.GetFileName(path);
            string newName = PromptInput("重命名", "请输入新名称：", oldName);
            if (string.IsNullOrWhiteSpace(newName) || newName == oldName) return;
            string newPath = Path.Combine(Path.GetDirectoryName(path), newName);
            try
            {
                if (isFile)
                {
                    File.Move(path, newPath);
                    if (string.Equals(ViewModel.CurrentFilePath, path, StringComparison.OrdinalIgnoreCase))
                        ViewModel.CurrentFilePath = newPath;
                }
                else Directory.Move(path, newPath);
                ViewModel.StatusText = $"已重命名: {oldName} \u2192 {newName}";
                RefreshFileList();
            }
            catch (Exception ex) { MessageBox.Show($"重命名失败: {ex.Message}", "错误"); }
        }

        private void OnCtxDuplicate(object sender, RoutedEventArgs e)
        {
            var path = GetSelectedTreePath();
            if (path == null || !File.Exists(path)) return;
            string dir = Path.GetDirectoryName(path);
            string nameNoExt = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            string copyPath = Path.Combine(dir, nameNoExt + " - 副本" + ext);
            int n = 2;
            while (File.Exists(copyPath))
            {
                copyPath = Path.Combine(dir, $"{nameNoExt} - 副本{n}{ext}");
                n++;
            }
            File.Copy(path, copyPath);
            ViewModel.StatusText = $"已创建副本: {Path.GetFileName(copyPath)}";
            RefreshFileList();
        }

        private void OnCtxDelete(object sender, RoutedEventArgs e)
        {
            var path = GetSelectedTreePath();
            if (string.IsNullOrWhiteSpace(path)) return;
            bool isFile = File.Exists(path);
            bool isDir = Directory.Exists(path);
            if (!isFile && !isDir) return;
            string name = Path.GetFileName(path);
            if (MessageBox.Show($"确定删除 \"{name}\"？\n此操作不可撤销。", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try
            {
                if (isFile)
                {
                    File.Delete(path);
                    if (string.Equals(ViewModel.CurrentFilePath, path, StringComparison.OrdinalIgnoreCase))
                    {
                        ViewModel.CurrentFilePath = "";
                        ViewModel.MarkdownText = "";
                        OnEditorContentLoadRequested("");
                    }
                }
                else Directory.Delete(path, true);
                ViewModel.StatusText = $"已删除: {name}";
                RefreshFileList();
            }
            catch (Exception ex) { MessageBox.Show($"删除失败: {ex.Message}", "错误"); }
        }

        private void OnCtxCopyPath(object sender, RoutedEventArgs e)
        {
            var path = GetSelectedTreePath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                Clipboard.SetText(path);
                ViewModel.StatusText = "已复制路径";
            }
        }

        private void OnCtxOpenFolder(object sender, RoutedEventArgs e)
        {
            var path = GetSelectedTreePath();
            if (string.IsNullOrWhiteSpace(path)) return;
            string dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                System.Diagnostics.Process.Start("explorer.exe", dir);
        }

        private string ResolveDir(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (Directory.Exists(path)) return path;
            if (File.Exists(path)) return Path.GetDirectoryName(path);
            return null;
        }

        private static string PromptInput(string title, string prompt, string defaultValue)
        {
            var win = new Window
            {
                Title = title,
                Width = 360,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = BrushFromHex("#1c2128"),
            };
            var sp = new StackPanel { Margin = new Thickness(16) };
            sp.Children.Add(new TextBlock
            {
                Text = prompt,
                Foreground = BrushFromHex("#c9d1d9"),
                Margin = new Thickness(0, 0, 0, 8)
            });
            var tb = new TextBox
            {
                Text = defaultValue,
                Background = BrushFromHex("#0d1117"),
                Foreground = BrushFromHex("#c9d1d9"),
                BorderBrush = BrushFromHex("#30363d"),
                CaretBrush = BrushFromHex("#ffffff"),
                Padding = new Thickness(4, 2, 4, 2)
            };
            tb.SelectAll();
            sp.Children.Add(tb);
            var bp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var ok = new Button
            {
                Content = "确定", Width = 60, Margin = new Thickness(0, 0, 8, 0),
                Background = BrushFromHex("#0e639c"), Foreground = BrushFromHex("#ffffff"),
                BorderThickness = new Thickness(0)
            };
            var cancel = new Button
            {
                Content = "取消", Width = 60,
                Background = BrushFromHex("#30363d"), Foreground = BrushFromHex("#c9d1d9"),
                BorderThickness = new Thickness(0)
            };
            ok.Click += (_, __) => { win.DialogResult = true; };
            cancel.Click += (_, __) => { win.DialogResult = false; };
            bp.Children.Add(ok);
            bp.Children.Add(cancel);
            sp.Children.Add(bp);
            win.Content = sp;
            tb.Focus();
            return win.ShowDialog() == true ? tb.Text?.Trim() : null;
        }

        #endregion

        #endregion

        #region 预览刷新

        private void OnPropChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.PreviewScale):
                    UpdateRulerScale();
                    TryApplyLivePreviewZoom();
                    SchedulePreviewRefresh();
                    break;

                case nameof(ViewModel.ColumnCount):
                case nameof(ViewModel.ColumnGutter):
                case nameof(ViewModel.TextSize):
                case nameof(ViewModel.TextXScale):
                case nameof(ViewModel.DrawScale):
                case nameof(ViewModel.PagePreset):
                case nameof(ViewModel.IsLandscape):
                case nameof(ViewModel.PageWidthMm):
                case nameof(ViewModel.PageHeightMm):
                case "SpacingChanged":
                    RefreshPreview();
                    UpdateRulerScale();
                    break;

                case nameof(ViewModel.MarkdownText):
                    RefreshOutline();
                    break;

                case nameof(ViewModel.CurrentFilePath):
                    RefreshFileList();
                    break;
            }
        }

        private void SchedulePreviewRefresh()
        {
            _previewRefreshDebounceTimer.Stop();
            _previewRefreshDebounceTimer.Start();
        }

        private void TryApplyLivePreviewZoom()
        {
            if (!_previewVisible || PreviewBrowser == null) return;
            if (_renderedPreviewScale <= 0) return;

            double ratio = ViewModel.PreviewScale / _renderedPreviewScale;
            ratio = Math.Max(0.2, Math.Min(8.0, ratio));

            try
            {
                string ratioText = ratio.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                PreviewBrowser.InvokeScript("eval", new object[] { $"document.body.style.zoom='{ratioText}'" });
            }
            catch
            {
                // 文档尚未就绪时忽略，等待防抖后的完整刷新
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
                _renderedPreviewScale = Math.Max(0.1, ViewModel.PreviewScale);
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
