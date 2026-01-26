using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions
{
    /// <summary>
    /// AutoCAD选择操作扩展方法
    /// AutoCAD Selection Operation Extension Methods
    /// </summary>
    public static class SelectionExtensions
    {
        /// <summary>
        /// 使用过滤器选择所有符合条件的实体
        /// Select all entities with filter
        /// </summary>
        public static ObjectId[] SelectAllWithFilter(this Editor editor, SelectionFilter filter)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            using (doc.LockDocument())
            {
                // 清空预选择集
                editor.SetImpliedSelection(new ObjectId[0]);

                // 使用过滤器选择所有符合条件的实体
                var res = editor.SelectAll(filter);

                if (res.Status == PromptStatus.OK)
                {
                    editor.WriteMessage($"选中了 {res.Value.Count} 个满足过滤条件的图形对象。\n");
                    return res.Value.GetObjectIds();
                }
                else
                {
                    editor.WriteMessage("未能选中任何对象。\n");
                    return new ObjectId[0];
                }
            }
        }

        /// <summary>
        /// 使用过滤器选择实体（用户交互）
        /// Select entities with filter (user interaction)
        /// </summary>
        public static ObjectId[] SelectWithFilter(this Editor editor, SelectionFilter filter, string message = "\n请选择要过滤图形对象:")
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            try
            {
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    // 清空预选集
                    editor.SetImpliedSelection(new ObjectId[0]);

                    // 创建PromptSelectionOptions对象，用于设置选择提示
                    var opt = new PromptSelectionOptions
                    {
                        MessageForAdding = message
                    };

                    // 使用选择过滤器获取选择集
                    var res = editor.GetSelection(opt, filter);

                    // 检查选择结果状态
                    if (res.Status == PromptStatus.OK)
                    {
                        editor.WriteMessage($"选中了 {res.Value.Count} 个满足过滤条件的图形对象。\n");
                        var ids = res.Value.GetObjectIds();
                        tr.Commit();
                        return ids;
                    }
                    else
                    {
                        editor.WriteMessage("未能选中任何对象。\n");
                        return new ObjectId[0];
                    }
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                editor.WriteMessage($"发生错误: {ex.Message}\n");
                return new ObjectId[0];
            }
        }

        /// <summary>
        /// 高亮显示实体集合
        /// Highlight entity collection
        /// </summary>
        public static void Highlight(this IEnumerable<ObjectId> ids)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using (var trans = db.TransactionManager.StartTransaction())
            {
                foreach (var id in ids)
                {
                    if (trans.GetObject(id, OpenMode.ForRead) is Entity ent)
                    {
                        ent.Highlight();
                    }
                }
                trans.Commit();
            }
        }

        /// <summary>
        /// 取消高亮显示实体集合
        /// Unhighlight entity collection
        /// </summary>
        public static void Unhighlight(this IEnumerable<ObjectId> ids)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using (var trans = db.TransactionManager.StartTransaction())
            {
                foreach (var id in ids)
                {
                    if (trans.GetObject(id, OpenMode.ForRead) is Entity ent)
                    {
                        ent.Unhighlight();
                    }
                }
                trans.Commit();
            }
        }

        /// <summary>
        /// 设置实体集合的可见性
        /// Set visibility of entity collection
        /// </summary>
        public static void SetVisibility(this IEnumerable<ObjectId> ids, bool visible)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using (var docLock = doc.LockDocument())
            using (var trans = db.TransactionManager.StartTransaction())
            {
                foreach (var id in ids)
                {
                    if (trans.GetObject(id, OpenMode.ForWrite) is Entity ent)
                    {
                        ent.Visible = visible;
                    }
                }
                trans.Commit();
            }
        }

        /// <summary>
        /// 根据谓词过滤实体集合
        /// Filter entity collection by predicate
        /// </summary>
        public static ObjectId[] FilterBy(this ObjectId[] ids, Func<Entity, bool> predicate)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var result = new List<ObjectId>();

            using (var trans = db.TransactionManager.StartTransaction())
            {
                foreach (var id in ids)
                {
                    if (trans.GetObject(id, OpenMode.ForRead) is Entity ent)
                    {
                        try
                        {
                            if (predicate(ent))
                            {
                                result.Add(id);
                            }
                        }
                        catch
                        {
                            // 忽略错误
                        }
                    }
                }
                trans.Commit();
            }

            return result.ToArray();
        }

        /// <summary>
        /// 按类型过滤实体集合
        /// Filter entity collection by type
        /// </summary>
        public static ObjectId[] FilterByType<T>(this ObjectId[] ids) where T : Entity
        {
            return ids.FilterBy(ent => ent is T);
        }

        /// <summary>
        /// 按图层过滤实体集合
        /// Filter entity collection by layer
        /// </summary>
        public static ObjectId[] FilterByLayer(this ObjectId[] ids, string layerName)
        {
            return ids.FilterBy(ent => ent.Layer == layerName);
        }

        /// <summary>
        /// 转换ObjectId集合为实体集合
        /// Convert ObjectId collection to Entity collection
        /// </summary>
        public static Entity[] ToEntities(this IEnumerable<ObjectId> ids)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using (var trans = db.TransactionManager.StartTransaction())
            {
                var entities = ids
                    .Select(id => trans.GetObject(id, OpenMode.ForRead) as Entity)
                    .Where(ent => ent != null)
                    .ToArray();
                trans.Commit();
                return entities;
            }
        }

        /// <summary>
        /// 转换ObjectId集合为指定类型实体集合
        /// Convert ObjectId collection to typed Entity collection
        /// </summary>
        public static T[] ToEntities<T>(this IEnumerable<ObjectId> ids) where T : Entity
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using (var trans = db.TransactionManager.StartTransaction())
            {
                var entities = ids
                    .Select(id => trans.GetObject(id, OpenMode.ForRead) as T)
                    .Where(ent => ent != null)
                    .ToArray();
                trans.Commit();
                return entities;
            }
        }

        /// <summary>
        /// 转换实体集合为ObjectId集合
        /// Convert Entity collection to ObjectId collection
        /// </summary>
        public static ObjectId[] ToObjectIds(this IEnumerable<Entity> entities)
        {
            return entities.Select(ent => ent.ObjectId).ToArray();
        }

        /// <summary>
        /// 检查选择集是否为空
        /// Check if selection set is empty
        /// </summary>
        public static bool IsEmpty(this ObjectId[] ids)
        {
            return ids == null || ids.Length == 0;
        }

        /// <summary>
        /// 检查选择集是否包含指定类型的实体
        /// Check if selection set contains entities of specified type
        /// </summary>
        public static bool ContainsType<T>(this ObjectId[] ids) where T : Entity
        {
            return ids.FilterByType<T>().Length > 0;
        }

        /// <summary>
        /// 获取选择集中指定类型实体的数量
        /// Get count of entities of specified type in selection set
        /// </summary>
        public static int CountOfType<T>(this ObjectId[] ids) where T : Entity
        {
            return ids.FilterByType<T>().Length;
        }
    }
}

