using System;
using System.Windows;
using System.Windows.Interop;
using HyCADTool.UniverEditor.Services;
using HyCADTool.UniverEditor.Views;

namespace HyCADTool.UniverEditor
{
    /// <summary>
    /// Univer 表格编辑器静态入口（供主项目反射调用）。
    /// 签名: public static bool Show(long ownerHandle)
    /// </summary>
    public static class EditorLauncher
    {
        private static readonly object WindowLock = new object();
        private static readonly object DelegateLock = new object();
        private static UniverEditorWindow _currentWindow;

        public static UniverEditorHostContext HostContext { get; set; }

        private static Action _requestExportSnapshot;
        private static Action<string> _requestExportSnapshotForPublish;
        private static Action _prepareForCadInteraction;
        private static Action _restoreAfterCadInteraction;

        /// <summary>由窗口在 Session 就绪后注入，供 HyCAD 桥接触发 exportSnapshot。</summary>
        public static Action RequestExportSnapshot
        {
            get { lock (DelegateLock) return _requestExportSnapshot; }
            set { lock (DelegateLock) _requestExportSnapshot = value; }
        }

        public static Action<string> RequestExportSnapshotForPublish
        {
            get { lock (DelegateLock) return _requestExportSnapshotForPublish; }
            set { lock (DelegateLock) _requestExportSnapshotForPublish = value; }
        }

        /// <summary>窗口注入：落图/拾取前让出前台（Topmost=false）。</summary>
        public static Action PrepareForCadInteraction
        {
            get { lock (DelegateLock) return _prepareForCadInteraction; }
            set { lock (DelegateLock) _prepareForCadInteraction = value; }
        }

        /// <summary>窗口注入：CAD 交互结束后恢复窗口。</summary>
        public static Action RestoreAfterCadInteraction
        {
            get { lock (DelegateLock) return _restoreAfterCadInteraction; }
            set { lock (DelegateLock) _restoreAfterCadInteraction = value; }
        }

        public static bool IsOpen()
        {
            lock (WindowLock)
            {
                try
                {
                    return _currentWindow != null && _currentWindow.IsVisible;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static void WarmupWebView2Environment()
        {
            WebView2EnvironmentProvider.Warmup();
        }

        public static bool Show(long ownerHandle = 0)
        {
            lock (WindowLock)
            {
                if (_currentWindow != null)
                {
                    try
                    {
                        if (_currentWindow.IsVisible)
                        {
                            if (_currentWindow.WindowState == WindowState.Minimized)
                                _currentWindow.WindowState = WindowState.Normal;

                            _currentWindow.Topmost = true;
                            _currentWindow.Activate();
                            TryRebindExportSnapshot();
                            return true;
                        }

                        TrySetOwner(_currentWindow, ownerHandle);
                        _currentWindow.Show();
                        if (_currentWindow.WindowState == WindowState.Minimized)
                            _currentWindow.WindowState = WindowState.Normal;

                        _currentWindow.Topmost = true;
                        _currentWindow.Activate();
                        TryRebindExportSnapshot();
                        return true;
                    }
                    catch
                    {
                        // 显示已存在窗口失败，清理并在下面重新创建
                        try
                        {
                            _currentWindow.Close();
                        }
                        catch
                        {
                            // 忽略关闭失败
                        }
                        _currentWindow = null;
                    }
                }

                UniverEditorWindow window = null;
                try
                {
                    window = new UniverEditorWindow(HostContext ?? new UniverEditorHostContext());
                    window.Closed += (_, __) =>
                    {
                        lock (WindowLock)
                        {
                            if (ReferenceEquals(_currentWindow, window))
                                _currentWindow = null;
                        }
                    };

                    _currentWindow = window;
                    TrySetOwner(window, ownerHandle);
                    window.Show();
                    return true;
                }
                catch
                {
                    // 新建窗口失败，清理资源
                    if (window != null)
                    {
                        try
                        {
                            window.Closed -= (_, __) => { };
                            window.Close();
                        }
                        catch
                        {
                            // 忽略清理失败
                        }
                    }
                    _currentWindow = null;
                    return false;
                }
            }
        }

        public static void ForceResetWindowState()
        {
            lock (WindowLock)
            {
                if (_currentWindow == null)
                    return;

                try
                {
                    _currentWindow.ForceClose();
                }
                catch
                {
                    // ignore
                }

                _currentWindow = null;
            }
        }

        /// <summary>C2 重载后 HostContext 更新时，重新绑定 exportSnapshot。</summary>
        public static void TryRebindExportSnapshot()
        {
            lock (WindowLock)
            {
                _currentWindow?.SyncHostContextBindings();
                _currentWindow?.RebindExportSnapshot();
            }
        }

        private static void TrySetOwner(Window window, long ownerHandle)
        {
            window.Topmost = true;

            // 验证句柄：0 表示无父窗口，负数和其他无效值拒绝
            if (ownerHandle <= 0)
                return;

            try
            {
                new WindowInteropHelper(window) { Owner = new IntPtr(ownerHandle) };
            }
            catch
            {
                // 无效句柄或其他错误，仍允许 Show（保持 Topmost）
            }
        }
    }
}
