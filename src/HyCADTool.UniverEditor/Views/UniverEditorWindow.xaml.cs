using System;
using System.Windows;
using HyCADTool.UniverEditor.Services;

namespace HyCADTool.UniverEditor.Views
{
    public partial class UniverEditorWindow : Window
    {
        private UniverSessionLifecycle _session;
        private UniverEditorHostContext _hostContext;

        private UniverEditorHostContext ActiveContext => EditorLauncher.HostContext ?? _hostContext;

        public UniverEditorWindow(UniverEditorHostContext hostContext)
        {
            _hostContext = hostContext ?? new UniverEditorHostContext();
            InitializeComponent();
            Loaded += OnLoadedAsync;
            Closed += OnClosed;
        }

        private async void OnLoadedAsync(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoadedAsync;

            EditorLauncher.PrepareForCadInteraction = PrepareForCadInteraction;
            EditorLauncher.RestoreAfterCadInteraction = RestoreAfterCadInteraction;
            SyncHostContextBindings();

            try
            {
                SetStatus("初始化 WebView2...");
                _session = new UniverSessionLifecycle(UniverWebView, SetStatus, ActiveContext);
                _session.Ready += OnSessionReady;
                _session.SnapshotExported += OnSnapshotExported;
                await _session.InitializeAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"加载失败: {ex.Message}");
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Univer 编辑器",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OnSessionReady()
        {
            RebindExportSnapshot();
            RefreshSummary();
            _ = ReloadGridAsync();
        }

        public void SyncHostContextBindings()
        {
            var ctx = ActiveContext;
            if (ctx == null)
                return;

            ctx.GridChanged -= OnHostGridChanged;
            ctx.StatusChanged -= OnHostStatusChanged;
            ctx.GridChanged += OnHostGridChanged;
            ctx.StatusChanged += OnHostStatusChanged;
        }

        public void RebindExportSnapshot()
        {
            SyncHostContextBindings();

            if (_session == null || !_session.IsReady)
                return;

            var ctx = ActiveContext;
            ctx?.BindExportSnapshot?.Invoke(() => _ = _session.ExportSnapshotAsync());
            ctx?.BindExportForPublish?.Invoke(mode =>
                _ = _session.ExportSnapshotForPublishAsync(mode ?? "default"));
            EditorLauncher.RequestExportSnapshot = () => _ = _session.ExportSnapshotAsync();
            EditorLauncher.RequestExportSnapshotForPublish = mode =>
                _ = _session.ExportSnapshotForPublishAsync(mode ?? "default");
        }

        private void PrepareForCadInteraction()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(PrepareForCadInteraction);
                return;
            }

            Topmost = false;
            SetStatus("请在 AutoCAD 命令行/图面指定插入点（Esc 取消）");
        }

