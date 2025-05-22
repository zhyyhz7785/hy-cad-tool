using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace HyCADTool.Tools
{
    public static partial class Et
    {
        public static Dictionary<string, (string Value, string Type)> GetEntityProperties(this Entity entity)
        {
            var properties = new Dictionary<string, (string Value, string Type)>();
            foreach (var prop in entity.GetType().GetProperties())
            {
                try
                {
                    var value = prop.GetValue(entity, null);
                    string valueType = prop.PropertyType.Name;
                    properties[prop.Name] = (value != null ? value.ToString() : "null", valueType);
                }
                catch (System.Exception ex)
                {
                    properties[prop.Name] = ($"Error retrieving value ({ex.Message})", prop.PropertyType.Name);
                }
            }
            return properties;
        }
        public static Dictionary<string, (string Value, string Type)> GetLayerProperties(this Entity entity)
        {
            var properties = new Dictionary<string, (string Value, string Type)>();
            try
            {
                // 获取图层名
                string layerName = entity.Layer;
                // 获取数据库对象
                Database db = entity.Database;
                // 开启事务处理
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    // 获取图层表
                    LayerTable layerTable = (LayerTable)trans.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (layerTable.Has(layerName))
                    {
                        // 获取指定图层
                        LayerTableRecord layerRecord = (LayerTableRecord)trans.GetObject(layerTable[layerName], OpenMode.ForRead);
                        foreach (var prop in layerRecord.GetType().GetProperties())
                        {
                            try
                            {
                                var value = prop.GetValue(layerRecord, null);
                                string valueType = prop.PropertyType.Name;
                                properties[prop.Name] = (value != null ? value.ToString() : "null", valueType);
                            }
                            catch (System.Exception ex)
                            {
                                properties[prop.Name] = ($"Error retrieving value ({ex.Message})", prop.PropertyType.Name);
                            }
                        }
                    }
                    // 提交事务
                    trans.Commit();
                }
            }
            catch (System.Exception ex)
            {
                properties["Error"] = ($"Error retrieving layer properties: {ex.Message}", "Exception");
            }
            return properties;
        }
        ///类型属性相关操作
        /// <summary>
        /// 在数据库中修改实体属性。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="ent">要修改的实体。</param>
        /// <param name="act">修改操作。</param>
        /// <param name="db">数据库对象（可选）。</param>
        /// <param name="space">空间（可选）。</param>
        public static void ChangeEntityPropertyInDb<T>(this T ent, Action<T> act, Database db = null) where T : Entity
        {
            // 如果未提供数据库对象，则使用当前活动文档的数据库
            if (db == null)
            {
                db = Application.DocumentManager.MdiActiveDocument.Database;
            }
            if (ent == null)
            {
                throw new ArgumentNullException(nameof(ent), "实体不能为空。");
            }
            if (act == null)
            {
                throw new ArgumentNullException(nameof(act), "操作不能为空。");
            }
            var doc = Application.DocumentManager.MdiActiveDocument;
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (var trans = db.TransactionManager.StartTransaction())
                {
                    // 获取实体对象并设置为写模式
                    var obj = trans.GetObject(ent.Id, OpenMode.ForWrite) as T;
                    if (obj != null)
                    {
                        // 执行传入的操作
                        act(obj);
                    }
                    else
                    {
                        throw new InvalidOperationException("无法将实体转换为指定类型。");
                    }
                    // 提交事务
                    trans.Commit();
                }
            }
        }
        /// <summary>
        /// 修改实体集合的属性。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="ents">要修改的实体集合。</param>
        /// <param name="act">修改操作。</param>
        public static void ChangeEntitiesProperty<T>(this IEnumerable<T> ents, Action<Entity> act) where T : Entity
        {
            if (ents == null)
            {
                throw new ArgumentNullException(nameof(ents), "实体集合不能为空。");
            }
            if (act == null)
            {
                throw new ArgumentNullException(nameof(act), "操作不能为空。");
            }
            var doc = Application.DocumentManager.MdiActiveDocument;
            using (DocumentLock docLock = doc.LockDocument())
            {
                // 使用 Parallel.ForEach 进行并行处理
                Parallel.ForEach(ents, ent =>
                {
                    act.Invoke(ent);
                });
            }
        }
    }
}
