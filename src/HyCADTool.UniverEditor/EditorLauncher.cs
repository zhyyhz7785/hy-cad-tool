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

        private static UniverEditorWindow _currentWindow;

        public static UniverEditorHostContext HostContext { get; set; }

        /// <summary>由窗口在 Session 就绪后注入，供 HyCAD 桥接触发 exportSnapshot。</summary>
        public static Action RequestExportSnapshot { get; set; }



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

                            return true;

                        }



                        TrySetOwner(_currentWindow, ownerHandle);

                        _currentWindow.Show();

                        if (_currentWindow.WindowState == WindowState.Minimized)

                            _currentWindow.WindowState = WindowState.Normal;



                        _currentWindow.Topmost = true;

                        _currentWindow.Activate();

                        return true;

                    }

                    catch

                    {

                        _currentWindow = null;

                    }

                }



                var window = new UniverEditorWindow(HostContext ?? new UniverEditorHostContext());

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



        private static void TrySetOwner(Window window, long ownerHandle)

        {

            window.Topmost = true;

            if (ownerHandle == 0)

                return;



            try

            {

                new WindowInteropHelper(window) { Owner = new IntPtr(ownerHandle) };

            }

            catch

            {

                // 无宿主仍允许 Show

            }

        }

    }

}


