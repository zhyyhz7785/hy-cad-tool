using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Shared.AutoCAD.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Export
{
    /// <summary>
    /// 选择单个实体，将其图层属性导出为 Markdown 表格文件
    /// </summary>
    public class ExportMarkdownTableCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            // 选择实体
            var result = ed.GetEntity("\n选择要导出属性的实体: ");
            if (result.Status != PromptStatus.OK) return;

            try
            {
                Entity entity;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    entity = tr.GetObject(result.ObjectId, OpenMode.ForRead) as Entity;
                    if (entity == null) return;

                    var properties = GetLayerProperties(entity, db, tr);
                    string entityType = entity.GetType().Name;
                    string markdown = GenerateMarkdownTable(entityType, properties);

                    tr.Commit();

                    // 选择保存路径
                    var saveDialog = new System.Windows.Forms.SaveFileDialog
                    {
                        Filter = "Markdown 文件|*.md",
                        Title = "选择导出路径",
                        FileName = $"EntityProperties_{DateTime.Now:yyyyMMddHHmmss}.md"
                    };
                    if (saveDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                    File.WriteAllText(saveDialog.FileName, markdown);
                    ed.WriteMessage($"\n已导出至: {saveDialog.FileName}");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n导出失败: {ex.Message}");
            }
        }

        /// <summary>获取实体所在图层的属性</summary>
        private Dictionary<string, (string Value, string Type)> GetLayerProperties(
            Entity entity, Database db, Transaction tr)
        {
            var properties = new Dictionary<string, (string Value, string Type)>();
            string layerName = entity.Layer;

            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!layerTable.Has(layerName)) return properties;

            var layerRecord = (LayerTableRecord)tr.GetObject(layerTable[layerName], OpenMode.ForRead);
            foreach (var prop in layerRecord.GetType().GetProperties())
            {
                try
                {
                    var value = prop.GetValue(layerRecord, null);
                    properties[prop.Name] = (value?.ToString() ?? "null", prop.PropertyType.Name);
                }
                catch (System.Exception ex)
                {
                    properties[prop.Name] = ($"Error: {ex.Message}", prop.PropertyType.Name);
                }
            }

            return properties;
        }

        private string GenerateMarkdownTable(string entityType, Dictionary<string, (string Value, string Type)> properties)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Entity Properties");
            sb.AppendLine($"**EntityType**: {entityType}");
            sb.AppendLine();
            sb.AppendLine("| Property | Type | Value |");
            sb.AppendLine("|----------|------|-------|");
            foreach (var prop in properties)
            {
                sb.AppendLine($"| {prop.Key} | {prop.Value.Type} | {prop.Value.Value} |");
            }
            return sb.ToString();
        }
    }
}
