using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;



namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("ExportEntityPropertiesToCsv")]
        public static void ExportAll()
        {


            string outputPath = @"E:\BaiduSyncdisk\Code\testResult\AutoCAD_Entity_Properties.csv";
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                Dictionary<string, HashSet<(string Name, string Type)>> entityPropertyMap = new Dictionary<string, HashSet<(string, string)>>();

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    foreach (ObjectId id in ms)
                    {
                        if (!(tr.GetObject(id, OpenMode.ForRead) is Entity ent)) continue;

                        string entityType = ent.GetType().Name;
                        if (!entityPropertyMap.ContainsKey(entityType))
                            entityPropertyMap[entityType] = new HashSet<(string, string)>();

                        PropertyInfo[] props = ent.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                        foreach (var prop in props)
                        {
                            if (!prop.CanRead) continue;
                            try
                            {
                                var val = prop.GetValue(ent);
                                entityPropertyMap[entityType].Add((prop.Name, prop.PropertyType.Name));
                            }
                            catch { continue; }
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

                ed.WriteMessage($"\n实体属性已成功导出至: {outputPath}");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[错误] 导出失败: {ex.Message}");
            }
        }
    }
}
