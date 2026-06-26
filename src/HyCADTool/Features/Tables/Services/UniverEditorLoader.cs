using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using HyCADTool.Features.Tables.Presentation;

namespace HyCADTool.Features.Tables.Services
{
    /// <summary>
    /// 动态加载 HyCADTool.UniverEditor.dll（net8.0-windows）并打开 Univer WebView2 编辑器。
    /// </summary>
    public static class UniverEditorLoader
    {
        private const string EditorDllName = "HyCADTool.UniverEditor.dll";
        private const string LauncherTypeName = "HyCADTool.UniverEditor.EditorLauncher";
        private const string ShowMethodName = "Show";
        private const string IsOpenMethodName = "IsOpen";
        private const string ForceResetMethodName = "ForceResetWindowState";
        private const string WarmupMethodName = "WarmupWebView2Environment";
        private const string AppDataKeyNet8Dir = "HyCADTool.UniverEditorLoader.Net8Dir";
        private const string ReCallDepsPathKey = "HyCADTool.ReCall.DependenciesPath";
        private const string ReCallSourceBinPathKey = "HyCADTool.ReCall.SourceBinPath";

        private static Assembly _editorAssembly;
        private static MethodInfo _showMethod;
        private static MethodInfo _isOpenMethod;
        private static MethodInfo _forceResetMethod;
        private static MethodInfo _warmupMethod;
        private static string _net8Dir;
        private static ResolveEventHandler _resolveHandler;
        private static UniverTableEditorHostBridge _pendingHostBridge;

        public static string LastError { get; private set; }

        public static bool TryShow(long ownerHandle, UniverTableEditorHostBridge hostBridge, out string error)
        {
            LastError = null;
            error = null;

            if (hostBridge != null)
                _pendingHostBridge = hostBridge;

            if (!EnsureLoaded())
            {
                error = LastError ?? "Univer 编辑器加载失败。";
                return false;
            }

            if (_pendingHostBridge != null)
            {
                ConfigureHostContext(_pendingHostBridge);
                _pendingHostBridge = null;
            }

            if (TryInvokeShow(ownerHandle, out error))
                return true;

            TryForceResetStaleWindow();
            if (TryInvokeShow(ownerHandle, out error))
                return true;

            error = error ?? "打开 Univer 编辑器失败。";
            return false;
        }

        public static bool TryShow(long ownerHandle, out string error) =>
            TryShow(ownerHandle, null, out error);

        private static readonly HashSet<UniverTableEditorHostBridge> ConfiguredBridges =
            new HashSet<UniverTableEditorHostBridge>();

