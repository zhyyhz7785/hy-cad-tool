using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json.Linq;

namespace HyCADTool.UniverEditor.Services
{
    internal sealed class UniverSessionLifecycle : IDisposable
    {
        private const string VirtualHostName = "univer.local";
        private const string EntryUrl = "https://univer.local/index.html";
        private const string Net8DirKey = "HyCADTool.UniverEditorLoader.Net8Dir";
        private const string WebDistPathKey = "HyCADTool.UniverEditor.WebDistPath";
        private const string ReCallDepsPathKey = "HyCADTool.ReCall.DependenciesPath";
        private const string ReCallSourceBinPathKey = "HyCADTool.ReCall.SourceBinPath";

        private readonly WebView2 _webView;
        private readonly Action<string> _setStatus;
        private readonly UniverEditorHostContext _fallbackHostContext;

        private UniverEditorHostContext Host => EditorLauncher.HostContext ?? _fallbackHostContext;
        private bool _ready;
        private bool _disposed;
        private int _exportRequestId;

        public UniverSessionLifecycle(
            WebView2 webView,
            Action<string> setStatus,
            UniverEditorHostContext hostContext)
        {
            _webView = webView;
            _setStatus = setStatus;
            _fallbackHostContext = hostContext;
        }

        public bool IsReady => _ready;

        public event Action Ready;

        public async Task InitializeAsync()
        {
            _setStatus?.Invoke("初始化 WebView2...");

            string distPath = ResolveDistPath();
            if (!Directory.Exists(distPath))
                throw new DirectoryNotFoundException($"未找到 Univer 前端资源目录: {distPath}");

            string indexPath = Path.Combine(distPath, "index.html");
            if (!File.Exists(indexPath))
                throw new FileNotFoundException($"未找到 Univer 入口页: {indexPath}");

            string runtimeVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
            if (string.IsNullOrWhiteSpace(runtimeVersion))
                throw new InvalidOperationException("未检测到 WebView2 Runtime，请先安装 Microsoft Edge WebView2 Runtime。");

            var env = await WebView2EnvironmentProvider.GetOrCreateAsync();
            await _webView.EnsureCoreWebView2Async(env);

            _setStatus?.Invoke("加载 Univer 页面...");

            _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                VirtualHostName,
                distPath,
                CoreWebView2HostResourceAccessKind.Allow);
            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            _webView.CoreWebView2.Navigate(EntryUrl);
        }

        public Task LoadSnapshotAsync(string snapshotJson)
        {
            if (!_ready || string.IsNullOrWhiteSpace(snapshotJson))
                return Task.CompletedTask;

            return PostCommandAsync("loadSnapshot", JToken.Parse(snapshotJson));
        }

        public Task ExportSnapshotAsync()
        {
            if (!_ready)
                return Task.CompletedTask;

            _exportRequestId++;
            return PostCommandAsync("exportSnapshot", null);
        }

        public Task ExportSnapshotForPublishAsync(string mode)
        {
            if (!_ready)
                return Task.CompletedTask;

            _exportRequestId++;
            var payload = new JObject { ["mode"] = mode };
            return PostCommandAsync("exportSnapshotForPublish", payload);
        }

        private Task PostCommandAsync(string type, JToken payload)
        {
            var message = new JObject { ["type"] = type };
            if (payload != null)
                message["payload"] = payload;

            if (_webView?.CoreWebView2 == null)
                return Task.CompletedTask;

            string json = message.ToString();
            void Post()
            {
                _webView.CoreWebView2.PostWebMessageAsString(json);
            }

            if (_webView.Dispatcher.CheckAccess())
                Post();
            else
                _webView.Dispatcher.Invoke(Post);

            return Task.CompletedTask;
        }

        private static string ResolveDistPath()
        {
            string cached = AppDomain.CurrentDomain.GetData(WebDistPathKey) as string;
            if (IsValidDistPath(cached))
                return Path.GetFullPath(cached);

            foreach (string distPath in EnumerateDistCandidates())
            {
                if (IsValidDistPath(distPath))
                {
                    AppDomain.CurrentDomain.SetData(WebDistPathKey, distPath);
                    return distPath;
                }
            }

            throw new DirectoryNotFoundException(
                "未找到 Univer Web/dist。"
                + " 请在 src\\HyCADTool.UniverEditor\\Web 执行 npm run build，"
                + "再 dotnet build 并 C2 重载。");
        }

        private static IEnumerable<string> EnumerateDistCandidates()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string net8Dir = AppDomain.CurrentDomain.GetData(Net8DirKey) as string;
            if (!string.IsNullOrWhiteSpace(net8Dir))
                TryAddDistCandidate(seen, Path.Combine(net8Dir, "Web", "dist"));

            string depsPath = AppDomain.CurrentDomain.GetData(ReCallDepsPathKey) as string;
            if (!string.IsNullOrWhiteSpace(depsPath))
            {
                TryAddDistCandidate(seen, Path.Combine(depsPath, "net8", "Web", "dist"));
                TryAddDistCandidate(seen, Path.Combine(depsPath, "Web", "dist"));
            }

