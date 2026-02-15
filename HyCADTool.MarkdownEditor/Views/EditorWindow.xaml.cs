using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media;
using HyCADTool.MarkdownEditor.Html;
using HyCADTool.MarkdownEditor.Models;
using HyCADTool.MarkdownEditor.Services;
using HyCADTool.MarkdownEditor.ViewModels;
using HyCADTool.MarkdownEditor.Views.Controls;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HyCADTool.MarkdownEditor.Views
{
    public partial class EditorWindow : Window
    {
        public EditorViewModel ViewModel { get; }
        public EditorResult Result { get; private set; }

        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_GETMINMAXINFO = 0x0024;
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        private const int PreviewRefreshDebounceMs = 180;
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

        private readonly DispatcherTimer _rulerSyncTimer;
        private readonly DispatcherTimer _previewRefreshDebounceTimer;
        private readonly ThemeManager _themeManager = new ThemeManager();
        private readonly PreviewManager _previewManager = new PreviewManager();

        private VditorJsHelper _js;
        private bool _editorReady;
        private bool _previewVisible = true;
        private bool _outlineVisible = true;
        private bool _bottomPanelVisible = true;
        private bool _rulerVisible = true;
        private bool _defaultWorkspaceLayoutApplied;
        private double _bottomPanelHeight = 160;
        private double _outlinePanelWidth = DefaultOutlinePanelWidth;
        private double _previewPanelWidth = DefaultPreviewPanelWidth;
        private readonly TitleBarControl _titleBar;
        private readonly LeftPanelControl _leftPanel;
        private readonly PreviewPanelControl _previewPanel;
        private HwndSource _hwndSource;

        public EditorWindow(EditorInput input)
        {
            ViewModel = new EditorViewModel(input);
            DataContext = ViewModel;
            InitializeComponent();
            _titleBar = ResolveRequiredControl<TitleBarControl>("TitleBar");
            _leftPanel = ResolveRequiredControl<LeftPanelControl>("LeftPanel");
            _previewPanel = ResolveRequiredControl<PreviewPanelControl>("PreviewPanel");

            _rulerSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _rulerSyncTimer.Tick += (_, __) => SyncRulerFromPaper();
            _previewRefreshDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PreviewRefreshDebounceMs) };
            _previewRefreshDebounceTimer.Tick += (_, __) =>
            {
                _previewRefreshDebounceTimer.Stop();
                _ = RefreshPreviewAsync();
            };

            WireChildControlEvents();
            ApplyTheme(true);
            ApplyOutlineLayout();
            ApplyPreviewLayout();
            ApplyBottomPanelLayout();
            UpdateWindowCaptionButtons();

            Loaded += OnLoaded;
            Closed += OnClosed;
            SourceInitialized += OnSourceInitialized;
            StateChanged += OnWindowStateChanged;
            ViewModel.PropertyChanged += OnPropChanged;
            ViewModel.EditorContentLoadRequested += OnEditorContentLoadRequested;
        }

        private T ResolveRequiredControl<T>(string name) where T : class
        {
            return FindName(name) as T
                ?? throw new InvalidOperationException($"无法找到控件：{name}");
        }

        private static void LogSilentException(string context, Exception ex)
        {
            Debug.WriteLine($"[MarkdownEditor][{context}] {ex.Message}");
        }

        private void WireChildControlEvents()
        {
            _titleBar.ThemeDarkRequested += (_, __) => OnThemeDark();
            _titleBar.ThemeLightRequested += (_, __) => OnThemeLight();
            _titleBar.ToggleRulerRequested += (_, __) => OnToggleRuler();
            _titleBar.ToggleOutlineRequested += (_, __) => OnToggleOutline();
            _titleBar.ToggleBottomPanelRequested += (_, __) => OnToggleBottomPanel();
            _titleBar.TogglePreviewRequested += (_, __) => OnTogglePreview();
            _titleBar.ConfirmRequested += async (_, __) => await OnConfirmClickAsync();
            _titleBar.MinimizeRequested += (_, __) => SystemCommands.MinimizeWindow(this);
            _titleBar.MaxRestoreRequested += (_, __) => ToggleMaxRestore();
            _titleBar.CloseRequested += (_, __) => OnCloseClick();
            _titleBar.DragMoveRequested += (_, __) =>
            {
                try { DragMove(); }
                catch (Exception ex) { LogSilentException(nameof(DragMove), ex); }
            };
            _titleBar.SystemMenuRequested += (_, e) => SystemCommands.ShowSystemMenu(this, e.ScreenPoint);

            _leftPanel.FileOpenRequested += (_, path) => OpenMdFile(path);
            _leftPanel.OutlineHeadingSelected += async (_, heading) => await ScrollToHeadingAsync(heading);
            _leftPanel.StatusChanged += (_, status) => ViewModel.StatusText = status ?? "";
            _leftPanel.CurrentFilePathChanged += (_, e) => ViewModel.CurrentFilePath = e.NewPath;
            _leftPanel.CurrentFileClearedRequested += (_, __) =>
            {
                ViewModel.CurrentFilePath = "";
                ViewModel.MarkdownText = "";
                OnEditorContentLoadRequested("");
            };

            _previewPanel.TogglePageOrientationRequested += (_, __) => OnTogglePageOrientation();
        }

        private Brush ThemeBrush(string key) => _themeManager.GetBrush(this, key);

        private void ApplyTheme(bool dark)
        {
            _themeManager.ApplyTheme(Resources, dark);
            _leftPanel.SetThemeMode(dark);
            UpdateTitleBarToggleState();
        }

        private void OnThemeDark()
        {
            ApplyTheme(true);
            ViewModel.StatusText = "主题：黑色为主";
        }

        private void OnThemeLight()
        {
            ApplyTheme(false);
            ViewModel.StatusText = "主题：白色为主";
        }

        private void UpdateTitleBarToggleState()
        {
            _titleBar.UpdateToggleState(
                _rulerVisible,
                _outlineVisible,
                _bottomPanelVisible,
                _previewVisible,
                ThemeBrush("ThemeTextPrimaryBrush"),
                ThemeBrush("ThemeTextSecondaryBrush"));
        }

        private void ToggleMaxRestore()
        {
            if (ResizeMode == ResizeMode.NoResize || ResizeMode == ResizeMode.CanMinimize) return;
            if (WindowState == WindowState.Maximized)
                SystemCommands.RestoreWindow(this);
            else
                SystemCommands.MaximizeWindow(this);
        }

        private void UpdateWindowCaptionButtons()
        {
            _titleBar.UpdateWindowState(WindowState == WindowState.Maximized);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;
            try
            {
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
                await _previewPanel.PreviewWebViewControl.EnsureCoreWebView2Async(env);

                _previewManager.IsPreviewReady = _previewPanel.PreviewWebViewControl.CoreWebView2 != null;
                if (_previewManager.IsPreviewReady)
                {
                    _previewPanel.PreviewWebViewControl.CoreWebView2.Settings.IsZoomControlEnabled = false;
                    _previewPanel.PreviewWebViewControl.CoreWebView2.Settings.IsPinchZoomEnabled = false;
                    _previewPanel.PreviewWebViewControl.CoreWebView2.WebMessageReceived += OnPreviewWebMessageReceived;
                }

                string cdnBase = null;
                if (VditorCacheManager.IsCached)
                {
                    EditorWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        "vditor.local", VditorCacheManager.CacheDir,
                        CoreWebView2HostResourceAccessKind.Allow);
                    cdnBase = "https://vditor.local";
                }

                _js = new VditorJsHelper(script => EditorWebView.CoreWebView2.ExecuteScriptAsync(script));
                ViewModel.SetJsHelper(_js);
                EditorWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                string html = VditorHtmlTemplate.Generate(ViewModel.MarkdownText, cdnBase);
                EditorWebView.CoreWebView2.NavigateToString(html);

                if (_previewManager.IsPreviewReady && _previewManager.HasPendingHtml)
                {
                    _previewPanel.PreviewWebViewControl.CoreWebView2.NavigateToString(_previewManager.PendingHtml);
                    _previewManager.ClearPendingHtml();
                }

                await RefreshPreviewAsync();
                RefreshOutline();
                RefreshFileList();
                ApplyDefaultWorkspaceLayout(false);

                _ = Dispatcher.BeginInvoke(new Action(() =>
                {
                    FitPaperToPreviewArea();
                    UpdateRulerScale();
                }), DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Vditor 编辑器加载失败：{ex.Message}\n\n请确认已安装 WebView2 Runtime。",
                    "编辑器初始化错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                LogSilentException(nameof(OnLoaded), ex);
            }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            ComponentDispatcher.ThreadPreprocessMessage -= OnThreadPreprocessMessage;
            _rulerSyncTimer.Stop();
            _previewRefreshDebounceTimer.Stop();
            SourceInitialized -= OnSourceInitialized;
            StateChanged -= OnWindowStateChanged;
            ViewModel.PropertyChanged -= OnPropChanged;
            ViewModel.EditorContentLoadRequested -= OnEditorContentLoadRequested;
            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }
            try
            {
                if (EditorWebView?.CoreWebView2 != null)
                    EditorWebView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                if (_previewPanel?.PreviewWebViewControl?.CoreWebView2 != null)
                    _previewPanel.PreviewWebViewControl.CoreWebView2.WebMessageReceived -= OnPreviewWebMessageReceived;
            }
            catch (Exception ex)
            {
                LogSilentException(nameof(OnClosed), ex);
            }

            try { EditorWebView?.Dispose(); } catch (Exception ex) { LogSilentException(nameof(OnClosed), ex); }
            try { _previewPanel?.PreviewWebViewControl?.Dispose(); } catch (Exception ex) { LogSilentException(nameof(OnClosed), ex); }
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            _hwndSource?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero) return;

            var monitorInfo = new MONITORINFO();
            if (!GetMonitorInfo(monitor, monitorInfo)) return;

            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            RECT workArea = monitorInfo.rcWork;
            RECT monitorArea = monitorInfo.rcMonitor;

            mmi.ptMaxPosition.X = Math.Abs(workArea.Left - monitorArea.Left);
            mmi.ptMaxPosition.Y = Math.Abs(workArea.Top - monitorArea.Top);
            mmi.ptMaxSize.X = Math.Abs(workArea.Right - workArea.Left);
            mmi.ptMaxSize.Y = Math.Abs(workArea.Bottom - workArea.Top);

            Marshal.StructureToPtr(mmi, lParam, true);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MONITORINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            public RECT rcMonitor = default;
            public RECT rcWork = default;
            public int dwFlags = 0;
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
                        ViewModel.SetMarkdownFromEditor(msg.Value<string>("value") ?? "");
                        SchedulePreviewRefresh();
                        break;
                }
            }
            catch (Exception ex)
            {
                LogSilentException(nameof(OnWebMessageReceived), ex);
            }
        }

        private async void OnEditorContentLoadRequested(string markdown)
        {
            if (!_editorReady || EditorWebView?.CoreWebView2 == null) return;
            try
            {
                string escaped = JsonConvert.SerializeObject(markdown ?? "");
                await EditorWebView.CoreWebView2.ExecuteScriptAsync($"setContent({escaped})");
            }
            catch (Exception ex)
            {
                LogSilentException(nameof(OnEditorContentLoadRequested), ex);
            }
        }

        private void OnPreviewWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                _previewManager.TryHandlePreviewWebMessage(args.WebMessageAsJson, ViewModel);
            }
            catch (Exception ex)
            {
                LogSilentException(nameof(OnPreviewWebMessageReceived), ex);
            }
        }

        private void OnPropChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(EditorViewModel.PreviewScale):
                    UpdateRulerScale();
                    _ = TryApplyLivePreviewZoomAsync();
                    break;

                case nameof(EditorViewModel.ColumnCount):
                case nameof(EditorViewModel.ColumnGutter):
                case nameof(EditorViewModel.TextSize):
                case nameof(EditorViewModel.TextXScale):
                case nameof(EditorViewModel.DrawScale):
                case nameof(EditorViewModel.PagePreset):
                case nameof(EditorViewModel.IsLandscape):
                case nameof(EditorViewModel.PageWidthMm):
                case nameof(EditorViewModel.PageHeightMm):
                case "SpacingChanged":
                    SchedulePreviewRefresh();
                    UpdateRulerScale();
                    break;

                case nameof(EditorViewModel.MarkdownText):
                    RefreshOutline();
                    break;

                case nameof(EditorViewModel.CurrentFilePath):
                    RefreshFileList();
                    break;
            }
        }

        private void SchedulePreviewRefresh()
        {
            _previewRefreshDebounceTimer.Stop();
            _previewRefreshDebounceTimer.Start();
        }

        private async Task TryApplyLivePreviewZoomAsync()
        {
            try
            {
                await _previewManager.TryApplyLivePreviewZoomAsync(_previewPanel.PreviewWebViewControl, ViewModel, _previewVisible);
            }
            catch
            {
                // 文档尚未就绪时忽略，等待防抖后的完整刷新
            }
        }

        private async Task RefreshPreviewAsync()
        {
            try
            {
                await _previewManager.RefreshPreviewAsync(_previewPanel.PreviewWebViewControl, ViewModel);
            }
            catch (Exception ex)
            {
                LogSilentException(nameof(RefreshPreviewAsync), ex);
            }
        }

        private async Task SyncFromPreviewAsync()
        {
            try
            {
                await _previewManager.SyncFromPreviewAsync(_previewPanel.PreviewWebViewControl, ViewModel);
            }
            catch (Exception ex)
            {
                LogSilentException(nameof(SyncFromPreviewAsync), ex);
            }
        }

        private void RefreshOutline()
        {
            _leftPanel.RefreshOutline(ViewModel.MarkdownText);
        }

        private void RefreshFileList()
        {
            _leftPanel.RefreshFileList(ViewModel.CurrentFilePath);
        }

        private async Task ScrollToHeadingAsync(string heading)
        {
            if (string.IsNullOrWhiteSpace(heading) || !_editorReady || EditorWebView?.CoreWebView2 == null) return;
            try
            {
                string escaped = JsonConvert.SerializeObject(heading);
                await EditorWebView.CoreWebView2.ExecuteScriptAsync($"scrollToHeading({escaped})");
            }
            catch (Exception ex)
            {
                LogSilentException(nameof(ScrollToHeadingAsync), ex);
            }
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
                _ = RefreshPreviewAsync();
                RefreshOutline();
                RefreshFileList();
            }
            catch (Exception ex)
            {
                ViewModel.StatusText = $"打开失败: {ex.Message}";
            }
        }

        private void OnToggleOutline()
        {
            _outlineVisible = !_outlineVisible;
            ApplyOutlineLayout();
            RelayoutPreviewIfNeeded();
        }

        private void OnToggleBottomPanel()
        {
            _bottomPanelVisible = !_bottomPanelVisible;
            ApplyBottomPanelLayout();
            ViewModel.StatusText = _bottomPanelVisible ? "底部面板：已打开" : "底部面板：关闭";
            UpdateTitleBarToggleState();
        }

        private void OnTogglePreview()
        {
            _previewVisible = !_previewVisible;
            ApplyPreviewLayout();
            if (_previewVisible)
                _ = RefreshPreviewAsync();
        }

        private void OnToggleRuler()
        {
            _rulerVisible = !_rulerVisible;
            ApplyRulerLayout();
            ViewModel.StatusText = _rulerVisible ? "标线：已显示" : "标线：已隐藏";
            UpdateTitleBarToggleState();
        }

        private void OnTogglePageOrientation()
        {
            ViewModel.IsLandscape = !ViewModel.IsLandscape;
            ViewModel.StatusText = $"图纸方向：{ViewModel.PageOrientationLabel}";
        }

        private void RelayoutPreviewIfNeeded()
        {
            if (!_previewVisible) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                EnsurePreviewColumnVisible();
                _ = RefreshPreviewAsync();
                SyncRulerFromPaper();
            }), DispatcherPriority.Background);
        }

        private void EnsurePreviewColumnVisible()
        {
            if (!_previewVisible || PreviewCol == null) return;

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
            if (OutlineCol == null || OutlineSplitterCol == null || OutlineSplitter == null) return;

            if (_outlineVisible)
            {
                OutlineCol.Width = new GridLength(Math.Max(MinOutlinePanelWidth, _outlinePanelWidth), GridUnitType.Pixel);
                OutlineSplitterCol.Width = new GridLength(OutlineSplitterWidth, GridUnitType.Pixel);
                OutlineSplitter.Visibility = Visibility.Visible;
            }
            else
            {
                if (OutlineCol.ActualWidth > 0)
                    _outlinePanelWidth = Math.Max(MinOutlinePanelWidth, OutlineCol.ActualWidth);
                OutlineCol.Width = new GridLength(0, GridUnitType.Pixel);
                OutlineSplitterCol.Width = new GridLength(0, GridUnitType.Pixel);
                OutlineSplitter.Visibility = Visibility.Collapsed;
            }

            UpdateTitleBarToggleState();
        }

        private void ApplyPreviewLayout()
        {
            if (SplitterCol == null || PreviewCol == null || PreviewSplitter == null || _previewPanel == null)
                return;

            if (_previewVisible)
            {
                SplitterCol.Width = new GridLength(4, GridUnitType.Pixel);
                PreviewCol.MinWidth = MinPreviewVisibleWidth;
                PreviewCol.Width = new GridLength(Math.Max(MinPreviewVisibleWidth, _previewPanelWidth), GridUnitType.Pixel);
                PreviewSplitter.Visibility = Visibility.Visible;
                _previewPanel.Visibility = Visibility.Visible;
                _rulerSyncTimer.Start();
                EnsurePreviewColumnVisible();
                ApplyRulerLayout();
                ApplyBottomPanelLayout();
            }
            else
            {
                if (PreviewCol.ActualWidth > 0)
                    _previewPanelWidth = Math.Max(MinPreviewVisibleWidth, PreviewCol.ActualWidth);
                SplitterCol.Width = new GridLength(0);
                PreviewCol.MinWidth = 0;
                PreviewCol.Width = new GridLength(0);
                PreviewSplitter.Visibility = Visibility.Collapsed;
                _previewPanel.Visibility = Visibility.Collapsed;
                _rulerSyncTimer.Stop();
                ApplyRulerLayout();
                ApplyBottomPanelLayout();
            }

            UpdateTitleBarToggleState();
        }

        private void ApplyRulerLayout()
        {
            if (_previewPanel?.RulerRowDefinition == null || _previewPanel.RulerColumnDefinition == null) return;

            bool show = _previewVisible && _rulerVisible;
            _previewPanel.RulerRowDefinition.Height = show
                ? new GridLength(RulerThickness, GridUnitType.Pixel)
                : new GridLength(0, GridUnitType.Pixel);
            _previewPanel.RulerColumnDefinition.Width = show
                ? new GridLength(RulerThickness, GridUnitType.Pixel)
                : new GridLength(0, GridUnitType.Pixel);

            var rulerVisibility = show ? Visibility.Visible : Visibility.Collapsed;
            _previewPanel.RulerCornerElement.Visibility = rulerVisibility;
            _previewPanel.HorizontalRuler.Visibility = rulerVisibility;
            _previewPanel.VerticalRuler.Visibility = rulerVisibility;
            UpdateTitleBarToggleState();
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

        private void UpdateRulerScale()
        {
            _previewManager.UpdateRulerScale(_previewPanel, ViewModel);
        }

        private void FitPaperToPreviewArea()
        {
            _previewManager.FitPaperToPreviewArea(_previewPanel, ViewModel, _previewVisible);
        }

        private void SyncRulerFromPaper()
        {
            if (!_previewVisible || _previewPanel.Visibility != Visibility.Visible) return;
            UpdateRulerScale();
        }

        private void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            if (handled || !_previewVisible || _previewPanel.Visibility != Visibility.Visible) return;
            if (msg.message != WM_MOUSEWHEEL) return;
            if (!IsMouseWheelInsidePreviewArea(msg.lParam)) return;
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            if (_previewPanel.PreviewWebViewControl?.IsMouseOver == true) return;

            int wheelDelta = (short)((msg.wParam.ToInt64() >> 16) & 0xffff);
            if (wheelDelta == 0) return;
            _previewManager.ApplyPreviewScaleStep(ViewModel, wheelDelta / 120.0);
            handled = true;
        }

        private bool IsMouseWheelInsidePreviewArea(IntPtr lParam)
        {
            if (_previewPanel == null || _previewPanel.Visibility != Visibility.Visible) return false;
            try
            {
                long lp = lParam.ToInt64();
                int screenX = (short)(lp & 0xFFFF);
                int screenY = (short)((lp >> 16) & 0xFFFF);
                Point local = _previewPanel.PreviewGrid.PointFromScreen(new Point(screenX, screenY));
                return local.X >= 0
                    && local.Y >= 0
                    && local.X <= _previewPanel.PreviewGrid.ActualWidth
                    && local.Y <= _previewPanel.PreviewGrid.ActualHeight;
            }
            catch
            {
                return false;
            }
        }

        private void OnOutlineSplitterDragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (OutlineCol?.ActualWidth > 0)
                _outlinePanelWidth = Math.Max(MinOutlinePanelWidth, OutlineCol.ActualWidth);
        }

        private void OnPreviewSplitterDragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (PreviewCol?.ActualWidth > 0)
            {
                _previewPanelWidth = Math.Max(MinPreviewVisibleWidth, PreviewCol.ActualWidth);
                PreviewCol.Width = new GridLength(_previewPanelWidth, GridUnitType.Pixel);
            }
        }

        private void OnBottomPanelSplitterDragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (BottomPanelRow.ActualHeight > 0)
                _bottomPanelHeight = BottomPanelRow.ActualHeight;
        }

        private async Task OnConfirmClickAsync()
        {
            await SyncMarkdownFromEditorAsync();
            await RefreshPreviewAsync();
            await Task.Delay(300);
            await SyncFromPreviewAsync();

            Result = new EditorResult
            {
                Confirmed = true,
                Markdown = ViewModel.MarkdownText,
                Config = ViewModel.BuildConfig(),
                CharsPerColumn = ViewModel.CharsPerColumn,
                ColumnParagraphIndices = ViewModel.ColumnParagraphIndices,
                PreviewStats = _previewManager.LatestPreviewStats
            };

            DialogResult = true;
            Close();
        }

        private void OnCloseClick()
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
                    string md = JsonConvert.DeserializeObject<string>(result);
                    if (md != null)
                        ViewModel.SetMarkdownFromEditor(md);
                }
            }
            catch (Exception ex)
            {
                LogSilentException(nameof(SyncMarkdownFromEditorAsync), ex);
            }
        }
    }
}