        private void RestoreAfterCadInteraction()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(RestoreAfterCadInteraction);
                return;
            }

            Topmost = true;
            RefreshSummary();
        }

        private void OnHostStatusChanged()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(OnHostStatusChanged));
                return;
            }

            RefreshSummary();
        }

        private int _reloadSeq;

        private void OnHostGridChanged()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(OnHostGridChanged));
                return;
            }

            ScheduleDebouncedReload();
        }

        private async void ScheduleDebouncedReload()
        {
            int seq = ++_reloadSeq;
            await System.Threading.Tasks.Task.Delay(50);
            if (seq != _reloadSeq)
                return;

            await ReloadGridThenRefreshAsync();
        }

        private async System.Threading.Tasks.Task ReloadGridThenRefreshAsync()
        {
            await ReloadGridAsync();
            RefreshSummary();
        }

        private void OnSnapshotExported(UniverWebSnapshotMessage message)
        {
            if (message == null)
                return;

            string metaJson = BuildMetaJson(message);
            ActiveContext?.OnSnapshotExported?.Invoke(message.SnapshotJson, metaJson);
            RefreshSummary();
        }

        private static string BuildMetaJson(UniverWebSnapshotMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.PublishMode)
                || string.Equals(message.PublishMode, "default", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var meta = new Newtonsoft.Json.Linq.JObject
            {
                ["mode"] = message.PublishMode,
            };

            if (message.ClipStartRow.HasValue)
            {
                meta["clipRect"] = new Newtonsoft.Json.Linq.JObject
                {
                    ["startRow"] = message.ClipStartRow.Value,
                    ["startCol"] = message.ClipStartCol ?? 0,
                    ["endRow"] = message.ClipEndRow ?? message.ClipStartRow.Value,
                    ["endCol"] = message.ClipEndCol ?? message.ClipStartCol ?? 0,
                };
            }

            return meta.ToString(Newtonsoft.Json.Formatting.None);
        }

        private async System.Threading.Tasks.Task ReloadGridAsync()
        {
            if (_session == null || !_session.IsReady)
                return;

            string json = ActiveContext?.TryGetLoadSnapshotJson();
            // #region agent log
            AgentDebugLog646873.Write("H4", "UniverEditorWindow.ReloadGridAsync", "reload", new
            {
                hasJson = !string.IsNullOrWhiteSpace(json),
                jsonLength = json?.Length ?? 0,
            });
            // #endregion

            if (string.IsNullOrWhiteSpace(json))
                return;

            await _session.LoadSnapshotAsync(json);
        }

        private void RefreshSummary()
        {
            SummaryTextBlock.Text = ActiveContext?.GetSummaryText?.Invoke() ?? "未加载表格";
            var status = ActiveContext?.GetStatusMessage?.Invoke();
            if (!string.IsNullOrWhiteSpace(status))
                FooterStatusTextBlock.Text = status;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            var ctx = ActiveContext;
            if (ctx != null)
            {
                ctx.GridChanged -= OnHostGridChanged;
                ctx.StatusChanged -= OnHostStatusChanged;
            }

            if (ReferenceEquals(EditorLauncher.PrepareForCadInteraction, (Action)PrepareForCadInteraction))
                EditorLauncher.PrepareForCadInteraction = null;

            if (ReferenceEquals(EditorLauncher.RestoreAfterCadInteraction, (Action)RestoreAfterCadInteraction))
                EditorLauncher.RestoreAfterCadInteraction = null;

            try { _session?.Dispose(); }
            catch { }
            finally { _session = null; }
        }

        private void SetStatus(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SetStatus(message));
                return;
            }

            StatusTextBlock.Text = message;
            FooterStatusTextBlock.Text = message;

            if (_session != null && _session.IsReady)
                LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void OnNewEmptyTableClick(object sender, RoutedEventArgs e)
        {
            ActiveContext?.InvokeSafe(ActiveContext.NewEmptyTable, SetStatus);
            RefreshSummary();
        }

        private void OnLoadPersonnelSampleClick(object sender, RoutedEventArgs e)
        {
            ActiveContext?.InvokeSafe(ActiveContext.LoadPersonnelSample, SetStatus);
            RefreshSummary();
        }

        private void OnPickClick(object sender, RoutedEventArgs e)
        {
            ActiveContext?.InvokeSafe(ActiveContext.RequestPick, SetStatus);
            RefreshSummary();
        }

        private void OnPublishClick(object sender, RoutedEventArgs e)
        {
            ActiveContext?.InvokeSafe(ActiveContext.RequestPublish, SetStatus);
            RefreshSummary();
        }

        private void OnPublishRangeFullClick(object sender, RoutedEventArgs e)
        {
            ActiveContext?.InvokeSafe(ActiveContext.RequestPublishRangeFull, SetStatus);
            RefreshSummary();
        }

        private void OnExportJsonClick(object sender, RoutedEventArgs e)
        {
            ActiveContext?.InvokeSafe(ActiveContext.ExportJsonSnapshot, SetStatus);
        }

        private void OnImportXlsxClick(object sender, RoutedEventArgs e)
        {
            ActiveContext?.InvokeSafe(ActiveContext.ImportXlsx, SetStatus);
            RefreshSummary();
        }

        private void OnExportXlsxClick(object sender, RoutedEventArgs e)
        {
            ActiveContext?.InvokeSafe(ActiveContext.ExportXlsx, SetStatus);
        }

        private void OnMinimizeClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        public void ForceClose()
        {
            Close();
        }
    }
}
