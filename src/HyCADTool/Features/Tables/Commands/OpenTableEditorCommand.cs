using System.Windows.Interop;
using HyCADTool.App.Bootstrap;
using HyCADTool.Features.Tables.ViewModels;
using HyCADTool.Features.Tables.Views;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Tables.Commands
{
    /// <summary>
    /// N37 / HYTB：打开独立表格编辑器窗口（018 M0）。
    /// </summary>
    public sealed class OpenTableEditorCommand
    {
        private static TableEditorWindow _currentWindow;

        public void Execute()
        {
            if (AcApp.DocumentManager.MdiActiveDocument == null)
                return;

            if (_currentWindow != null)
            {
                try
                {
                    if (_currentWindow.IsVisible)
                    {
                        if (_currentWindow.WindowState == System.Windows.WindowState.Minimized)
                            _currentWindow.WindowState = System.Windows.WindowState.Normal;

                        _currentWindow.Topmost = true;
                        _currentWindow.Activate();
                        return;
                    }
                }
                catch
                {
                    _currentWindow = null;
                }
            }

            TableEditorViewModel viewModel = ServiceLocator.TryResolve<TableEditorViewModel>()
                ?? new TableEditorViewModel();

            var window = new TableEditorWindow(viewModel);
            window.Closed += (_, __) =>
            {
                if (ReferenceEquals(_currentWindow, window))
                    _currentWindow = null;
            };

            _currentWindow = window;
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
}