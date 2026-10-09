using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Features.DesignSpec.Domain.Models;
using HyCADTool.Features.DesignSpec.Services;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Export
{
    /// <summary>
    /// hymdE：点选贴到图纸的文字/表格，把 Markdown 源导入 HyMd 编辑（原 hymde 二次编辑）。
    /// </summary>
    public class DesignSpecEditCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            HyMdCadBridge.Start();
            if (!HyMdCadBridge.EnsureProcess(ed))
                return;

            var peo = new PromptEntityOptions("\n选择要导入 HyMd 编辑的设计说明：");
            peo.AllowNone = false;
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
                return;

            string markdown = null;
            DesignSpecConfig config = null;
            using (doc.LockDocument())
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Entity;
                if (ent == null)
                {
                    ed.WriteMessage("\n选中的对象无效。");
                    return;
                }
                DesignSpecService.TryReadHymdSource(tr, doc.Database, ent, out markdown, out config);
                tr.Commit();
            }

            string sourcePath = config != null ? config.SourcePath : null;
            if (string.IsNullOrWhiteSpace(markdown) && string.IsNullOrWhiteSpace(sourcePath))
            {
                ed.WriteMessage("\n选中的图元没有 HyMd 源码。请选择「贴到图纸」插入的文字或表格。");
                return;
            }

            string dwgPath = null;
            try { dwgPath = doc.Name; } catch { }
            double scale = config != null && config.Scale > 0 ? config.Scale : HyMdCadBridge.LastScale;
            string styleName = config != null && !string.IsNullOrWhiteSpace(config.TextStyleName)
                ? config.TextStyleName
                : HyMdCadBridge.LastStyleName;

            HyMdCadBridge.SendOpen(sourcePath, markdown, dwgPath, scale, styleName);
            ed.WriteMessage("\n已导入 HyMd。改完后在编辑器里再点「贴到图纸」。");
        }
    }
}
