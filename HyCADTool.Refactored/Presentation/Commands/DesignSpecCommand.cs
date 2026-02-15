using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.Models.Text;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using LayoutResultModel = HyCADTool.TextLayout.LayoutResult;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// Markdown 设计说明排版命令 (hymd)
    /// 仅使用 WebView2 编辑器（net8.0）
    /// </summary>
    public class DesignSpecCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            string[] columnContents = null;
            string[] columnMarkdowns = null;
            string markdownSource = null;
            DesignSpecConfig config = null;
            LayoutResultModel layoutResult = null;

            // 尝试 WebView2 编辑器（net8.0 项目）
            long ownerHandle = 0;
            try { ownerHandle = AcApp.MainWindow.Handle.ToInt64(); } catch { }

            bool useModernEditor = EditorLoader.TryShowEditor(
                null, null, ownerHandle,
                out columnContents, out columnMarkdowns, out markdownSource, out config, out layoutResult);

            if (!useModernEditor)
            {
                ed.WriteMessage("\nWebView2 编辑器不可用，请安装/修复 WebView2 Runtime 后重试。");
                return;
            }

            if (columnContents == null || columnContents.Length == 0)
            {
                ed.WriteMessage("\n已取消。");
                return;
            }

            // 选择插入点
            var ptRes = ed.GetPoint("\n请选择设计说明左上角插入点：");
            if (ptRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n已取消。");
                return;
            }

            try
            {
                var service = new DesignSpecService();
                service.Insert(columnContents, markdownSource, config, ptRes.Value, columnMarkdowns, layoutResult);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n插入失败: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
