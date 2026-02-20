using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Threading;
using HyCADTool.MarkdownEditor.Html;
using HyCADTool.MarkdownEditor.ViewModels;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace HyCADTool.MarkdownEditor.Services
{
    internal sealed class EditorSessionLifecycle
    {
        private readonly EditorViewModel _viewModel;
        private readonly PreviewManager _previewManager;
        private readonly WebView2 _editorWebView;
        private readonly WebView2 _previewWebView;
        private readonly Dispatcher _dispatcher;
        private readonly Action<string, Exception> _log;
        private readonly Action _updateEditorActionRouting;
        private readonly Func<PreviewRefreshReason, Task> _refreshPreviewAsync;
        private readonly Action _updatePreviewPageState;
        private readonly Action _refreshOutline;
        private readonly Action _refreshFileList;
        private readonly Action<bool> _applyDefaultWorkspaceLayout;
        private readonly Action _fitPaperToPreviewArea;
        private readonly Action _updateRulerScale;
        private readonly EventHandler<CoreWebView2WebMessageReceivedEventArgs> _onPreviewWebMessageReceived;
        private readonly EventHandler<CoreWebView2NavigationCompletedEventArgs> _onPreviewNavigationCompleted;
        private readonly EventHandler<CoreWebView2WebMessageReceivedEventArgs> _onEditorWebMessageReceived;
        private readonly Action _clearHostCallbacks;

        public EditorSessionLifecycle(
            EditorViewModel viewModel,
            PreviewManager previewManager,
            WebView2 editorWebView,
            WebView2 previewWebView,
            Dispatcher dispatcher,
            Action<string, Exception> log,
            Action updateEditorActionRouting,
            Func<PreviewRefreshReason, Task> refreshPreviewAsync,
            Action updatePreviewPageState,
            Action refreshOutline,
            Action refreshFileList,
            Action<bool> applyDefaultWorkspaceLayout,
            Action fitPaperToPreviewArea,
            Action updateRulerScale,
            EventHandler<CoreWebView2WebMessageReceivedEventArgs> onPreviewWebMessageReceived,
            EventHandler<CoreWebView2NavigationCompletedEventArgs> onPreviewNavigationCompleted,
            EventHandler<CoreWebView2WebMessageReceivedEventArgs> onEditorWebMessageReceived,
            Action clearHostCallbacks)
        {
            _viewModel = viewModel;
            _previewManager = previewManager;
            _editorWebView = editorWebView;
            _previewWebView = previewWebView;
            _dispatcher = dispatcher;
            _log = log;
            _updateEditorActionRouting = updateEditorActionRouting;
            _refreshPreviewAsync = refreshPreviewAsync;
            _updatePreviewPageState = updatePreviewPageState;
            _refreshOutline = refreshOutline;
            _refreshFileList = refreshFileList;
            _applyDefaultWorkspaceLayout = applyDefaultWorkspaceLayout;
            _fitPaperToPreviewArea = fitPaperToPreviewArea;
            _updateRulerScale = updateRulerScale;
            _onPreviewWebMessageReceived = onPreviewWebMessageReceived;
            _onPreviewNavigationCompleted = onPreviewNavigationCompleted;
            _onEditorWebMessageReceived = onEditorWebMessageReceived;
            _clearHostCallbacks = clearHostCallbacks;
        }

        public VditorJsHelper EditorJsHelper { get; private set; }

        public async Task InitializeAsync()
        {
            var cacheTask = VditorCacheManager.EnsureCachedAsync(msg =>
                _dispatcher.Invoke(() => _viewModel.StatusText = msg));

            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HyCADTool", "WebView2");
            Directory.CreateDirectory(userDataFolder);
            var envTask = CoreWebView2Environment.CreateAsync(null, userDataFolder);

            await Task.WhenAll(cacheTask, envTask);

            var env = envTask.Result;
            await _editorWebView.EnsureCoreWebView2Async(env);
            await _previewWebView.EnsureCoreWebView2Async(env);

            _previewManager.IsPreviewReady = _previewWebView.CoreWebView2 != null;
            if (_previewManager.IsPreviewReady)
            {
                _previewWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                _previewWebView.CoreWebView2.Settings.IsPinchZoomEnabled = false;
                _previewWebView.CoreWebView2.WebMessageReceived += _onPreviewWebMessageReceived;
                _previewWebView.CoreWebView2.NavigationCompleted += _onPreviewNavigationCompleted;
            }

            string cdnBase = null;
            if (VditorCacheManager.IsCached)
            {
                _editorWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "vditor.local", VditorCacheManager.CacheDir,
                    CoreWebView2HostResourceAccessKind.Allow);
                cdnBase = "https://vditor.local";
            }

            EditorJsHelper = new VditorJsHelper(script => _editorWebView.CoreWebView2.ExecuteScriptAsync(script));
            _updateEditorActionRouting();
            _editorWebView.CoreWebView2.WebMessageReceived += _onEditorWebMessageReceived;

            string html = VditorHtmlTemplate.Generate(_viewModel.MarkdownText, cdnBase);
            _editorWebView.CoreWebView2.NavigateToString(html);

            if (_previewManager.IsPreviewReady && _previewManager.HasPendingHtml)
            {
                _previewWebView.CoreWebView2.NavigateToString(_previewManager.PendingHtml);
                _previewManager.ClearPendingHtml();
            }

            await _refreshPreviewAsync(PreviewRefreshReason.InitialLoad);
            _updatePreviewPageState();
            _refreshOutline();
            _refreshFileList();
            _applyDefaultWorkspaceLayout(false);

            _ = _dispatcher.BeginInvoke(new Action(() =>
            {
                _fitPaperToPreviewArea();
                _updateRulerScale();
            }), DispatcherPriority.Loaded);
        }

        public void Dispose()
        {
            try
            {
                if (_editorWebView?.CoreWebView2 != null)
                    _editorWebView.CoreWebView2.WebMessageReceived -= _onEditorWebMessageReceived;
                if (_previewWebView?.CoreWebView2 != null)
                {
                    _previewWebView.CoreWebView2.WebMessageReceived -= _onPreviewWebMessageReceived;
                    _previewWebView.CoreWebView2.NavigationCompleted -= _onPreviewNavigationCompleted;
                }
            }
            catch (Exception ex)
            {
                _log?.Invoke(nameof(Dispose), ex);
            }

            try { _editorWebView?.Dispose(); } catch (Exception ex) { _log?.Invoke(nameof(Dispose), ex); }
            try { _previewWebView?.Dispose(); } catch (Exception ex) { _log?.Invoke(nameof(Dispose), ex); }

            _clearHostCallbacks?.Invoke();
        }
    }
}