        private static void ConfigureHostContext(UniverTableEditorHostBridge bridge)
        {
            try
            {
                Type launcherType = _editorAssembly?.GetType(LauncherTypeName);
                Type contextType = _editorAssembly?.GetType("HyCADTool.UniverEditor.UniverEditorHostContext");
                if (launcherType == null || contextType == null)
                    return;

                object context = Activator.CreateInstance(contextType);
                SetDelegate(context, "GetLoadSnapshotJson", (Func<string>)bridge.BuildLoadSnapshotJson);
                SetDelegate(context, "OnCellChanged", (Action<int, int, string>)bridge.OnCellChanged);
                SetDelegate(context, "NewEmptyTable", (Action)bridge.NewEmptyTable);
                SetDelegate(context, "LoadPersonnelSample", (Action)bridge.LoadPersonnelSample);
                SetDelegate(context, "RequestPick", (Action)bridge.RequestPick);
                SetDelegate(context, "RequestPublish", (Action)bridge.RequestPublish);
                SetDelegate(context, "RequestPublishRangeFull", (Action)bridge.RequestPublishRangeFull);
                SetDelegate(context, "RequestPublishRangeContent", (Action)bridge.RequestPublishRangeContent);
                SetDelegate(context, "ExportJsonSnapshot", (Action)bridge.ExportJsonSnapshot);
                SetDelegate(context, "ImportXlsx", (Action)bridge.ImportXlsx);
                SetDelegate(context, "ExportXlsx", (Action)bridge.ExportXlsx);
                SetDelegate(context, "GetSummaryText", (Func<string>)bridge.GetSummaryText);
                SetDelegate(context, "GetStatusMessage", (Func<string>)bridge.GetStatusMessage);
                SetDelegate(context, "OnSnapshotExported", (Action<string, string>)bridge.CompleteExportSnapshot);
                SetDelegate(context, "OnExportError", (Action<string>)bridge.CancelExportPending);
                SetDelegate(context, "BindExportSnapshot", (Action<Action>)bridge.SetExportSnapshotHandler);
                SetDelegate(context, "BindExportForPublish", (Action<Action<string>>)bridge.SetExportForPublishWebHandler);

                bridge.PrepareForCadInteraction = () =>
                {
                    var prepare = launcherType.GetProperty("PrepareForCadInteraction");
                    (prepare?.GetValue(null) as Action)?.Invoke();
                };
                bridge.RestoreAfterCadInteraction = () =>
                {
                    var restore = launcherType.GetProperty("RestoreAfterCadInteraction");
                    (restore?.GetValue(null) as Action)?.Invoke();
                };

                if (ConfiguredBridges.Add(bridge))
                {
                    bridge.RequestExportSnapshot += () =>
                    {
                        var exportProp = launcherType.GetProperty("RequestExportSnapshot");
                        (exportProp?.GetValue(null) as Action)?.Invoke();
                    };

                    bridge.RequestExportSnapshotForPublish += mode =>
                    {
                        var exportProp = launcherType.GetProperty("RequestExportSnapshotForPublish");
                        var handler = exportProp?.GetValue(null) as Action<string>;
                        handler?.Invoke(MapPublishModeToWeb(mode));
                    };

                    bridge.StatusChanged += () =>
                    {
                        var ctx = launcherType.GetProperty("HostContext")?.GetValue(null);
                        ctx?.GetType().GetMethod("NotifyStatusChanged")?.Invoke(ctx, null);
                    };
                }

                MethodInfo notifyGridChanged = contextType.GetMethod("NotifyGridChanged");
                if (notifyGridChanged != null)
                {
                    bridge.SetGridChangedForward(() => notifyGridChanged.Invoke(context, null));
                }

                launcherType.GetProperty("HostContext")?.SetValue(null, context);

                var rebind = launcherType.GetMethod("TryRebindExportSnapshot");
                rebind?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                LastError = $"配置 Univer HostContext 失败: {ex.Message}";
            }
        }

        private static string MapPublishModeToWeb(UniverPublishExportMode mode)
        {
            switch (mode)
            {
                case UniverPublishExportMode.RangeFull:
                    return "full";
                case UniverPublishExportMode.RangeContent:
                    return "content";
                default:
                    return "default";
            }
        }

        private static void SetDelegate(object target, string propertyName, Delegate value)
        {
            target.GetType().GetProperty(propertyName)?.SetValue(target, value);
        }

