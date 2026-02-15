using System;
using System.Windows.Interop;
using HyCADTool.MarkdownEditor.Models;
using HyCADTool.MarkdownEditor.Views;
using Newtonsoft.Json;

namespace HyCADTool.MarkdownEditor
{
    /// <summary>
    /// 编辑器静态入口（供主项目反射调用）
    /// 签名: public static string ShowDialog(string inputJson, long ownerHandle)
    /// </summary>
    public static class EditorLauncher
    {
        private static readonly object _callbackLock = new object();
        private static readonly object _windowLock = new object();
        private static Action<string> _manualSyncCallback;
        private static Action<string> _liveSyncCallback;
        private static EditorWindow _nonModalWindow;

        /// <summary>
        /// 注册编辑窗口运行期间的同步回调（JSON 字符串）。
        /// </summary>
        public static void RegisterSyncCallbacks(Action<string> manualSyncCallback, Action<string> liveSyncCallback)
        {
            lock (_callbackLock)
            {
                _manualSyncCallback = manualSyncCallback;
                _liveSyncCallback = liveSyncCallback;
            }
        }

        /// <summary>
        /// 清理已注册的同步回调，避免跨会话残留。
        /// </summary>
        public static void ClearSyncCallbacks()
        {
            lock (_callbackLock)
            {
                _manualSyncCallback = null;
                _liveSyncCallback = null;
            }
        }

        internal static void RaiseManualSync(string resultJson)
        {
            Action<string> callback;
            lock (_callbackLock)
                callback = _manualSyncCallback;

            try { callback?.Invoke(resultJson); }
            catch { }
        }

        internal static void RaiseLiveSync(string resultJson)
        {
            Action<string> callback;
            lock (_callbackLock)
                callback = _liveSyncCallback;

            try { callback?.Invoke(resultJson); }
            catch { }
        }

        /// <summary>
        /// 显示 Markdown WYSIWYG 编辑器
        /// </summary>
        /// <param name="inputJson">EditorInput 的 JSON 序列化</param>
        /// <param name="ownerHandle">父窗口句柄（0 = 无父窗口）</param>
        /// <returns>EditorResult 的 JSON 序列化</returns>
        public static string ShowDialog(string inputJson, long ownerHandle = 0)
        {
            EditorInput input = ParseInput(inputJson);
            var window = new EditorWindow(input);
            TrySetOwner(window, ownerHandle);
            window.ShowDialog();

            var result = window.Result ?? new EditorResult { Confirmed = false };
            return JsonConvert.SerializeObject(result);
        }

        /// <summary>
        /// 以非模态方式显示编辑器。窗口存在时仅激活，不重复打开。
        /// </summary>
        /// <returns>true = 已打开或激活；false = 打开失败</returns>
        public static bool ShowNonModal(string inputJson, long ownerHandle = 0)
        {
            lock (_windowLock)
            {
                if (_nonModalWindow != null && _nonModalWindow.IsVisible)
                {
                    try
                    {
                        if (_nonModalWindow.WindowState == System.Windows.WindowState.Minimized)
                            _nonModalWindow.WindowState = System.Windows.WindowState.Normal;
                        _nonModalWindow.Activate();
                    }
                    catch
                    {
                        // ignore activate failure
                    }
                    return true;
                }
            }

            try
            {
                EditorInput input = ParseInput(inputJson);
                var window = new EditorWindow(input, isModal: false);
                TrySetOwner(window, ownerHandle);
                window.Closed += OnNonModalWindowClosed;

                lock (_windowLock)
                    _nonModalWindow = window;

                window.Show();
                return true;
            }
            catch
            {
                ClearSyncCallbacks();
                lock (_windowLock)
                    _nonModalWindow = null;
                return false;
            }
        }

        /// <summary>
        /// 当前是否已有非模态编辑器窗口在运行。
        /// </summary>
        public static bool IsNonModalOpen()
        {
            lock (_windowLock)
                return _nonModalWindow != null && _nonModalWindow.IsVisible;
        }

        private static EditorInput ParseInput(string inputJson)
        {
            try
            {
                return JsonConvert.DeserializeObject<EditorInput>(inputJson) ?? new EditorInput();
            }
            catch
            {
                return new EditorInput();
            }
        }

        private static void TrySetOwner(EditorWindow window, long ownerHandle)
        {
            if (window == null || ownerHandle == 0)
                return;

            try
            {
                var helper = new WindowInteropHelper(window);
                helper.Owner = new IntPtr(ownerHandle);
            }
            catch
            {
                // ignore owner setup failure
            }
        }

        private static void OnNonModalWindowClosed(object sender, EventArgs e)
        {
            lock (_windowLock)
            {
                if (ReferenceEquals(_nonModalWindow, sender))
                    _nonModalWindow = null;
            }

            ClearSyncCallbacks();
        }
    }
}
