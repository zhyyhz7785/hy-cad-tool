using HyCADTool.App.Bootstrap;
using HyCADTool.Features.Tables.Services;
using HyCADTool.Features.Tables.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Tables.Commands
{
    /// <summary>
    /// N38：打开 Univer WebView2 表格编辑器（TableGrid 桥 + WPF 文件菜单）。
    /// </summary>
    public sealed class OpenUniverTableEditorCommand
    {
        private static UniverTableEditorHostBridge _hostBridge;

        public void Execute()
        {
            if (AcApp.DocumentManager.MdiActiveDocument == null)
                return;

            long ownerHandle = 0;
            try
            {
                ownerHandle = AcApp.MainWindow.Handle.ToInt64();
            }
            catch
            {
                // ignore
            }

            EnsureHostBridge();
            if (!UniverEditorLoader.TryShow(ownerHandle, _hostBridge, out string error))
            {
                AcApp.DocumentManager.MdiActiveDocument?.Editor
                    ?.WriteMessage($"\n[HyTable Univer] 打开失败: {error}");
            }
        }

        private static void EnsureHostBridge()
        {
            if (_hostBridge != null)
                return;

            var viewModel = ServiceLocator.TryResolve<TableEditorViewModel>()
                ?? new TableEditorViewModel();
            _hostBridge = new UniverTableEditorHostBridge(viewModel);
        }
    }
}