            string sourceBin = AppDomain.CurrentDomain.GetData(ReCallSourceBinPathKey) as string;
            if (!string.IsNullOrWhiteSpace(sourceBin))
                TryAddDistCandidate(seen, Path.Combine(sourceBin, "net8", "Web", "dist"));

            foreach (string baseDir in EnumerateDevOutputCandidates())
                TryAddDistCandidate(seen, Path.Combine(baseDir, "net8", "Web", "dist"));

            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrWhiteSpace(assemblyDir))
                TryAddDistCandidate(seen, Path.Combine(assemblyDir, "Web", "dist"));

            foreach (string candidate in seen)
                yield return candidate;
        }

        private static IEnumerable<string> EnumerateDevOutputCandidates()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string sourceBin = AppDomain.CurrentDomain.GetData(ReCallSourceBinPathKey) as string;
            if (!string.IsNullOrWhiteSpace(sourceBin))
            {
                string full = Path.GetFullPath(sourceBin.Trim());
                if (Directory.Exists(full) && seen.Add(full))
                    yield return full;
            }

            Assembly recall = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, "ReCall", StringComparison.OrdinalIgnoreCase));
            if (recall != null && !string.IsNullOrEmpty(recall.Location))
            {
                string recallDir = Path.GetDirectoryName(recall.Location);
                if (!string.IsNullOrEmpty(recallDir))
                {
                    string[] relatives =
                    {
                        Path.Combine(recallDir, "..", "..", "..", "HyCADTool", "bin", "Debug"),
                        Path.Combine(recallDir, "..", "..", "HyCADTool", "bin", "Debug"),
                    };

                    foreach (string relative in relatives)
                    {
                        string full = Path.GetFullPath(relative);
                        if (Directory.Exists(full) && seen.Add(full))
                            yield return full;
                    }
                }
            }

            string env = Environment.GetEnvironmentVariable("HYCAD_TOOL_BIN");
            if (!string.IsNullOrWhiteSpace(env))
            {
                string full = Path.GetFullPath(env.Trim());
                if (Directory.Exists(full) && seen.Add(full))
                    yield return full;
            }
        }

        private static void TryAddDistCandidate(ISet<string> seen, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            seen.Add(Path.GetFullPath(path));
        }

        private static bool IsValidDistPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && Directory.Exists(path)
                && File.Exists(Path.Combine(path, "index.html"));
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                _setStatus?.Invoke("初始化表格引擎...");
                return;
            }

            _setStatus?.Invoke($"Univer 页面加载失败: {e.WebErrorStatus}");
        }

        private async void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string raw = e.TryGetWebMessageAsString();
                if (string.IsNullOrWhiteSpace(raw))
                    return;

                JObject message = JObject.Parse(raw);
                string type = message.Value<string>("type");
                if (string.Equals(type, "ready", StringComparison.OrdinalIgnoreCase))
                {
                    _ready = true;
                    _setStatus?.Invoke("Univer 已就绪");
                    Ready?.Invoke();
                    await PushInitialSnapshotAsync();
                    return;
                }

                if (string.Equals(type, "cellChanged", StringComparison.OrdinalIgnoreCase))
                {
                    int row = message.Value<int?>("row") ?? -1;
                    int col = message.Value<int?>("col") ?? -1;
                    string text = message.Value<string>("text") ?? string.Empty;
                    if (row >= 0 && col >= 0)
                        Host?.OnCellChanged?.Invoke(row, col, text);
                    return;
                }

                if (string.Equals(type, "snapshot", StringComparison.OrdinalIgnoreCase))
                {
                    var exportMessage = new UniverWebSnapshotMessage
                    {
                        SnapshotJson = message["payload"]?.ToString(),
                        PublishMode = message.Value<string>("publishMode"),
                        ClipStartRow = message["clipRect"]?.Value<int?>("startRow"),
                        ClipStartCol = message["clipRect"]?.Value<int?>("startCol"),
                        ClipEndRow = message["clipRect"]?.Value<int?>("endRow"),
                        ClipEndCol = message["clipRect"]?.Value<int?>("endCol"),
                    };
                    InvokeOnUiThread(() => SnapshotExported?.Invoke(exportMessage));
                    return;
                }

                if (string.Equals(type, "hyCadAction", StringComparison.OrdinalIgnoreCase))
                {
                    string action = message.Value<string>("action");
                    InvokeOnUiThread(() => HandleHyCadAction(action));
                    return;
                }

                if (string.Equals(type, "selectionChanged", StringComparison.OrdinalIgnoreCase))
                {
                    int startRow = message.Value<int?>("startRow") ?? -1;
                    int startCol = message.Value<int?>("startCol") ?? -1;
                    int endRow = message.Value<int?>("endRow") ?? startRow;
                    int endCol = message.Value<int?>("endCol") ?? startCol;
                    if (startRow >= 0 && startCol >= 0)
                        InvokeOnUiThread(() => Host?.OnSelectionChanged?.Invoke(startRow, startCol, endRow, endCol));
                    return;
                }

                if (string.Equals(type, "hyCadLayout", StringComparison.OrdinalIgnoreCase))
                {
                    string op = message.Value<string>("op");
                    double value = message.Value<double?>("value") ?? 0.0;
                    if (!string.IsNullOrEmpty(op))
                        InvokeOnUiThread(() => HandleHyCadLayout(op, value));
                    return;
                }

                if (string.Equals(type, "hyCadFileAction", StringComparison.OrdinalIgnoreCase))
                {
                    string action = message.Value<string>("action");
                    InvokeOnUiThread(() => HandleHyCadFileAction(action));
                    return;
                }

                if (string.Equals(type, "hyCadWindowControl", StringComparison.OrdinalIgnoreCase))
                {
                    string action = message.Value<string>("action");
                    InvokeOnUiThread(() => WindowControlRequested?.Invoke(action));
                    return;
                }

                if (string.Equals(type, "snapshotLoaded", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (string.Equals(type, "error", StringComparison.OrdinalIgnoreCase))
                {
                    string detail = message.Value<string>("message") ?? "未知错误";
                    Host?.OnExportError?.Invoke(detail);
                    _setStatus?.Invoke($"Univer 错误: {detail}");
                }
            }
            catch
            {
                // ignore malformed host messages
            }
        }

        public event Action<UniverWebSnapshotMessage> SnapshotExported;

        public event Action<string> WindowControlRequested;

        private void HandleHyCadAction(string action)
        {
            switch (action)
            {
                case "pick":
                    Host?.InvokeSafe(Host.RequestPick, _setStatus);
                    break;
                case "publish":
                    Host?.InvokeSafe(Host.RequestPublish, _setStatus);
                    break;
                case "publishRangeFull":
                    Host?.InvokeSafe(Host.RequestPublishRangeFull, _setStatus);
                    break;
                case "publishRangeContent":
                    Host?.InvokeSafe(Host.RequestPublishRangeContent, _setStatus);
                    break;
            }
        }

        private void HandleHyCadLayout(string op, double value)
        {
            // setScale 仅改 VM.Scale（不改 grid、不回灌）；其余结构/尺寸 op 经 OnLayoutOp → VM 命令 → GridChanged 回灌。
            if (string.Equals(op, "setScale", StringComparison.OrdinalIgnoreCase))
            {
                if (Host?.SetScale != null)
                    Host.InvokeSafe(() => Host.SetScale(value), _setStatus);
                return;
            }

            var layoutOp = Host?.OnLayoutOp;
            if (layoutOp != null)
                Host.InvokeSafe(() => layoutOp(op, value), _setStatus);
        }

        private void HandleHyCadFileAction(string action)
        {
            switch (action)
            {
                case "newEmpty":
                    Host?.InvokeSafe(Host.NewEmptyTable, _setStatus);
                    break;
                case "loadPersonnel":
                    Host?.InvokeSafe(Host.LoadPersonnelSample, _setStatus);
                    break;
                case "importXlsx":
                    Host?.InvokeSafe(Host.ImportXlsx, _setStatus);
                    break;
                case "exportXlsx":
                    Host?.InvokeSafe(Host.ExportXlsx, _setStatus);
                    break;
                case "exportJson":
                    Host?.InvokeSafe(Host.ExportJsonSnapshot, _setStatus);
                    break;
                case "pick":
                    Host?.InvokeSafe(Host.RequestPick, _setStatus);
                    break;
                case "publish":
                    Host?.InvokeSafe(Host.RequestPublish, _setStatus);
                    break;
                case "publishRangeFull":
                    Host?.InvokeSafe(Host.RequestPublishRangeFull, _setStatus);
                    break;
                case "publishRangeContent":
                    Host?.InvokeSafe(Host.RequestPublishRangeContent, _setStatus);
                    break;
            }
        }

        private async Task PushInitialSnapshotAsync()
        {
            await PushScaleAsync();

            string json = Host?.TryGetLoadSnapshotJson();
            if (string.IsNullOrWhiteSpace(json))
                return;

            await LoadSnapshotAsync(json);
        }

        /// <summary>把当前 VM.Scale 下发网页布局 Tab（口径 B 初始化/恢复 hy 值）。</summary>
        public Task PushScaleAsync()
        {
            if (!_ready)
                return Task.CompletedTask;

            double scale;
            try
            {
                scale = Host?.GetScale?.Invoke() ?? 1.0;
            }
            catch
            {
                scale = 1.0;
            }

            if (scale <= 0)
                scale = 1.0;

            var payload = new JObject { ["scale"] = scale };
            return PostCommandAsync("setScale", payload);
        }

        private void InvokeOnUiThread(Action action)
        {
            if (action == null || _webView?.Dispatcher == null)
                return;

            if (_webView.Dispatcher.CheckAccess())
                action();
            else
                _webView.Dispatcher.Invoke(action);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                if (_webView?.CoreWebView2 != null)
                {
                    _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                    _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
                }
            }
            catch
            {
                // ignore
            }

            try { _webView?.Dispose(); } catch { }
        }
    }
}
