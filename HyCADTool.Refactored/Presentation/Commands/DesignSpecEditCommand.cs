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

            ObjectId anchorEntityId = ObjectId.Null;
            string markdownSource = null;
            DesignSpecConfig existingConfig = null;

            while (true)
            {
                var selOpts = new PromptEntityOptions("\n请选择要编辑的设计说明实体（MText/表格）：");
                selOpts.SetRejectMessage("\n请选择 MText 或 Table 实体。");
                selOpts.AddAllowedClass(typeof(MText), true);
                selOpts.AddAllowedClass(typeof(Table), true);
                var selRes = ed.GetEntity(selOpts);
                if (selRes.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n已取消。");
                    return;
                }

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var entity = tr.GetObject(selRes.ObjectId, OpenMode.ForRead) as Entity;
                    if (entity == null)
                    {
                        ed.WriteMessage("\n所选实体无效。");
                        tr.Commit();
                        continue;
                    }

                    string groupId = ExtensionDictionaryService.ReadLongString(tr, entity, "HyDesignSpec_Group");
                    bool hasMarkdown = DesignSpecService.HasMarkdownSource(tr, entity);

                    if (hasMarkdown)
                    {
                        markdownSource = DesignSpecService.ReadMarkdownSource(tr, entity);
                        existingConfig = DesignSpecService.ReadConfig(tr, entity);
                        anchorEntityId = entity.ObjectId;
                        tr.Commit();
                        break;
                    }

                    if (!string.IsNullOrEmpty(groupId))
                    {
                        bool resolved = TryResolveMarkdownFromGroup(
                            tr,
                            doc.Database,
                            groupId,
                            out markdownSource,
                            out existingConfig,
                            out ObjectId sourceEntityId);

                        if (resolved && !string.IsNullOrEmpty(markdownSource))
                        {
                            // 仍以用户当前选择实体为定位基准进行更新
                            anchorEntityId = entity.ObjectId;
                            tr.Commit();
                            break;
                        }
                    }

                    ed.WriteMessage("\n此实体不含 Markdown 源码，请重新选择（ESC 取消）。");
                    tr.Commit();
                    continue;
                }
            }

            if (string.IsNullOrEmpty(markdownSource))
            {
                ed.WriteMessage("\nMarkdown 源码为空。");
                return;
            }

            string[] columnContents = null;
            string[] columnMarkdowns = null;
            string newMarkdownSource = null;
            DesignSpecConfig newConfig = null;

            // 尝试 WebView2 编辑器
            long ownerHandle = 0;
            try { ownerHandle = AcApp.MainWindow.Handle.ToInt64(); } catch { }

            bool useModernEditor = EditorLoader.TryShowEditor(
                markdownSource, existingConfig, ownerHandle,
                out columnContents, out columnMarkdowns, out newMarkdownSource, out newConfig);

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
                service.Update(anchorEntityId, columnContents, newMarkdownSource, newConfig, columnMarkdowns);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n更新失败: {ex.Message}");
            }
        }

        private static bool TryResolveMarkdownFromGroup(
            Transaction tr,
            Database db,
            string groupId,
            out string markdownSource,
            out DesignSpecConfig config,
            out ObjectId sourceEntityId)
        {
            markdownSource = null;
            config = null;
            sourceEntityId = ObjectId.Null;

            if (string.IsNullOrEmpty(groupId))
                return false;

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in btr)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null)
                    continue;

                string gid = ExtensionDictionaryService.ReadLongString(tr, ent, "HyDesignSpec_Group");
                if (gid != groupId)
                    continue;

                if (!DesignSpecService.HasMarkdownSource(tr, ent))
                    continue;

                string source = DesignSpecService.ReadMarkdownSource(tr, ent);
                if (string.IsNullOrEmpty(source))
                    continue;

                markdownSource = source;
                config = DesignSpecService.ReadConfig(tr, ent);
                sourceEntityId = id;
                return true;
            }

            return false;
        }
    }
}
