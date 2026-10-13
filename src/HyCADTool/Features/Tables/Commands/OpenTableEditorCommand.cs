using System;
using System.Windows.Interop;
using HyCADTool.App.Bootstrap;
using HyCADTool.Features.Tables.ViewModels;
using HyCADTool.Features.Tables.Views;
using HyCADTool.Shared.AutoCAD.Session;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Tables.Commands
{
    /// <summary>
    /// N37 / HYTB：打开独立表格编辑器窗口（018 M0）。
    /// </summary>
    public sealed class OpenTableEditorCommand
    {
        public void Execute()
        {
            if (AcApp.DocumentManager.MdiActiveDocument == null)
                return;

            var session = ActiveDocumentSession.GetOrNull<TableEditorWindowSession>();
            if (session == null)
                return;

            if (session.CurrentWindow != null)
            {
                try
                {
                    if (session.CurrentWindow.IsVisible)
                    {
                        if (session.CurrentWindow.WindowState == System.Windows.WindowState.Minimized)
                            session.CurrentWindow.WindowState = System.Windows.WindowState.Normal;

                        session.CurrentWindow.Topmost = true;
                        session.CurrentWindow.Activate();
                        return;
                    }
                }
                catch
                {
                    session.CurrentWindow = null;
                }
            }

            TableEditorViewModel viewModel = ServiceLocator.TryResolve<TableEditorViewModel>()
                ?? new TableEditorViewModel();

            var window = new TableEditorWindow(viewModel);
            window.Closed += (_, __) =>
            {
                if (ReferenceEquals(session.CurrentWindow, window))
                    session.CurrentWindow = null;
            };

            session.CurrentWindow = window;
            AttachToAutoCad(window);
            window.Show();
        }

        private static void AttachToAutoCad(TableEditorWindow window)
        {
            window.Topmost = true;
            try
            {
                new WindowInteropHelper(window) { Owner = AcApp.MainWindow.Handle };
            }
            catch
            {
                // 无宿主仍允许 Show
            }
        }
    }

    /// <summary>单张图上的表格窗口槽，文档关闭时关掉窗口。</summary>
    public sealed class TableEditorWindowSession : IDisposable
    {
        public TableEditorWindow CurrentWindow;

        public void Dispose()
        {
            try { CurrentWindow?.Close(); }
            catch { }
            CurrentWindow = null;
        }
    }
}
