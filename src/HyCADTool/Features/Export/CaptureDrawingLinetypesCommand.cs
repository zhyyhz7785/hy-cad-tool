using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Domain.ValueObjects.Configuration.Global;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Features.Export.Services;
using HyCADTool.Presentation.ViewModels;
using Newtonsoft.Json;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Export
{
    /// <summary>
    /// <c>hyLtCapture</c>：从当前图形线型表提取定义，写入
    /// <c>%APPDATA%\HyCADTool\hy-linetype-catalog.json</c>，可选另存 <c>.lin</c>，
    /// 并把轻量快照合并进 <c>hy-settings.json</c>（路径 + 线型名列表）。
    /// </summary>
    public sealed class CaptureDrawingLinetypesCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            string appDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HyCADTool");
            Directory.CreateDirectory(appDir);
            string catalogPath = Path.Combine(appDir, "hy-linetype-catalog.json");

            try
            {
                List<LinetypeCatalogItem> items;
                // 命令态下文档已由 AutoCAD 锁定，不再 doc.LockDocument()（个别场景下会 eNotApplicable）。
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    items = LinetypeTableExporter.Collect(tr, db);
                    tr.Commit();
                }

                {
                    string dwgPath = db.Filename;
                    if (string.IsNullOrWhiteSpace(dwgPath))
                        dwgPath = doc.Name;

                    var document = new LinetypeCatalogDocument
                    {
                        CapturedAtIsoUtc = DateTime.UtcNow.ToString("o"),
                        SourceDrawingFile = dwgPath,
                        Items = items
                    };

                    File.WriteAllText(catalogPath, JsonConvert.SerializeObject(document, Formatting.Indented));

                    string linPath = null;
                    var dlg = new System.Windows.Forms.SaveFileDialog
                    {
                        Filter = "线型库|*.lin|所有文件|*.*",
                        Title = "另存线型 .lin（取消则仅写入目录 JSON）",
                        FileName = Path.GetFileNameWithoutExtension(dwgPath) + "_linetypes.lin",
                        OverwritePrompt = true
                    };
                    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK
                        && !string.IsNullOrWhiteSpace(dlg.FileName))
                    {
                        linPath = dlg.FileName;
                        using (var writer = new StreamWriter(linPath, false, System.Text.Encoding.UTF8))
                            LinetypeTableExporter.WriteLin(items, writer);
                    }

                    var snapshot = new LinetypeCatalogSnapshot
                    {
                        CapturedAtIsoUtc = document.CapturedAtIsoUtc,
                        SourceDrawingFile = document.SourceDrawingFile,
                        CatalogJsonPath = catalogPath,
                        LastLinExportPath = linPath ?? string.Empty,
                        Names = items.Select(i => i.Name).ToList()
                    };

                    var settings = SettingsPanelViewModel.Current;
                    if (settings != null)
                        settings.SetLinetypeCatalogSnapshot(snapshot);
                    else
                        ed.WriteMessage("\n[线型] 提示：统一面板未初始化，已跳过写入 hy-settings.json（JSON 与 .lin 已保存）。");

                    ed.WriteMessage($"\n[线型] 已写入目录：{catalogPath}（{items.Count} 条）");
                    if (!string.IsNullOrEmpty(linPath))
                        ed.WriteMessage($"\n[线型] .lin：{linPath}");
                    else
                        ed.WriteMessage("\n[线型] 未另存 .lin；可随时用 hyLtCapture 再次导出。");
                    if (settings != null)
                        ed.WriteMessage("\n[线型] 已更新 hy-settings.json 中的 LinetypeCatalog 快照。");
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[线型] 提取失败：{ex.Message}");
            }
        }
    }
}
