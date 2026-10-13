using HyCADTool.App.Bootstrap;
using HyCADTool.Features.Tables.Services;
using HyCADTool.Features.Tables.ViewModels;
using HyCADTool.Shared.AutoCAD.Session;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Tables.Commands
{
    /// <summary>
    /// N38：打开 Univer WebView2 表格编辑器（TableGrid 桥 + WPF 文件菜单）。
    /// </summary>
    public sealed class OpenUniverTableEditorCommand
    {
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

            var session = ActiveDocumentSession.GetOrNull<UniverTableHostSession>();
            if (session == null)
                return;

            if (session.HostBridge == null)
            {
                var viewModel = ServiceLocator.TryResolve<TableEditorViewModel>()
                    ?? new TableEditorViewModel();
                session.HostBridge = new UniverTableEditorHostBridge(viewModel);
            }

            if (!UniverEditorLoader.TryShow(ownerHandle, session.HostBridge, out string error))
            {
                AcApp.DocumentManager.MdiActiveDocument?.Editor
                    ?.WriteMessage($"\n[HyTable Univer] 打开失败: {error}");
            }
        }
    }

    /// <summary>单张图上的 Univer 桥接槽，由 <see cref="DocumentSessionStore"/> 按 Database 持有。</summary>
    public sealed class UniverTableHostSession
    {
        public UniverTableEditorHostBridge HostBridge;
    }
}
