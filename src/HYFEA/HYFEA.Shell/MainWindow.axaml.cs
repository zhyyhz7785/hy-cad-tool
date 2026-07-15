using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HYFEA.Shell.ViewModels;

namespace HYFEA.Shell;

public partial class MainWindow : Window
{
    private MainViewModel? _vm;
    private bool _viewerReady;
    private bool _useSidecar;
    private ViewerSidecar? _sidecar;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closing += OnClosing;
        DataContextChanged += (_, _) => AttachViewModel();
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        AttachViewModel();
        ResultWebView.NavigationCompleted += OnNavigationCompleted;
        await StartViewerAsync();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        _sidecar?.Dispose();
        _sidecar = null;
    }

    private void AttachViewModel()
    {
        if (_vm != null)
            _vm.MeshJsonChanged -= OnMeshJsonChanged;

        _vm = DataContext as MainViewModel;
        if (_vm != null)
            _vm.MeshJsonChanged += OnMeshJsonChanged;
    }

    private async Task StartViewerAsync()
    {
        _sidecar?.Dispose();
        _sidecar = new ViewerSidecar();
        var (ok, message) = await _sidecar.TryStartAsync(ViewerSidecar.DefaultVtuPath);
        if (ok && _sidecar.BaseUri != null)
        {
            _useSidecar = true;
            try
            {
                ResultWebView.Source = _sidecar.BaseUri;
                SetStatusHint("视口：PyVista/trame 边车 — " + message);
                return;
            }
            catch (Exception ex)
            {
                _sidecar.Dispose();
                _sidecar = null;
                _useSidecar = false;
                SetStatusHint("边车 WebView 导航失败，回落 vtk.js：" + ex.Message);
            }
        }
        else
        {
            _sidecar?.Dispose();
            _sidecar = null;
            _useSidecar = false;
            SetStatusHint("边车未启动，回落 vtk.js — " + message);
        }

        NavigateVtkJsFallback();
    }

    private void NavigateVtkJsFallback()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var indexPath = Path.Combine(baseDir, "Assets", "Viewer", "index.html");
            if (!File.Exists(indexPath))
            {
                SetStatusHint("未找到 Assets/Viewer/index.html，请确认已复制到输出目录");
                return;
            }

            ResultWebView.Source = new Uri(indexPath);
        }
        catch (Exception ex)
        {
            SetStatusHint("WebView 加载失败（需 WebView2 Runtime）：" + ex.Message);
        }
    }

    private async void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        _viewerReady = e.IsSuccess;
        if (!e.IsSuccess)
        {
            SetStatusHint("视口导航失败 — 请安装 Microsoft Edge WebView2 Runtime");
            return;
        }

        if (!_useSidecar)
            await PushMeshAsync();
    }

    private async void OnMeshJsonChanged(object? sender, EventArgs e)
    {
        // 边车模式：Present 已写 hyfea-last.vtu，viewer 按 mtime 自刷新
        if (_useSidecar)
            return;
        await PushMeshAsync();
    }

    private async Task PushMeshAsync()
    {
        if (!_viewerReady || _vm?.LastMeshJson is not { Length: > 0 } json)
            return;

        try
        {
            var literal = JsonSerializer.Serialize(json);
            var script = $"window.hyfea && window.hyfea.setMesh({literal});";
            await ResultWebView.InvokeScript(script);
        }
        catch (Exception ex)
        {
            SetStatusHint("推送云图失败：" + ex.Message);
        }
    }

    private void SetStatusHint(string message) =>
        _vm?.ReportStatus(message);

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        ResultWebView.NavigationCompleted -= OnNavigationCompleted;
        if (_vm != null)
            _vm.MeshJsonChanged -= OnMeshJsonChanged;
        _sidecar?.Dispose();
        _sidecar = null;
        base.OnUnloaded(e);
    }
}
