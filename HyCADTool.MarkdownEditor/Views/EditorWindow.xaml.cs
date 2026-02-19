using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
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
        private const int PreviewRefreshDebounceMs = 220;
        private const int AutoCadSyncDebounceMs = 900;
        private const bool DefaultPaperPrimaryMode = false;
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
        private readonly DispatcherTimer _autoCadSyncDebounceTimer;
        private readonly ThemeManager _themeManager = new ThemeManager();
        private readonly PreviewManager _previewManager = new PreviewManager();

        private VditorJsHelper _js;
        private bool _editorReady;
        private bool _previewVisible = true;
        private bool _outlineVisible = true;
        private bool _bottomPanelVisible = true;
        private bool _rulerVisible = true;
        private bool _editorVisible = true;
        private bool _paperPrimaryEditMode = DefaultPaperPrimaryMode;
        private bool _defaultWorkspaceLayoutApplied;
        private double _bottomPanelHeight = 160;
        private double _outlinePanelWidth = DefaultOutlinePanelWidth;
        private double _previewPanelWidth = DefaultPreviewPanelWidth;
        private double _editorPanelWidth = 0;
        private readonly TitleBarControl _titleBar;
        private readonly LeftPanelControl _leftPanel;
        private readonly PreviewPanelControl _previewPanel;
        private HwndSource _hwndSource;
        private bool _isAutoSyncRunning;
        private bool _autoSyncPending;
        private readonly bool _isModalSession;
        private bool _isUpdatingFromPreview;
        private string _pendingPreviewSyncHash = string.Empty;
        private PreviewRefreshReason _pendingPreviewRefreshReason = PreviewRefreshReason.InitialLoad;

        private enum MarkdownSyncSource
        {
            Editor = 0,
            Preview = 1
        }

        public EditorWindow(EditorInput input, bool isModal = true)
        {
            _isModalSession = isModal;
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
                var reason = _pendingPreviewRefreshReason;
                _pendingPreviewRefreshReason = PreviewRefreshReason.ContentInput;
                _ = RefreshPreviewAsync(reason);
            };
            _autoCadSyncDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(AutoCadSyncDebounceMs) };
            _autoCadSyncDebounceTimer.Tick += async (_, __) =>
            {
                _autoCadSyncDebounceTimer.Stop();
                await TriggerLiveSyncAsync();
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
            ViewModel.PreviewEditorActionRequested += OnPreviewEditorActionRequestedAsync;
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
            _titleBar.SettingsRequested += (_, __) => OnOpenSettingsPanel();
            _titleBar.InsertCadRequested += async (_, __) => await OnInsertCadClickAsync();
            _titleBar.ConfirmRequested += async (_, __) => await OnConfirmClickAsync();
            _titleBar.MinimizeRequested += (_, __) => SystemCommands.MinimizeWindow(this);
            _titleBar.MaxRestoreRequested += (_, __) => ToggleMaxRestore();
            _titleBar.CloseRequested += (_, __) => OnCloseClick();
            _titleBar.WorkspaceModeRequested += (_, e) => ApplyWorkspaceMode(e.Mode);
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
            _previewPanel.ResetLayoutRequested += async (_, __) => await OnResetLayoutRequestedAsync();
            _previewPanel.PreviousPageRequested += async (_, __) => await ShiftPreviewPageAsync(-1);
            _previewPanel.NextPageRequested += async (_, __) => await ShiftPreviewPageAsync(1);
            _previewPanel.JumpPageRequested += async (_, page) => await JumpPreviewPageAsync(page);
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
                UpdateEditorActionRouting();
                EditorWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                string html = VditorHtmlTemplate.Generate(ViewModel.MarkdownText, cdnBase);
                EditorWebView.CoreWebView2.NavigateToString(html);

                if (_previewManager.IsPreviewReady && _previewManager.HasPendingHtml)
                {
                    _previewPanel.PreviewWebViewControl.CoreWebView2.NavigateToString(_previewManager.PendingHtml);
                    _previewManager.ClearPendingHtml();
                }

                await RefreshPreviewAsync(PreviewRefreshReason.InitialLoad);
                UpdatePreviewPageState();
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
            _autoCadSyncDebounceTimer.Stop();
            SourceInitialized -= OnSourceInitialized;
            StateChanged -= OnWindowStateChanged;
            ViewModel.PropertyChanged -= OnPropChanged;
            ViewModel.EditorContentLoadRequested -= OnEditorContentLoadRequested;
            ViewModel.PreviewEditorActionRequested -= OnPreviewEditorActionRequestedAsync;
            ViewModel.SetJsHelper(null);
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

            // 关闭窗口后兜底清理回调，防止跨会话残留。
            EditorLauncher.ClearSyncCallbacks();
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
                        string markdownFromEditor = msg.Value<string>("value") ?? "";
                        string incomingHash = ComputeTextHash(markdownFromEditor);
                        if (_isUpdatingFromPreview)
                        {
                            if (!string.IsNullOrEmpty(_pendingPreviewSyncHash)
                                && string.Equals(_pendingPreviewSyncHash, incomingHash, StringComparison.Ordinal))
                            {
                                ClearPendingPreviewSyncState();
                                break;
                            }

                            // 预览回写尚未完成时用户在编辑器继续输入，后写覆盖先写。
                            ClearPendingPreviewSyncState();
                        }

                        if (ApplyMarkdownFromSource(markdownFromEditor, MarkdownSyncSource.Editor))
                        {
                            SchedulePreviewRefresh(PreviewRefreshReason.ContentInput);
                            ScheduleAutoCadSync();
                        }
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
            if (!CanUseEditorScriptPipeline()) return;
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
                if (_previewManager.TryHandlePreviewWebMessage(args.WebMessageAsJson, ViewModel, out PreviewContentChange contentChanged)
                    && contentChanged != null
                    && ApplyMarkdownFromSource(contentChanged.Markdown, MarkdownSyncSource.Preview, contentChanged.Version))
                {
                    _ = SyncEditorFromPreviewAsync(contentChanged.Markdown);
                    ScheduleAutoCadSync();
                }
                UpdatePreviewPageState();
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
                    _ = ApplyPaperGeometryAsync();
                    _ = ApplyPaperColumnLayoutAsync();
                    break;

                case nameof(EditorViewModel.ColumnCount):
                case nameof(EditorViewModel.ColumnGutter):
                    _ = ApplyPaperColumnLayoutAsync();
                    UpdateRulerScale();
                    break;

                case nameof(EditorViewModel.PagePreset):
                case nameof(EditorViewModel.IsLandscape):
                case nameof(EditorViewModel.DrawScale):
                    _ = ApplyPaperGeometryAsync();
                    UpdateRulerScale();
                    break;

                case nameof(EditorViewModel.PageWidthMm):
                case nameof(EditorViewModel.PageHeightMm):
                case nameof(EditorViewModel.MarginLeftMm):
                case nameof(EditorViewModel.MarginRightMm):
                case nameof(EditorViewModel.MarginTopMm):
                case nameof(EditorViewModel.MarginBottomMm):
                    _ = ApplyPaperGeometryAsync();
                    UpdateRulerScale();
                    break;

                case nameof(EditorViewModel.TextSize):
                case nameof(EditorViewModel.TextXScale):
                case "SpacingChanged":
                    SchedulePreviewRefresh(PreviewRefreshReason.ConfigChanged);
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

        private static int RefreshReasonPriority(PreviewRefreshReason reason)
        {
            switch (reason)
            {
                case PreviewRefreshReason.FallbackRebuild:
                    return 50;
                case PreviewRefreshReason.InitialLoad:
                    return 40;
                case PreviewRefreshReason.ConfigChanged:
                    return 30;
                case PreviewRefreshReason.ViewportChanged:
                    return 20;
                case PreviewRefreshReason.ContentInput:
                default:
                    return 10;
            }
        }

        private void SchedulePreviewRefresh(PreviewRefreshReason reason = PreviewRefreshReason.ConfigChanged)
        {
            if (RefreshReasonPriority(reason) >= RefreshReasonPriority(_pendingPreviewRefreshReason))
                _pendingPreviewRefreshReason = reason;
            _previewRefreshDebounceTimer.Stop();
            _previewRefreshDebounceTimer.Start();
        }

        private void ScheduleAutoCadSync()
        {
            _autoCadSyncDebounceTimer.Stop();
            _autoCadSyncDebounceTimer.Start();
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

        private async Task RefreshPreviewAsync(PreviewRefreshReason reason = PreviewRefreshReason.ConfigChanged)
        {
            try
            {
                await _previewManager.RefreshPreviewAsync(_previewPanel.PreviewWebViewControl, ViewModel, reason);
                UpdatePreviewPageState();
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
            if (string.IsNullOrWhiteSpace(heading)) return;
            try
            {
                string escaped = JsonConvert.SerializeObject(heading);
                if (_editorReady && EditorWebView?.CoreWebView2 != null)
                    await EditorWebView.CoreWebView2.ExecuteScriptAsync($"scrollToHeading({escaped})");

                if (_previewPanel?.PreviewWebViewControl?.CoreWebView2 != null)
                    await _previewPanel.PreviewWebViewControl.CoreWebView2.ExecuteScriptAsync($"scrollToHeading({escaped})");
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
                _ = RefreshPreviewAsync(PreviewRefreshReason.ContentInput);
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

        private void OnOpenSettingsPanel()
        {
            if (!_outlineVisible)
            {
                _outlineVisible = true;
                ApplyOutlineLayout();
                RelayoutPreviewIfNeeded();
            }

            _leftPanel.ShowSettingsTab();
            ViewModel.StatusText = "已打开设置面板";
        }

        private void OnTogglePreview()
        {
            _previewVisible = !_previewVisible;
            if (!_previewVisible && !_editorVisible)
            {
                _paperPrimaryEditMode = false;
                _editorVisible = true;
            }
            ApplyPreviewLayout();
            ApplyEditorLayout();
            if (_previewVisible)
                _ = RefreshPreviewAsync(PreviewRefreshReason.ViewportChanged);
            ViewModel.StatusText = _previewVisible
                ? $"预览：已显示（图纸主编辑：{(_paperPrimaryEditMode ? "开" : "关")}）"
                : "预览：已隐藏，已自动保留编辑面板";
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

        private async Task ShiftPreviewPageAsync(int delta)
        {
            try
            {
                await _previewManager.ShiftPageAsync(_previewPanel.PreviewWebViewControl, delta);
            }
            catch (Exception ex)
            {
                LogSilentException(nameof(ShiftPreviewPageAsync), ex);
            }
        }

        private async Task JumpPreviewPageAsync(int page)
        {
            try
            {
                await _previewManager.GoToPageAsync(_previewPanel.PreviewWebViewControl, page);
            }
            catch (Exception ex)
            {
                LogSilentException(nameof(JumpPreviewPageAsync), ex);
            }
        }

        private void UpdatePreviewPageState()
        {
            _previewPanel?.UpdatePageState(_previewManager.CurrentPage, _previewManager.TotalPages);
        }

        private void ApplyWorkspaceMode(WorkspaceMode mode)
        {
            switch (mode)
            {
                case WorkspaceMode.Writing:
                    _outlineVisible = true;
                    _previewVisible = false;
                    _bottomPanelVisible = false;
                    _rulerVisible = false;
                    _paperPrimaryEditMode = false;
                    _editorVisible = true;
                    ViewModel.StatusText = "视图：写作模式（编辑主导）";
                    break;

                case WorkspaceMode.Layout:
                    _outlineVisible = false;
                    _previewVisible = true;
                    _bottomPanelVisible = true;
                    _rulerVisible = true;
                    _paperPrimaryEditMode = true;
                    _editorVisible = false;
                    ViewModel.StatusText = "视图：排版模式（图纸主编辑）";
                    break;

                case WorkspaceMode.Proofread:
                    _outlineVisible = true;
                    _previewVisible = true;
                    _rulerVisible = false;
                    _paperPrimaryEditMode = false;
                    _editorVisible = true;
                    ViewModel.StatusText = "视图：校对模式（双面板）";
                    break;
            }

            if (!_previewVisible && !_editorVisible)
                _editorVisible = true;

            ApplyOutlineLayout();
            ApplyPreviewLayout();
            ApplyEditorLayout();
            ApplyBottomPanelLayout();
            ApplyRulerLayout();
            if (_previewVisible)
                _ = RefreshPreviewAsync(PreviewRefreshReason.ViewportChanged);
        }

        private void RelayoutPreviewIfNeeded()
        {
            if (!_previewVisible) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                EnsurePreviewColumnVisible();
                _ = RefreshPreviewAsync(PreviewRefreshReason.ViewportChanged);
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
            UpdatePreviewPageState();
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

        private void ApplyEditorLayout()
        {
            if (EditorCol == null || EditorWebView == null)
                return;

            if (_editorVisible)
            {
                EditorCol.MinWidth = DefaultEditorMinWidth;
                if (EditorCol.Width.Value <= 0)
                {
                    double target = _editorPanelWidth > 0 ? _editorPanelWidth : DefaultEditorMinWidth * 2.0;
                    EditorCol.Width = new GridLength(target, GridUnitType.Pixel);
                }
                EditorWebView.Visibility = Visibility.Visible;
            }
            else
            {
                if (EditorCol.ActualWidth > 0)
                    _editorPanelWidth = Math.Max(DefaultEditorMinWidth, EditorCol.ActualWidth);
                EditorCol.MinWidth = 0;
                EditorCol.Width = new GridLength(0, GridUnitType.Pixel);
                EditorWebView.Visibility = Visibility.Collapsed;
            }

            UpdateEditorActionRouting();
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

            _outlineVisible = false;
            _previewVisible = true;
            _bottomPanelVisible = true;
            _rulerVisible = true;
            _paperPrimaryEditMode = DefaultPaperPrimaryMode;
            _editorVisible = !_paperPrimaryEditMode;

            ApplyOutlineLayout();
            ApplyPreviewLayout();
            _editorPanelWidth = Math.Max(editorMinWidth, unit * DefaultRatioEditor);
            ApplyEditorLayout();
            if (_editorVisible)
            {
                EditorCol.MinWidth = editorMinWidth;
                if (EditorCol.Width.Value <= 0)
                    EditorCol.Width = new GridLength(1, GridUnitType.Star);
            }
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

        private async Task OnInsertCadClickAsync()
        {
            var result = await BuildEditorResultAsync(confirmed: true);
            Result = result;
            RaiseManualSync(result);
        }

        private async Task OnConfirmClickAsync()
        {
            var result = await BuildEditorResultAsync(confirmed: true);
            Result = result;
            RaiseManualSync(result);

            if (_isModalSession)
                DialogResult = true;
            Close();
        }

        private void OnCloseClick()
        {
            Result = new EditorResult { Confirmed = false };
            if (_isModalSession)
                DialogResult = false;
            Close();
        }

        private async Task TriggerLiveSyncAsync()
        {
            if (_isAutoSyncRunning)
            {
                _autoSyncPending = true;
                return;
            }

            _isAutoSyncRunning = true;
            try
            {
                var result = await BuildEditorResultAsync(confirmed: true);
                Result = result;
                RaiseLiveSync(result);
            }
            finally
            {
                _isAutoSyncRunning = false;
            }

            if (_autoSyncPending)
            {
                _autoSyncPending = false;
                ScheduleAutoCadSync();
            }
        }

        private async Task<EditorResult> BuildEditorResultAsync(bool confirmed)
        {
            await SyncMarkdownFromEditorAsync();
            await RefreshPreviewAsync(PreviewRefreshReason.ContentInput);
            await Task.Delay(200);
            await SyncFromPreviewAsync();

            return new EditorResult
            {
                Confirmed = confirmed,
                Markdown = ViewModel.MarkdownText,
                Config = ViewModel.BuildConfig(),
                CharsPerColumn = ViewModel.CharsPerColumn,
                ColumnParagraphIndices = ViewModel.ColumnParagraphIndices,
                PreviewStats = _previewManager.LatestPreviewStats,
                LayoutResult = _previewManager.LatestLayoutResult
            };
        }

        private static void RaiseManualSync(EditorResult result)
        {
            try
            {
                EditorLauncher.RaiseManualSync(JsonConvert.SerializeObject(result ?? new EditorResult()));
            }
            catch { }
        }

        private static void RaiseLiveSync(EditorResult result)
        {
            try
            {
                EditorLauncher.RaiseLiveSync(JsonConvert.SerializeObject(result ?? new EditorResult()));
            }
            catch { }
        }

        private async Task SyncMarkdownFromEditorAsync()
        {
            if (!CanUseEditorScriptPipeline()) return;
            // 预览正在回写编辑器时，避免用旧编辑器内容覆盖预览最新内容。
            if (_isUpdatingFromPreview) return;
            try
            {
                string result = await EditorWebView.CoreWebView2.ExecuteScriptAsync("getContent()");
                if (!string.IsNullOrEmpty(result) && result != "null")
                {
                    string md = JsonConvert.DeserializeObject<string>(result);
                    if (md != null)
                        ApplyMarkdownFromSource(md, MarkdownSyncSource.Editor);
                }
            }
            catch (Exception ex)
            {
                LogSilentException(nameof(SyncMarkdownFromEditorAsync), ex);
            }
        }

        private async Task SyncEditorFromPreviewAsync(string markdown)
        {
            if (!CanUseEditorScriptPipeline())
            {
                ClearPendingPreviewSyncState();
                return;
            }
            try
            {
                _isUpdatingFromPreview = true;
                _pendingPreviewSyncHash = ComputeTextHash(markdown ?? "");
                string escaped = JsonConvert.SerializeObject(markdown ?? "");
                await EditorWebView.CoreWebView2.ExecuteScriptAsync($"setContent({escaped})");
                RefreshOutline();
                string expectedHash = _pendingPreviewSyncHash;
                _ = Task.Delay(1200).ContinueWith(_ =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_isUpdatingFromPreview && string.Equals(_pendingPreviewSyncHash, expectedHash, StringComparison.Ordinal))
                        {
                            ClearPendingPreviewSyncState();
                        }
                    }));
                });
            }
            catch (Exception ex)
            {
                ClearPendingPreviewSyncState();
                LogSilentException(nameof(SyncEditorFromPreviewAsync), ex);
            }
        }

        private async Task<bool> OnPreviewEditorActionRequestedAsync(string action)
        {
            if (string.IsNullOrWhiteSpace(action)) return false;
            if (!_paperPrimaryEditMode || !_previewVisible) return false;
            if (_previewPanel?.PreviewWebViewControl?.CoreWebView2 == null) return false;

            try
            {
                string escaped = JsonConvert.SerializeObject(action);
                string raw = await _previewPanel.PreviewWebViewControl.CoreWebView2.ExecuteScriptAsync($"applyEditorAction({escaped})");
                bool handled = ParseJsBoolean(raw);
                if (!handled)
                    return false;

                await SyncFromPreviewAsync();
                RefreshOutline();
                ScheduleAutoCadSync();
                return true;
            }
            catch (Exception ex)
            {
                LogSilentException(nameof(OnPreviewEditorActionRequestedAsync), ex);
                return false;
            }
        }

        private static bool ParseJsBoolean(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw == "null")
                return false;

            string payload = raw.Trim();
            if (payload.StartsWith("\"", StringComparison.Ordinal))
            {
                payload = JsonConvert.DeserializeObject<string>(payload) ?? string.Empty;
            }

            return bool.TryParse(payload, out bool value) && value;
        }

        private static string ComputeTextHash(string text)
        {
            string safe = text ?? string.Empty;
            using (var sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(safe);
                byte[] hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        private void ClearPendingPreviewSyncState()
        {
            _isUpdatingFromPreview = false;
            _pendingPreviewSyncHash = string.Empty;
        }

        private bool ApplyMarkdownFromSource(
            string markdown,
            MarkdownSyncSource source,
            long previewVersion = 0)
        {
            string next = markdown ?? string.Empty;
            _ = source;
            _ = previewVersion;

            if (string.Equals(ViewModel.MarkdownText ?? string.Empty, next, StringComparison.Ordinal))
                return false;

            ViewModel.SetMarkdownFromEditor(next);
            return true;
        }

        private bool CanUseEditorScriptPipeline()
        {
            if (!_editorVisible) return false;
            if (_paperPrimaryEditMode) return false;
            if (!_editorReady) return false;
            return EditorWebView?.CoreWebView2 != null;
        }

        private async Task ApplyPaperColumnLayoutAsync()
        {
            if (_previewPanel?.PreviewWebViewControl?.CoreWebView2 == null) return;

            try
            {
                int columnCount = Math.Max(1, Math.Min(10, ViewModel.ColumnCount));
                double gapPx = ComputePaperColumnGapPx(ViewModel.ColumnGutter, ViewModel.PreviewScale);
                string countJson = JsonConvert.SerializeObject(columnCount);
                string gapJson = JsonConvert.SerializeObject(Math.Round(gapPx, 2));
                await _previewPanel.PreviewWebViewControl.CoreWebView2.ExecuteScriptAsync($"setPaperColumnLayout({countJson}, {gapJson})");
            }
            catch (Exception ex)
            {
                LogSilentException(nameof(ApplyPaperColumnLayoutAsync), ex);
            }
        }

        private static double ComputePaperColumnGapPx(double columnGutter, double previewScale)
        {
            double safeGutter = Math.Max(0, columnGutter);
            double safeScale = Math.Max(0.1, previewScale);
            double px = safeGutter * safeScale;
            return Math.Max(6, Math.Min(240, px));
        }

        private async Task ApplyPaperGeometryAsync()
        {
            if (_previewPanel?.PreviewWebViewControl?.CoreWebView2 == null) return;

            try
            {
                string widthMmJson = JsonConvert.SerializeObject(Math.Round(ViewModel.PageWidthMm, 3));
                string heightMmJson = JsonConvert.SerializeObject(Math.Round(ViewModel.PageHeightMm, 3));
                string leftMmJson = JsonConvert.SerializeObject(Math.Round(ViewModel.MarginLeftMm, 3));
                string rightMmJson = JsonConvert.SerializeObject(Math.Round(ViewModel.MarginRightMm, 3));
                string topMmJson = JsonConvert.SerializeObject(Math.Round(ViewModel.MarginTopMm, 3));
                string bottomMmJson = JsonConvert.SerializeObject(Math.Round(ViewModel.MarginBottomMm, 3));

                await _previewPanel.PreviewWebViewControl.CoreWebView2.ExecuteScriptAsync($"setPaperGeometry({widthMmJson}, {heightMmJson})");
                await _previewPanel.PreviewWebViewControl.CoreWebView2.ExecuteScriptAsync($"setPaperMargins({leftMmJson}, {rightMmJson}, {topMmJson}, {bottomMmJson})");
            }
            catch (Exception ex)
            {
                LogSilentException(nameof(ApplyPaperGeometryAsync), ex);
            }
        }

        private async Task ResetPaperLayoutAsync()
        {
            if (_previewPanel?.PreviewWebViewControl?.CoreWebView2 == null) return;
            try
            {
                await _previewPanel.PreviewWebViewControl.CoreWebView2.ExecuteScriptAsync("if(window.resetPaperLayout){resetPaperLayout();}");
            }
            catch (Exception ex)
            {
                LogSilentException(nameof(ResetPaperLayoutAsync), ex);
            }
        }

        private async Task OnResetLayoutRequestedAsync()
        {
            try
            {
                ViewModel.ResetLayoutDefaults();
                await ApplyPaperGeometryAsync();
                await ApplyPaperColumnLayoutAsync();
                await ResetPaperLayoutAsync();
                SchedulePreviewRefresh(PreviewRefreshReason.ConfigChanged);
                ScheduleAutoCadSync();
            }
            catch (Exception ex)
            {
                LogSilentException(nameof(OnResetLayoutRequestedAsync), ex);
            }
        }

        private void UpdateEditorActionRouting()
        {
            if (_paperPrimaryEditMode || !_editorVisible)
            {
                ViewModel.SetJsHelper(null);
                return;
            }

            ViewModel.SetJsHelper(_js);
        }
    }
}
