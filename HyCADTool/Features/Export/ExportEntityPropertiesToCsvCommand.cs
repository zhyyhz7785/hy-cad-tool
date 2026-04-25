using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Export
{
    /// <summary>
    /// 导出模型空间所有实体的属性到 CSV 文件
    /// </summary>
    public class ExportEntityPropertiesToCsvCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            // 让用户选择保存路径
            var saveDialog = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "CSV 文件|*.csv",
                Title = "选择导出路径",
                FileName = "AutoCAD_Entity_Properties.csv"
            };
            if (saveDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            string outputPath = saveDialog.FileName;

            try
            {
                var entityPropertyMap = new Dictionary<string, HashSet<(string Name, string Type)>>();

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    foreach (ObjectId id in ms)
                    {
                        if (!(tr.GetObject(id, OpenMode.ForRead) is Entity ent)) continue;

                        string entityType = ent.GetType().Name;
                        if (!entityPropertyMap.ContainsKey(entityType))
                            entityPropertyMap[entityType] = new HashSet<(string, string)>();

                        var props = ent.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                        foreach (var prop in props)
                        {
                            if (!prop.CanRead) continue;
                            try
                            {
                                prop.GetValue(ent);
                                entityPropertyMap[entityType].Add((prop.Name, prop.PropertyType.Name));
                            }
                            catch { }
                        }
                    }

                    tr.Commit();
                }

                using (var writer = new StreamWriter(outputPath))
                {
                    writer.WriteLine("EntityType,PropertyName,PropertyType");
                    foreach (var kv in entityPropertyMap.OrderBy(k => k.Key))
                    {
                        foreach (var prop in kv.Value.OrderBy(p => p.Name))
                        {
                            writer.WriteLine($"{kv.Key},{prop.Name},{prop.Type}");
                        }
                    }
                }

                ed.WriteMessage($"\n已导出至: {outputPath}");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n导出失败: {ex.Message}");
            }
        }
    }
}
