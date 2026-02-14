using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.Models.Text;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 二次编辑 Markdown 设计说明命令 (hymdE，仅 WebView2 编辑器)
    /// </summary>
    public class DesignSpecEditCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            var selOpts = new PromptEntityOptions("\n请选择要编辑的 MText 设计说明：");
            selOpts.SetRejectMessage("\n请选择 MText 实体。");
            selOpts.AddAllowedClass(typeof(MText), true);
            var selRes = ed.GetEntity(selOpts);
            if (selRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n已取消。");
                return;
            }

            ObjectId mtextId = selRes.ObjectId;
            string markdownSource = null;
            DesignSpecConfig existingConfig = null;

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var mtext = tr.GetObject(mtextId, OpenMode.ForRead) as MText;
                if (mtext == null)
                {
                    ed.WriteMessage("\n所选实体不是 MText。");
                    tr.Commit();
                    return;
                }
                if (!DesignSpecService.HasMarkdownSource(tr, mtext))
                {
                    ed.WriteMessage("\n此 MText 不含 Markdown 源码。");
                    tr.Commit();
                    return;
                }
                markdownSource = DesignSpecService.ReadMarkdownSource(tr, mtext);
                existingConfig = DesignSpecService.ReadConfig(tr, mtext);
                tr.Commit();
            }

            if (string.IsNullOrEmpty(markdownSource))
            {
                ed.WriteMessage("\nMarkdown 源码为空。");
                return;
            }

            string[] columnContents = null;
            string newMarkdownSource = null;
            DesignSpecConfig newConfig = null;

            // 尝试 WebView2 编辑器
            long ownerHandle = 0;
            try { ownerHandle = AcApp.MainWindow.Handle.ToInt64(); } catch { }

            bool useModernEditor = EditorLoader.TryShowEditor(
                markdownSource, existingConfig, ownerHandle,
                out columnContents, out newMarkdownSource, out newConfig);

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

            try
            {
                var service = new DesignSpecService();
                service.Update(mtextId, columnContents, newMarkdownSource, newConfig);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n更新失败: {ex.Message}");
            }
        }
    }
}
