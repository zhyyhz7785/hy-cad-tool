using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Models.Text;
using HyCADTool.Shell.Commands;
using HyCADTool.Features.DesignSpec.Services;
using HyCADTool.Shared.AutoCAD.Services;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using LayoutResultModel = HyCADTool.TextLayout.LayoutResult;

namespace HyCADTool.Features.Export
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
            var service = new DesignSpecService();

            ObjectId anchorEntityId = ObjectId.Null;
            bool hasInserted = false;
            string lastSyncHash = null;
            bool insertionPointSelected = false;
            Point3d insertionPoint = Point3d.Origin;

            // 尝试 WebView2 编辑器（net8.0 项目）
            long ownerHandle = 0;
            try { ownerHandle = AcApp.MainWindow.Handle.ToInt64(); } catch { }

            void ApplyToCad(
                string[] syncColumnContents,
                string[] syncColumnMarkdowns,
                string syncMarkdownSource,
                DesignSpecConfig syncConfig,
                LayoutResultModel syncLayoutResult,
                bool allowPickInsertionPoint)
            {
                if (syncColumnContents == null || syncColumnContents.Length == 0 || syncConfig == null)
                    return;

                string syncHash = BuildSyncHash(syncMarkdownSource, syncConfig);
                if (syncHash == lastSyncHash)
                    return;

                try
                {
                    if (!hasInserted || anchorEntityId.IsNull)
                    {
                        if (!insertionPointSelected)
                        {
                            if (!allowPickInsertionPoint)
                                return;

                            var ptRes = ed.GetPoint("\n请选择设计说明左上角插入点：");
                            if (ptRes.Status != PromptStatus.OK)
                            {
                                ed.WriteMessage("\n已取消插入点选择，本次未同步。");
                                return;
                            }

                            insertionPoint = ptRes.Value;
                            insertionPointSelected = true;
                        }

                        anchorEntityId = service.Insert(
                            syncColumnContents,
                            syncMarkdownSource,
                            syncConfig,
                            insertionPoint,
                            syncColumnMarkdowns,
                            syncLayoutResult);
                        hasInserted = !anchorEntityId.IsNull;
                    }
                    else
                    {
                        var newAnchor = service.Update(
                            anchorEntityId,
                            syncColumnContents,
                            syncMarkdownSource,
                            syncConfig,
                            syncColumnMarkdowns,
                            syncLayoutResult);
                        if (!newAnchor.IsNull)
                            anchorEntityId = newAnchor;
                    }

                    lastSyncHash = syncHash;
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n同步失败: {ex.Message}");
                }
            }

            void ApplyManualToCad(
                string[] syncColumnContents,
                string[] syncColumnMarkdowns,
                string syncMarkdownSource,
                DesignSpecConfig syncConfig,
                LayoutResultModel syncLayoutResult)
            {
                ApplyToCad(syncColumnContents, syncColumnMarkdowns, syncMarkdownSource, syncConfig, syncLayoutResult, true);
            }

            void ApplyLiveToCad(
                string[] syncColumnContents,
                string[] syncColumnMarkdowns,
                string syncMarkdownSource,
                DesignSpecConfig syncConfig,
                LayoutResultModel syncLayoutResult)
            {
                ApplyToCad(syncColumnContents, syncColumnMarkdowns, syncMarkdownSource, syncConfig, syncLayoutResult, false);
            }

            bool useModernEditor = EditorLoader.TryShowEditorNonModal(
                null,
                null,
                ownerHandle,
                ApplyManualToCad,
                ApplyLiveToCad);

            if (!useModernEditor)
            {
                ed.WriteMessage("\nWebView2 编辑器不可用，请安装/修复 WebView2 Runtime 后重试。");
                if (!string.IsNullOrWhiteSpace(EditorLoader.LastError))
                    ed.WriteMessage($"\n详细原因: {EditorLoader.LastError}");
                return;
            }
        }

        private static string BuildSyncHash(string markdownSource, DesignSpecConfig config)
        {
            string cfgJson;
            try
            {
                cfgJson = Newtonsoft.Json.JsonConvert.SerializeObject(config);
            }
            catch
            {
                cfgJson = string.Empty;
            }

            return (markdownSource ?? string.Empty) + "\n@@CFG@@\n" + cfgJson;
        }
    }
}
