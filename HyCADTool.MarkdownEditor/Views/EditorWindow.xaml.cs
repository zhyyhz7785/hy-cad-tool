using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
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
using HyCADTool.MarkdownEditor.Views.Helpers;
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
        private const int PreviewRefreshDebounceMs = 220;
        private const int AutoCadSyncDebounceMs = 900;

        private readonly DispatcherTimer _rulerSyncTimer;
        private readonly DispatcherTimer _previewRefreshDebounceTimer;
        private readonly DispatcherTimer _autoCadSyncDebounceTimer;
        private readonly ThemeManager _themeManager = new ThemeManager();
        private readonly PreviewManager _previewManager = new PreviewManager();
        private readonly WorkspaceLayoutManager _layoutManager;
        private readonly CadSyncService _cadSyncService;
        private readonly MarkdownSyncCoordinator _syncCoordinator;
        private readonly EditorSessionLifecycle _sessionLifecycle;
        private readonly PreviewInteractionFacade _previewInteractions;
        private readonly EditorUiEventRouter _eventRouter;

        private VditorJsHelper _js;
        private bool _editorReady;
        private readonly TitleBarControl _titleBar;
        private readonly LeftPanelControl _leftPanel;
        private readonly PreviewPanelControl _previewPanel;
        private HwndSource _hwndSource;
        private readonly bool _isModalSession;
        private PreviewRefreshReason _pendingPreviewRefreshReason = PreviewRefreshReason.InitialLoad;

        public EditorWindow(EditorInput input, bool isModal = true)
        {
            _isModalSession = isModal;
            ViewModel = new EditorViewModel(input);
            DataContext = ViewModel;
            InitializeComponent();
            _titleBar = ResolveRequiredControl<TitleBarControl>("TitleBar");
            _leftPanel = ResolveRequiredControl<LeftPanelControl>("LeftPanel");
            _previewPanel = ResolveRequiredControl<PreviewPanelControl>("PreviewPanel");
            _cadSyncService = new CadSyncService(ViewModel, _previewManager);
            _syncCoordinator = new MarkdownSyncCoordinator(ViewModel);
            _layoutManager = new WorkspaceLayoutManager(
                new LayoutTargets(
                    OutlineCol,
                    OutlineSplitterCol,
                    EditorCol,
                    SplitterCol,
                    PreviewCol,
                    BottomPanelSplitterRow,
                    BottomPanelRow,
                    OutlineSplitter,
                    PreviewSplitter,
                    BottomPanelSplitter,
                    EditorWebView,
                    BottomPanelHost,
                    _previewPanel),
                UpdateTitleBarToggleState,
                UpdatePreviewPageState,
                running =>
                {
                    if (running) _rulerSyncTimer.Start();
                    else _rulerSyncTimer.Stop();
                });
            _previewInteractions = new PreviewInteractionFacade(
                _previewManager,
                _previewPanel,
                ViewModel,
                () => _layoutManager.IsPreviewVisible,
                () => _layoutManager.IsPaperPrimaryEditMode,
                LogSilentException);
            _eventRouter = new EditorUiEventRouter(_titleBar, _leftPanel, _previewPanel);
            _sessionLifecycle = new EditorSessionLifecycle(
                ViewModel,
                _previewManager,
                EditorWebView,
                _previewPanel.PreviewWebViewControl,
                Dispatcher,
                LogSilentException,
                UpdateEditorActionRouting,
                RefreshPreviewAsync,
                UpdatePreviewPageState,
                RefreshOutline,
                RefreshFileList,
                ApplyDefaultWorkspaceLayout,
                FitPaperToPreviewArea,
                UpdateRulerScale,
                OnPreviewWebMessageReceived,
                OnPreviewNavigationCompleted,
                OnWebMessageReceived,
                EditorLauncher.ClearSyncCallbacks);

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

            EditorWebView.GotFocus += (_, __) => _syncCoordinator.ClaimOwnership(ContentOwner.CSharp);
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
            // #region agent log
            AgentDebugLog("pre-fix", "H6", "EditorWindow:LogSilentException", "exception captured", new
            {
                context,
                message = ex?.Message ?? string.Empty
            });
            // #endregion
        }

        // #region agent log
        private static void AgentDebugLog(string runId, string hypothesisId, string location, string message, object data = null)
        {
            try
            {
                var payload = new JObject
                {
                    ["sessionId"] = "1db17a",
                    ["runId"] = runId,
                    ["hypothesisId"] = hypothesisId,
                    ["location"] = location,
                    ["message"] = message,
                    ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ["data"] = data == null ? new JObject() : JObject.FromObject(data)
                };
                File.AppendAllText(
                    @"e:\BaiduSyncdisk\Code\CSharp\CursorProjects\hy-cad-tool\debug-1db17a.log",
                    payload.ToString(Formatting.None) + Environment.NewLine);
            }
            catch
            {
            }
        }
        // #endregion

        // #region agent log
        private static int CountNewLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') count++;
            }
            return count;
        }

        private static int CountTrailingNewLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int count = 0;
            for (int i = text.Length - 1; i >= 0; i--)
            {
                if (text[i] != '\n') break;
                count++;
            }
            return count;
        }

        private static string TailDebug(string text, int max = 40)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string tail = text.Length <= max ? text : text.Substring(text.Length - max);
            return tail.Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string NormalizeForVditor(string markdown)
        {
            string text = (markdown ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");
            text = Regex.Replace(text, "\n{3,}", "\n\n");
            if (text.Length > 0 && !text.EndsWith("\n", StringComparison.Ordinal))
                text += "\n";
            return text;
        }

        private static int FirstDiffIndex(string a, string b)
        {
            a ??= string.Empty;
            b ??= string.Empty;
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                if (a[i] != b[i]) return i;
            }
            return a.Length == b.Length ? -1 : n;
        }

        private static string SliceDebug(string text, int start, int len = 50)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            int s = Math.Max(0, Math.Min(start, text.Length));
            int l = Math.Max(0, Math.Min(len, text.Length - s));
            return text.Substring(s, l).Replace("\r", "\\r").Replace("\n", "\\n");
        }
        // #endregion

        private void WireChildControlEvents()
        {
            _eventRouter.Wire(new EditorUiEventHandlers
            {
                OnThemeDark = OnThemeDark,
                OnThemeLight = OnThemeLight,
                OnToggleRuler = OnToggleRuler,
                OnToggleOutline = OnToggleOutline,
                OnToggleBottomPanel = OnToggleBottomPanel,
                OnTogglePreview = OnTogglePreview,
                OnOpenSettingsPanel = OnOpenSettingsPanel,
                OnInsertCadClickAsync = OnInsertCadClickAsync,
                OnConfirmClickAsync = OnConfirmClickAsync,
                OnMinimizeRequested = () => SystemCommands.MinimizeWindow(this),
                OnMaxRestoreRequested = ToggleMaxRestore,
                OnCloseRequested = OnCloseClick,
                OnWorkspaceModeRequested = ApplyWorkspaceMode,
                OnDragMoveRequested = () =>
                {
                    try { DragMove(); }
                    catch (Exception ex) { LogSilentException(nameof(DragMove), ex); }
                },
                OnSystemMenuRequested = e => SystemCommands.ShowSystemMenu(this, e.ScreenPoint),
                OnFileOpenRequested = OpenMdFile,
                OnOutlineHeadingSelectedAsync = ScrollToHeadingAsync,
                OnLeftPanelStatusChanged = status => ViewModel.StatusText = status ?? "",
                OnCurrentFilePathChanged = newPath => ViewModel.CurrentFilePath = newPath,
                OnCurrentFileClearedRequested = () =>
                {
                    ViewModel.CurrentFilePath = "";
                    ViewModel.MarkdownText = "";
                    OnEditorContentLoadRequested("");
                },
                OnTogglePageOrientationRequested = OnTogglePageOrientation,
                OnResetLayoutRequestedAsync = OnResetLayoutRequestedAsync,
                OnPreviousPageRequestedAsync = () => ShiftPreviewPageAsync(-1),
                OnNextPageRequestedAsync = () => ShiftPreviewPageAsync(1),
                OnJumpPageRequestedAsync = JumpPreviewPageAsync
            });
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
                _layoutManager.IsRulerVisible,
                _layoutManager.IsOutlineVisible,
                _layoutManager.IsBottomPanelVisible,
                _layoutManager.IsPreviewVisible,
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
                await _sessionLifecycle.InitializeAsync();
                _js = _sessionLifecycle.EditorJsHelper;
                UpdateEditorActionRouting();
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
            _sessionLifecycle.Dispose();
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            _hwndSource?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (Win32MaximizeHelper.TryHandleMessage(hwnd, msg, lParam))
            {
                handled = true;
            }
            return IntPtr.Zero;
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
                    case "editorDebug":
                        // #region agent log
                        AgentDebugLog(
                            msg.Value<string>("runId") ?? "pre-fix",
                            msg.Value<string>("hypothesisId") ?? "H10",
                            msg.Value<string>("location") ?? "Vditor:editorDebug",
                            msg.Value<string>("message") ?? "editor debug message",
                            msg["data"] is JObject debugData ? debugData : new JObject());
                        // #endregion
                        break;
                    case "input":
                        string markdownFromEditor = msg.Value<string>("value") ?? "";
                        EditorInputSyncResult syncResult = _syncCoordinator.HandleEditorInput(markdownFromEditor);
                        // #region agent log
                        AgentDebugLog("pre-fix", "H7", "EditorWindow:OnWebMessageReceived", "editor input received", new
                        {
                            owner = _syncCoordinator.Owner.ToString(),
                            inputLength = markdownFromEditor.Length,
                            syncResult = syncResult.ToString()
                        });
                        // #endregion
                        if (syncResult == EditorInputSyncResult.Applied)
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
                _syncCoordinator.ClaimOwnership(ContentOwner.CSharp);
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
                var msg = JObject.Parse(args.WebMessageAsJson ?? "{}");
                string type = msg.Value<string>("type");
                if (string.Equals(type, "agentDebug", StringComparison.OrdinalIgnoreCase))
                {
                    // #region agent log
                    AgentDebugLog(
                        msg.Value<string>("runId") ?? "pre-fix",
                        msg.Value<string>("hypothesisId") ?? "H-Unknown",
                        msg.Value<string>("location") ?? "Preview:agentDebug",
                        msg.Value<string>("message") ?? "agent debug message",
                        msg["data"] is JObject dataObj ? dataObj : new JObject());
                    // #endregion
                    return;
                }
                // #region agent log
                AgentDebugLog("pre-fix", "H4", "EditorWindow:OnPreviewWebMessageReceived", "preview message received", new
                {
                    type,
                    owner = _syncCoordinator.Owner.ToString(),
                    vmLength = (ViewModel.MarkdownText ?? string.Empty).Length
                });
                // #endregion
                if (string.Equals(type, "colFocusIn", StringComparison.OrdinalIgnoreCase))
                {
                    _syncCoordinator.ClaimOwnership(ContentOwner.Preview);
                    return;
                }

                if (_syncCoordinator.Owner == ContentOwner.Preview
                    && string.Equals(type, "contentChanged", StringComparison.OrdinalIgnoreCase))
                {
                    string md = msg.Value<string>("markdown") ?? string.Empty;
                    string normalizedMd = NormalizeForVditor(md);
                    AgentDebugLog("post-fix", "H13", "EditorWindow:OnPreviewWebMessageReceived", "normalized preview markdown", new
                    {
                        rawLength = md.Length,
                        normalizedLength = normalizedMd.Length,
                        rawNewLines = CountNewLines(md),
                        normalizedNewLines = CountNewLines(normalizedMd),
                        changed = !string.Equals(md, normalizedMd, StringComparison.Ordinal)
                    });
                    // #region agent log
                    AgentDebugLog("pre-fix", "H4", "EditorWindow:OnPreviewWebMessageReceived", "preview contentChanged branch", new
                    {
                        mdLength = normalizedMd.Length,
                        vmLength = (ViewModel.MarkdownText ?? string.Empty).Length,
                        sameAsVm = string.Equals(ViewModel.MarkdownText ?? string.Empty, normalizedMd, StringComparison.Ordinal)
                    });
                    // #endregion
                    bool sameAsVm = string.Equals(ViewModel.MarkdownText ?? string.Empty, normalizedMd, StringComparison.Ordinal);
                    if (!sameAsVm)
                    {
                        ViewModel.SetMarkdownFromEditor(normalizedMd);
                    }
                    // #region agent log
                    AgentDebugLog("post-fix", "H14", "EditorWindow:OnPreviewWebMessageReceived", "force sync editor from unique truth", new
                    {
                        sameAsVm,
                        syncLength = normalizedMd.Length,
                        isEditorVisible = _layoutManager.IsEditorVisible,
                        isPaperPrimary = _layoutManager.IsPaperPrimaryEditMode,
                        editorControlVisibility = EditorWebView?.Visibility.ToString() ?? "null"
                    });
                    // #endregion
                    _ = SyncEditorFromPreviewAsync(normalizedMd);
                    if (!sameAsVm)
                        ScheduleAutoCadSync();
                }
                else
                {
                    _previewManager.TryHandlePreviewWebMessage(args.WebMessageAsJson, ViewModel, out PreviewContentChange contentChanged);
                    bool applied = _syncCoordinator.HandlePreviewContentChanged(contentChanged, out string appliedMarkdown);
                    // #region agent log
                    AgentDebugLog("pre-fix", "H4", "EditorWindow:OnPreviewWebMessageReceived", "fallback branch result", new
                    {
                        hasContentChanged = contentChanged != null,
                        applied,
                        appliedLength = (appliedMarkdown ?? string.Empty).Length
                    });
                    // #endregion
                    if (applied)
                    {
                        _ = SyncEditorFromPreviewAsync(appliedMarkdown);
                        ScheduleAutoCadSync();
                    }
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

                case nameof(EditorViewModel.MaxColumnCount):
                case nameof(EditorViewModel.MinColumnWidthPx):
                case nameof(EditorViewModel.MinColumnHeightPx):
                    SchedulePreviewRefresh(PreviewRefreshReason.ConfigChanged);
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
                case nameof(EditorViewModel.ColumnInnerPaddingMm):
                case nameof(EditorViewModel.BorderWidth):
                case nameof(EditorViewModel.HandleWidth):
                case nameof(EditorViewModel.HandleActiveWidth):
                case nameof(EditorViewModel.FontFileName):
                case nameof(EditorViewModel.BigFontFileName):
                case nameof(EditorViewModel.BoldFontName):
                case nameof(EditorViewModel.PreviewFontFamily):
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
            await _previewInteractions.TryApplyLivePreviewZoomAsync();
        }

        private async Task RefreshPreviewAsync(PreviewRefreshReason reason = PreviewRefreshReason.ConfigChanged)
        {
            try
            {
                if (_syncCoordinator.Owner == ContentOwner.Preview
                    && reason == PreviewRefreshReason.ContentInput)
                    return;

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
            _layoutManager.ToggleOutline();
            RelayoutPreviewIfNeeded();
        }

        private void OnToggleBottomPanel()
        {
            _layoutManager.ToggleBottomPanel();
            ViewModel.StatusText = _layoutManager.IsBottomPanelVisible ? "底部面板：已打开" : "底部面板：关闭";
        }

        private void OnOpenSettingsPanel()
        {
            if (!_layoutManager.IsOutlineVisible)
            {
                _layoutManager.ShowSettingsOutline();
                RelayoutPreviewIfNeeded();
            }

            _leftPanel.ShowSettingsTab();
            ViewModel.StatusText = "已打开设置面板";
        }

        private void OnTogglePreview()
        {
            _layoutManager.TogglePreview();
            UpdateEditorActionRouting();
            if (_layoutManager.IsPreviewVisible)
                _ = RefreshPreviewAsync(PreviewRefreshReason.ViewportChanged);
            ViewModel.StatusText = _layoutManager.IsPreviewVisible
                ? $"预览：已显示（图纸主编辑：{(_layoutManager.IsPaperPrimaryEditMode ? "开" : "关")}）"
                : "预览：已隐藏，已自动保留编辑面板";
        }

        private void OnToggleRuler()
        {
            _layoutManager.ToggleRuler();
            ViewModel.StatusText = _layoutManager.IsRulerVisible ? "标线：已显示" : "标线：已隐藏";
        }

        private void OnTogglePageOrientation()
        {
            ViewModel.IsLandscape = !ViewModel.IsLandscape;
            ViewModel.StatusText = $"图纸方向：{ViewModel.PageOrientationLabel}";
        }

        private async Task ShiftPreviewPageAsync(int delta)
        {
            await _previewInteractions.ShiftPreviewPageAsync(delta);
        }

        private async Task JumpPreviewPageAsync(int page)
        {
            await _previewInteractions.JumpPreviewPageAsync(page);
        }

        private void UpdatePreviewPageState()
        {
            _previewPanel?.UpdatePageState(_previewManager.CurrentPage, _previewManager.TotalPages);
        }

        private void ApplyWorkspaceMode(WorkspaceMode mode)
        {
            _syncCoordinator.ClaimOwnership(ContentOwner.CSharp);
            WorkspaceModeApplyResult result = _layoutManager.ApplyWorkspaceMode(mode, _editorReady && EditorWebView?.CoreWebView2 != null);
            AgentDebugLog("post-fix", "H15", "EditorWindow:ApplyWorkspaceMode", "workspace mode applied", new
            {
                mode = mode.ToString(),
                resultStatus = result.StatusText,
                resultEditorBecameVisible = result.EditorBecameVisible,
                resultShouldRefreshPreview = result.ShouldRefreshPreview,
                isEditorVisible = _layoutManager.IsEditorVisible,
                isPaperPrimary = _layoutManager.IsPaperPrimaryEditMode,
                editorControlVisibility = EditorWebView?.Visibility.ToString() ?? "null"
            });
            UpdateEditorActionRouting();
            ViewModel.StatusText = result.StatusText;

            // 编辑器从隐藏变为可见时（如排版→校对），同步当前 MarkdownText 到编辑器
            if (result.EditorBecameVisible)
                _ = SyncCurrentMarkdownToEditorAsync();

            if (result.ShouldRefreshPreview)
                _ = RefreshPreviewAsync(PreviewRefreshReason.ViewportChanged);
        }

        private void RelayoutPreviewIfNeeded()
        {
            if (!_layoutManager.IsPreviewVisible) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _layoutManager.EnsurePreviewColumnVisible();
                _ = RefreshPreviewAsync(PreviewRefreshReason.ViewportChanged);
                SyncRulerFromPaper();
            }), DispatcherPriority.Background);
        }

        private void ApplyOutlineLayout()
        {
            _layoutManager.ApplyOutlineLayout();
        }

        private void ApplyPreviewLayout()
        {
            _layoutManager.ApplyPreviewLayout();
        }

        private void ApplyEditorLayout()
        {
            _layoutManager.ApplyEditorLayout();
            UpdateEditorActionRouting();
        }

        private void ApplyRulerLayout()
        {
            _layoutManager.ApplyRulerLayout();
        }

        private void ApplyBottomPanelLayout()
        {
            _layoutManager.ApplyBottomPanelLayout();
        }

        private void ApplyDefaultWorkspaceLayout(bool force)
        {
            _layoutManager.ApplyDefaultWorkspaceLayout(force, ActualWidth, Width);
            ApplyEditorLayout();
            UpdateRulerScale();
        }

        private void UpdateRulerScale()
        {
            _previewManager.UpdateRulerScale(_previewPanel, ViewModel);
        }

        private void FitPaperToPreviewArea()
        {
            _previewManager.FitPaperToPreviewArea(_previewPanel, ViewModel, _layoutManager.IsPreviewVisible);
        }

        private void SyncRulerFromPaper()
        {
            if (!_layoutManager.IsPreviewVisible || _previewPanel.Visibility != Visibility.Visible) return;
            UpdateRulerScale();
        }

        private void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            if (handled || !_layoutManager.IsPreviewVisible || _previewPanel.Visibility != Visibility.Visible) return;
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
            _layoutManager.OnOutlineSplitterDragCompleted();
        }

        private void OnPreviewSplitterDragCompleted(object sender, DragCompletedEventArgs e)
        {
            _layoutManager.OnPreviewSplitterDragCompleted();
        }

        private void OnBottomPanelSplitterDragCompleted(object sender, DragCompletedEventArgs e)
        {
            _layoutManager.OnBottomPanelSplitterDragCompleted();
        }

        private async Task OnInsertCadClickAsync()
        {
            var result = await BuildEditorResultAsync(confirmed: true);
            Result = result;
            CadSyncService.RaiseManualSync(result);
        }

        private async Task OnConfirmClickAsync()
        {
            var result = await BuildEditorResultAsync(confirmed: true);
            Result = result;
            CadSyncService.RaiseManualSync(result);

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
            await _cadSyncService.TriggerLiveSyncAsync(
                () => BuildEditorResultAsync(confirmed: true),
                result => Result = result,
                CadSyncService.RaiseLiveSync,
                ScheduleAutoCadSync);
        }

        private async Task<EditorResult> BuildEditorResultAsync(bool confirmed)
        {
            return await _cadSyncService.BuildEditorResultAsync(
                confirmed,
                SyncMarkdownFromEditorAsync,
                RefreshPreviewAsync,
                SyncFromPreviewAsync);
        }

        private async Task SyncMarkdownFromEditorAsync()
        {
            await _syncCoordinator.SyncMarkdownFromEditorAsync(
                CanUseEditorScriptPipeline,
                script => EditorWebView.CoreWebView2.ExecuteScriptAsync(script),
                LogSilentException);
        }

        private async Task SyncEditorFromPreviewAsync(string markdown)
        {
            bool canUse = _editorReady && EditorWebView?.CoreWebView2 != null;
            // #region agent log
            AgentDebugLog("pre-fix", "H5", "EditorWindow:SyncEditorFromPreviewAsync", "sync requested", new
            {
                canUse,
                editorReady = _editorReady,
                hasEditorCore = EditorWebView?.CoreWebView2 != null,
                markdownLength = (markdown ?? string.Empty).Length,
                isEditorVisible = _layoutManager.IsEditorVisible,
                isPaperPrimary = _layoutManager.IsPaperPrimaryEditMode,
                editorControlVisibility = EditorWebView?.Visibility.ToString() ?? "null"
            });
            // #endregion
            await _syncCoordinator.SyncEditorFromPreviewAsync(
                markdown,
                () => _editorReady && EditorWebView?.CoreWebView2 != null,
                async script =>
                {
                    // #region agent log
                    AgentDebugLog("pre-fix", "H6", "EditorWindow:SyncEditorFromPreviewAsync", "execute script begin", new
                    {
                        scriptLength = (script ?? string.Empty).Length
                    });
                    // #endregion
                    string result = await EditorWebView.CoreWebView2.ExecuteScriptAsync(script);
                    // #region agent log
                    AgentDebugLog("pre-fix", "H6", "EditorWindow:SyncEditorFromPreviewAsync", "execute script end", new
                    {
                        resultLength = (result ?? string.Empty).Length
                    });
                    // #endregion
                    return result;
                },
                () => _previewPanel?.PreviewWebViewControl?.IsKeyboardFocusWithin == true,
                () => _previewPanel?.PreviewWebViewControl?.Focus(),
                LogSilentException);
            if (canUse)
            {
                try
                {
                    string editorContentRaw = await EditorWebView.CoreWebView2.ExecuteScriptAsync("getContent()");
                    string editorContent = string.IsNullOrWhiteSpace(editorContentRaw) || editorContentRaw == "null"
                        ? string.Empty
                        : JsonConvert.DeserializeObject<string>(editorContentRaw) ?? string.Empty;
                    // #region agent log
                    AgentDebugLog("pre-fix", "H8", "EditorWindow:SyncEditorFromPreviewAsync", "editor content after setContent", new
                    {
                        targetLength = (markdown ?? string.Empty).Length,
                        editorLength = editorContent.Length,
                        sameAsTarget = string.Equals(editorContent, markdown ?? string.Empty, StringComparison.Ordinal),
                        targetNewLines = CountNewLines(markdown ?? string.Empty),
                        editorNewLines = CountNewLines(editorContent),
                        targetTrailingNewLines = CountTrailingNewLines(markdown ?? string.Empty),
                        editorTrailingNewLines = CountTrailingNewLines(editorContent),
                        targetTail = TailDebug(markdown ?? string.Empty),
                        editorTail = TailDebug(editorContent)
                    });
                    if (!string.Equals(editorContent, markdown ?? string.Empty, StringComparison.Ordinal))
                    {
                        int diff = FirstDiffIndex(markdown ?? string.Empty, editorContent);
                        AgentDebugLog("pre-fix", "H12", "EditorWindow:SyncEditorFromPreviewAsync", "first diff details", new
                        {
                            diffIndex = diff,
                            targetAround = SliceDebug(markdown ?? string.Empty, Math.Max(0, diff - 20)),
                            editorAround = SliceDebug(editorContent, Math.Max(0, diff - 20))
                        });
                    }
                    // #endregion

                }
                catch (Exception ex)
                {
                    LogSilentException(nameof(SyncEditorFromPreviewAsync), ex);
                }
            }
        }

        private async Task SyncCurrentMarkdownToEditorAsync()
        {
            await _syncCoordinator.SyncCurrentMarkdownToEditorAsync(
                ViewModel.MarkdownText,
                _editorReady && EditorWebView?.CoreWebView2 != null,
                script => EditorWebView.CoreWebView2.ExecuteScriptAsync(script),
                LogSilentException);
        }

        private async Task<bool> OnPreviewEditorActionRequestedAsync(string action)
        {
            return await _previewInteractions.ApplyPreviewEditorActionAsync(action, async () =>
            {
                await SyncFromPreviewAsync();
                RefreshOutline();
                ScheduleAutoCadSync();
            });
        }

        private void OnPreviewNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            _syncCoordinator.RestoreFocusAfterNavigation(
                action => Dispatcher.BeginInvoke(action, DispatcherPriority.Input),
                () => _previewManager.RestoreCaretAfterNavigationAsync(_previewPanel?.PreviewWebViewControl));
        }

        private bool CanUseEditorScriptPipeline()
        {
            if (!_layoutManager.IsEditorVisible) return false;
            if (_layoutManager.IsPaperPrimaryEditMode) return false;
            if (!_editorReady) return false;
            return EditorWebView?.CoreWebView2 != null;
        }

        private async Task ApplyPaperColumnLayoutAsync()
        {
            await _previewInteractions.ApplyPaperColumnLayoutAsync();
        }

        private async Task ApplyPaperGeometryAsync()
        {
            await _previewInteractions.ApplyPaperGeometryAsync();
        }

        private async Task ResetPaperLayoutAsync()
        {
            await _previewInteractions.ResetPaperLayoutAsync();
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
            if (_layoutManager.IsPaperPrimaryEditMode || !_layoutManager.IsEditorVisible)
            {
                ViewModel.SetJsHelper(null);
                return;
            }

            ViewModel.SetJsHelper(_js);
        }
    }
}
