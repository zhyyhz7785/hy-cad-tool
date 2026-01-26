using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
[assembly: CommandClass(typeof(HyCADTool.Commands.HyCommand))]
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("hyex_MarkdownTable")]
        public static void ExportEntityProperties()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Entity entity = Tools.ZTools.SelectSingleEntity();
            if (entity == null)
            {
                ed.WriteMessage("\nOperation canceled.");
                return;
            }
            //var properties = GetEntityProperties(entity);
            var properties = entity.GetLayerProperties();
            string markdown = GenerateMarkdownTable(entity.GetType().Name, properties);
            OutputToFile(markdown, @"E:\BaiduSyncdisk\Code\testResult", ed);
        }
        //private static Dictionary<string, (string Value, string Type)> GetEntityProperties(Entity entity)
        //{
        //    var properties = new Dictionary<string, (string Value, string Type)>();
        //    foreach (var prop in entity.GetType().GetProperties())
        //    {
        //        try
        //        {
        //            var value = prop.GetValue(entity, null);
        //            string valueType = prop.PropertyType.Name;
        //            properties[prop.Name] = (value != null ? value.ToString() : "null", valueType);
        //        }
        //        catch (Exception ex)
        //        {
        //            properties[prop.Name] = ($"Error retrieving value ({ex.Message})", prop.PropertyType.Name);
        //        }
        //    }
        //    return properties;
        //}
        private static string GenerateMarkdownTable(string entityType, Dictionary<string, (string Value, string Type)> properties)
        {
            StringBuilder sb = new StringBuilder();
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
        private static void OutputToFile(string content, string directory, Editor ed)
        {
            // 获取当前时间并格式化为时间戳
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            // 确保目录存在
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            // 生成文件路径
            string filePath = Path.Combine(directory, $"EntityProperties_{timestamp}.md");
            File.WriteAllText(filePath, content);
            ed.WriteMessage($"\nEntity properties exported to {filePath}");
        }
    }
}