        private static bool TryInvokeShow(long ownerHandle, out string error)
        {
            error = null;
            try
            {
                object result = _showMethod.Invoke(null, new object[] { ownerHandle });
                bool opened = result is bool b ? b : true;
                if (!opened)
                {
                    error = "打开 Univer 编辑器失败。";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"调用 Univer 编辑器失败: {ex.Message}";
                return false;
            }
        }

        private static void TryForceResetStaleWindow()
        {
            try
            {
                _forceResetMethod?.Invoke(null, null);
            }
            catch
            {
                // ignore stale window reset failure
            }
        }

        public static bool IsOpen()
        {
            if (_isOpenMethod == null)
                return false;

            try
            {
                object result = _isOpenMethod.Invoke(null, null);
                return result is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        private static bool EnsureLoaded()
        {
            if (_showMethod != null)
                return true;

            try
            {
                string execLocation = Assembly.GetExecutingAssembly().Location;
                _editorAssembly =
                    TryLoadFromDirectory(execLocation)
                    ?? TryLoadFromAppDomainDepsPath()
                    ?? TryLoadFromReCallTemp()
                    ?? TryLoadFromDevOutputBin()
                    ?? TryLoadFromSharedNet8Dir();

                if (_editorAssembly == null)
                {
                    LastError =
                        "未找到 HyCADTool.UniverEditor.dll。"
                        + " 请先 dotnet build src\\HyCADTool\\HyCADTool.csproj，"
                        + "再在 AutoCAD 执行 C2 重载（将 net8\\HyCADTool.UniverEditor.dll 复制到 %TEMP%）后 C1。";
                    return false;
                }

                PreloadNativeDependencies();
                Type launcherType = _editorAssembly.GetType(LauncherTypeName);
                if (launcherType == null)
                {
                    LastError = "未找到 Univer EditorLauncher 类型。";
                    return false;
                }

                _showMethod = launcherType.GetMethod(ShowMethodName, BindingFlags.Public | BindingFlags.Static);
                _isOpenMethod = launcherType.GetMethod(IsOpenMethodName, BindingFlags.Public | BindingFlags.Static);
                _forceResetMethod = launcherType.GetMethod(ForceResetMethodName, BindingFlags.Public | BindingFlags.Static);
                _warmupMethod = launcherType.GetMethod(WarmupMethodName, BindingFlags.Public | BindingFlags.Static);
                if (_showMethod == null)
                {
                    LastError = "未找到 Show 方法。";
                    return false;
                }

                TryWarmupWebView2Environment();
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"加载 Univer 编辑器异常: {ex.Message}";
                return false;
            }
        }

        private static Assembly TryLoadFromDirectory(string assemblyLocation)
        {
            try
            {
                if (string.IsNullOrEmpty(assemblyLocation))
                    return null;

                string baseDir = Path.GetDirectoryName(assemblyLocation);
                return string.IsNullOrEmpty(baseDir) ? null : TryLoadFromBaseDir(baseDir);
            }
            catch
            {
                return null;
            }
        }

        private static Assembly TryLoadFromAppDomainDepsPath()
        {
            try
            {
                string depsPath = AppDomain.CurrentDomain.GetData(ReCallDepsPathKey) as string;
                if (string.IsNullOrEmpty(depsPath))
                    return null;

                return TryLoadFromBaseDir(depsPath);
            }
            catch
            {
                return null;
            }
        }

        private static Assembly TryLoadFromDevOutputBin()
        {
            try
            {
                foreach (string baseDir in EnumerateDevOutputCandidates())
                {
                    Assembly asm = TryLoadFromBaseDir(baseDir);
                    if (asm != null)
                        return asm;
                }

                return null;
            }
            catch
            {
                return null;
            }
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

        private static Assembly TryLoadFromReCallTemp()
        {
            try
            {
                string tempBase = Path.Combine(Path.GetTempPath(), "HyCADToolRefactored");
                if (!Directory.Exists(tempBase))
                    return null;

                string[] dirs = Directory.GetDirectories(tempBase);
                Array.Sort(dirs);
                for (int i = dirs.Length - 1; i >= 0; i--)
                {
                    Assembly asm = TryLoadFromBaseDir(dirs[i]);
                    if (asm != null)
                        return asm;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static Assembly TryLoadFromSharedNet8Dir()
        {
            try
            {
                string net8Dir =
                    AppDomain.CurrentDomain.GetData(AppDataKeyNet8Dir) as string
                    ?? AppDomain.CurrentDomain.GetData("HyCADTool.EditorLoader.Net8Dir") as string;
                if (string.IsNullOrEmpty(net8Dir))
                    return null;

                string dllPath = Path.Combine(net8Dir, EditorDllName);
                if (!File.Exists(dllPath))
                    return null;

                return TryLoadFromBaseDir(Path.GetDirectoryName(dllPath));
            }
            catch
            {
                return null;
            }
        }

        private static Assembly TryLoadFromBaseDir(string baseDir)
        {
            try
            {
                string[] searchPaths =
                {
                    Path.Combine(baseDir, "net8", EditorDllName),
                    Path.Combine(baseDir, EditorDllName),
                };

                string dllPath = searchPaths.FirstOrDefault(File.Exists);
                if (dllPath == null)
                    return null;

                _net8Dir = Path.GetDirectoryName(dllPath);
                AppDomain.CurrentDomain.SetData(AppDataKeyNet8Dir, _net8Dir);
                RemoveOldResolveHandler();
                _resolveHandler = ResolveEditorDeps;
                AppDomain.CurrentDomain.AssemblyResolve += _resolveHandler;
                AppDomain.CurrentDomain.SetData("HyCADTool.UniverEditorLoader.ResolveHandler", _resolveHandler);

                PreloadManagedDependencies(_net8Dir);
                AppDomain.CurrentDomain.SetData(
                    "HyCADTool.UniverEditor.WebDistPath",
                    Path.Combine(_net8Dir, "Web", "dist"));
                return Assembly.Load(File.ReadAllBytes(dllPath));
            }
            catch (Exception ex)
            {
                LastError = $"找到 Univer 编辑器 DLL 但加载失败: {ex.Message}";
                return null;
            }
        }

        private static void RemoveOldResolveHandler()
        {
            if (_resolveHandler != null)
            {
                try { AppDomain.CurrentDomain.AssemblyResolve -= _resolveHandler; }
                catch { }
                _resolveHandler = null;
            }

            try
            {
                var prev = AppDomain.CurrentDomain.GetData("HyCADTool.UniverEditorLoader.ResolveHandler") as ResolveEventHandler;
                if (prev != null)
                {
                    AppDomain.CurrentDomain.AssemblyResolve -= prev;
                    AppDomain.CurrentDomain.SetData("HyCADTool.UniverEditorLoader.ResolveHandler", null);
                }
            }
            catch { }
        }

        private static Assembly ResolveEditorDeps(object sender, ResolveEventArgs args)
        {
            try
            {
                if (string.IsNullOrEmpty(_net8Dir))
                {
                    _net8Dir = AppDomain.CurrentDomain.GetData(AppDataKeyNet8Dir) as string;
                    if (string.IsNullOrEmpty(_net8Dir))
                        return null;
                }

                string name = new AssemblyName(args.Name).Name;
                if (string.IsNullOrEmpty(name))
                    return null;

                string dllPath = Path.Combine(_net8Dir, name + ".dll");
                if (!File.Exists(dllPath))
                    return null;

                return Assembly.LoadFrom(dllPath);
            }
            catch
            {
                return null;
            }
        }

        private static void TryWarmupWebView2Environment()
        {
            try
            {
                _warmupMethod?.Invoke(null, null);
            }
            catch
            {
                // warmup is best-effort
            }
        }

        private static void PreloadNativeDependencies()
        {
            const string loader = "WebView2Loader.dll";
            var paths = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrEmpty(_net8Dir))
            {
                paths.Add(Path.Combine(_net8Dir, loader));
                paths.Add(Path.Combine(_net8Dir, "runtimes", "win-x64", "native", loader));
            }

            try
            {
                string nugetPkgDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".nuget", "packages", "microsoft.web.webview2");
                if (Directory.Exists(nugetPkgDir))
                {
                    string[] versions = Directory.GetDirectories(nugetPkgDir);
                    Array.Sort(versions);
                    for (int i = versions.Length - 1; i >= 0; i--)
                        paths.Add(Path.Combine(versions[i], "runtimes", "win-x64", "native", loader));
                }
            }
            catch { }

            foreach (string path in paths)
            {
                if (File.Exists(path))
                {
                    LoadLibrary(path);
                    return;
                }
            }
        }

        private static void PreloadManagedDependencies(string net8Dir)
        {
            if (string.IsNullOrEmpty(net8Dir))
                return;

            string[] webView2Dlls =
            {
                "Microsoft.Web.WebView2.Wpf.dll",
                "Microsoft.Web.WebView2.Core.dll",
            };

            foreach (string dll in webView2Dlls)
            {
                string path = Path.Combine(net8Dir, dll);
                if (File.Exists(path))
                {
                    try { Assembly.LoadFrom(path); } catch { }
                }
            }
        }
    }
}
