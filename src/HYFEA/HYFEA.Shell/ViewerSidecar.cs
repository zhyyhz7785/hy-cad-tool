using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace HYFEA.Shell;

/// <summary>托管 PyVista/trame Python 边车进程。</summary>
public sealed class ViewerSidecar : IDisposable
{
    private Process? _process;
    private bool _disposed;

    public int Port { get; private set; }

    public Uri? BaseUri => Port > 0 ? new Uri($"http://127.0.0.1:{Port}/") : null;

    public bool IsRunning =>
        _process is { HasExited: false };

    /// <summary>
    /// 尝试启动边车。成功返回 true 并设置 <see cref="BaseUri"/>。
    /// </summary>
    public async Task<(bool Ok, string Message)> TryStartAsync(
        string? preferredVtuPath = null,
        CancellationToken cancellationToken = default)
    {
        DisposeProcess();

        var python = FindPython();
        if (python == null)
            return (false, "未找到 Python（请在 HYFEA.Viewer.Py 下创建 .venv 并 pip install）");

        var script = FindViewerScript();
        if (script == null)
            return (false, "未找到 viewer.py（期望 src/HYFEA/HYFEA.Viewer.Py/viewer.py）");

        var vtu = preferredVtuPath ?? Path.Combine(Path.GetTempPath(), "hyfea-last.vtu");
        Port = GetFreeTcpPort();

        var psi = new ProcessStartInfo
        {
            FileName = python,
            Arguments = $"\"{script}\" --port {Port} --vtu \"{vtu}\"",
            WorkingDirectory = Path.GetDirectoryName(script)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            // 切勿 Redirect 却不读：管道缓冲区塞满会卡死 Python → WebView「Connection closed」
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };

        try
        {
            _process = Process.Start(psi);
            if (_process == null)
                return (false, "无法启动 Python 进程");

            _process.EnableRaisingEvents = true;
            _process.Exited += (_, _) =>
            {
                // 便于诊断；UI 侧仍显示 Connection closed
            };
        }
        catch (Exception ex)
        {
            return (false, "启动边车失败：" + ex.Message);
        }

        var ready = await WaitForHttpReadyAsync(Port, TimeSpan.FromSeconds(20), cancellationToken)
            .ConfigureAwait(false);
        if (!ready)
        {
            DisposeProcess();
            return (false, "边车 HTTP 未就绪（超时）。请检查 venv 依赖是否已安装。");
        }

        return (true, $"边车就绪 http://127.0.0.1:{Port}/");
    }

    public static string DefaultVtuPath =>
        Path.Combine(Path.GetTempPath(), "hyfea-last.vtu");

    private static string? FindPython()
    {
        var repoCandidates = new[]
        {
            // From Shell bin/.../net8.0-windows → ../../../../HYFEA.Viewer.Py/.venv/...
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HYFEA.Viewer.Py", ".venv", "Scripts", "python.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "HYFEA", "HYFEA.Viewer.Py", ".venv", "Scripts", "python.exe")),
        };

        foreach (var c in repoCandidates)
        {
            if (File.Exists(c)) return c;
        }

        // Walk up from BaseDirectory looking for HYFEA.Viewer.Py/.venv
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "HYFEA.Viewer.Py", ".venv", "Scripts", "python.exe");
            if (File.Exists(candidate)) return candidate;
            candidate = Path.Combine(dir.FullName, "src", "HYFEA", "HYFEA.Viewer.Py", ".venv", "Scripts", "python.exe");
            if (File.Exists(candidate)) return candidate;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var part in pathEnv.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            var candidate = Path.Combine(part.Trim(), "python.exe");
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private static string? FindViewerScript()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "HYFEA.Viewer.Py", "viewer.py");
            if (File.Exists(candidate)) return candidate;
            candidate = Path.Combine(dir.FullName, "src", "HYFEA", "HYFEA.Viewer.Py", "viewer.py");
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<bool> WaitForHttpReadyAsync(int port, TimeSpan timeout, CancellationToken ct)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow + timeout;
        var url = $"http://127.0.0.1:{port}/";
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var resp = await client.GetAsync(url, ct).ConfigureAwait(false);
                // Any HTTP response means server is up (even 404/500)
                return true;
            }
            catch
            {
                await Task.Delay(400, ct).ConfigureAwait(false);
            }
        }

        return false;
    }

    private void DisposeProcess()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
            }
        }
        catch
        {
            // ignore
        }
        finally
        {
            _process?.Dispose();
            _process = null;
            Port = 0;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeProcess();
        GC.SuppressFinalize(this);
    }
}
