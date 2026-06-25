using System;
using System.Windows;
using HyCADTool.UniverEditor.Services;

namespace HyCADTool.UniverEditor.Views
{
    public partial class UniverEditorWindow : Window
    {
        private UniverSessionLifecycle _session;
        private readonly UniverEditorHostContext _hostContext;

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

            try
            {
                SetStatus("初始化 WebView2...");
                _session = new UniverSessionLifecycle(UniverWebView, SetStatus, _hostContext);
                _session.Ready += OnSessionReady;
                _session.SnapshotExported += OnSnapshotExported;
                _hostContext.GridChanged += OnHostGridChanged;
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
            EditorLauncher.RequestExportSnapshot = () => _ = _session.ExportSnapshotAsync();
            RefreshSummary();
            _ = ReloadGridAsync();
        }

        private async void OnHostGridChanged()
        {
            await ReloadGridAsync();
            RefreshSummary();
        }

        private void OnSnapshotExported(string json)
        {
            _hostContext?.OnSnapshotExported?.Invoke(json);
            RefreshSummary();
        }

        private async System.Threading.Tasks.Task ReloadGridAsync()
        {
            if (_session == null || !_session.IsReady)
                return;

            string json = _hostContext?.TryGetLoadSnapshotJson();
            if (string.IsNullOrWhiteSpace(json))
                return;

            await _session.LoadSnapshotAsync(json);
        }

        private void RefreshSummary()
        {
            SummaryTextBlock.Text = _hostContext?.GetSummaryText?.Invoke() ?? "未加载表格";
            var status = _hostContext?.GetStatusMessage?.Invoke();
            if (!string.IsNullOrWhiteSpace(status))
                FooterStatusTextBlock.Text = status;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            if (_hostContext != null)
                _hostContext.GridChanged -= OnHostGridChanged;

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
            _hostContext?.InvokeSafe(_hostContext.NewEmptyTable, SetStatus);
            RefreshSummary();
        }

        private void OnLoadPersonnelSampleClick(object sender, RoutedEventArgs e)
        {
            _hostContext?.InvokeSafe(_hostContext.LoadPersonnelSample, SetStatus);
            RefreshSummary();
        }

        private void OnPickClick(object sender, RoutedEventArgs e)
        {
            _hostContext?.InvokeSafe(_hostContext.RequestPick, SetStatus);
        }

        private void OnPublishClick(object sender, RoutedEventArgs e)
        {
            _hostContext?.InvokeSafe(_hostContext.RequestPublish, SetStatus);
        }

        private void OnExportJsonClick(object sender, RoutedEventArgs e)
        {
            _hostContext?.InvokeSafe(_hostContext.ExportJsonSnapshot, SetStatus);
        }

        private void OnImportXlsxClick(object sender, RoutedEventArgs e)
        {
            _hostContext?.InvokeSafe(_hostContext.ImportXlsx, SetStatus);
        }

        private void OnExportXlsxClick(object sender, RoutedEventArgs e)
        {
            _hostContext?.InvokeSafe(_hostContext.ExportXlsx, SetStatus);
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
