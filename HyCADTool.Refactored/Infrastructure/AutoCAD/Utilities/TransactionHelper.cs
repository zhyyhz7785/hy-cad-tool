using Autodesk.AutoCAD.DatabaseServices;
using System;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Utilities
{
    /// <summary>
    /// 事务管理扩展方法 (Transaction Helper Extension Methods)
    /// 提供简化的事务操作
    /// </summary>
    /// <remarks>
    /// 设计目的 (Design Purpose):
    /// 1. 简化常见事务操作 (Simplify Common Transaction Operations)
    /// 2. 减少样板代码 (Reduce Boilerplate Code)
    /// 3. 提供类型安全的实体访问 (Provide Type-safe Entity Access)
    /// 
    /// 使用示例 (Usage Example):
    /// <code>
    /// using (var tr = db.TransactionManager.StartTransaction())
    /// {
    ///     var line = tr.GetEntity&lt;Line&gt;(lineId);
    ///     tr.ModifyEntity&lt;Line&gt;(lineId, l => l.Color = Color.FromColorIndex(ColorMethod.ByAci, 1));
    ///     var id = tr.AddToModelSpace(db, new Line(pt1, pt2));
    ///     tr.Commit();
    /// }
    /// </code>
    /// </remarks>
    public static class TransactionHelper
    {
        #region 实体获取 (Entity Retrieval)

        /// <summary>
        /// 从事务中获取实体（只读） (Get Entity - Read Only)
        /// </summary>
        /// <typeparam name="T">实体类型 (Entity Type)</typeparam>
        /// <param name="trans">事务对象 (Transaction)</param>
        /// <param name="id">实体 ObjectId</param>
        /// <returns>实体对象，类型不匹配返回 null</returns>
        public static T GetEntity<T>(this Transaction trans, ObjectId id) where T : Entity
        {
            if (id.IsNull)
                return null;

            return trans.GetObject(id, OpenMode.ForRead) as T;
        }

        /// <summary>
        /// 从事务中获取实体（可写） (Get Entity - Writable)
        /// </summary>
        /// <typeparam name="T">实体类型 (Entity Type)</typeparam>
        /// <param name="trans">事务对象 (Transaction)</param>
        /// <param name="id">实体 ObjectId</param>
        /// <returns>实体对象，类型不匹配返回 null</returns>
        public static T GetEntityForWrite<T>(this Transaction trans, ObjectId id) where T : Entity
        {
            if (id.IsNull)
                return null;

            return trans.GetObject(id, OpenMode.ForWrite) as T;
        }

        /// <summary>
        /// 批量获取实体 (Batch Get Entities)
        /// </summary>
        /// <typeparam name="T">实体类型 (Entity Type)</typeparam>
        /// <param name="trans">事务对象 (Transaction)</param>
        /// <param name="ids">实体 ObjectId 集合</param>
        /// <returns>实体对象列表</returns>
        public static List<T> GetEntities<T>(this Transaction trans, IEnumerable<ObjectId> ids) where T : Entity
        {
            var entities = new List<T>();

            if (ids == null)
                return entities;

            foreach (var id in ids)
            {
                if (!id.IsNull)
                {
                    var entity = trans.GetEntity<T>(id);
                    if (entity != null)
                    {
                        entities.Add(entity);
                    }
                }
            }

            return entities;
        }

        #endregion

        #region 实体修改 (Entity Modification)

        /// <summary>
        /// 修改实体（自动升级写权限） (Modify Entity - Auto Upgrade to Write)
        /// </summary>
        /// <typeparam name="T">实体类型 (Entity Type)</typeparam>
        /// <param name="trans">事务对象 (Transaction)</param>
        /// <param name="id">实体 ObjectId</param>
        /// <param name="action">修改操作 (Modification Action)</param>
        /// <returns>修改成功返回 true，实体不存在或类型不匹配返回 false</returns>
        public static bool ModifyEntity<T>(this Transaction trans, ObjectId id, Action<T> action) where T : Entity
        {
            if (id.IsNull || action == null)
                return false;

            var entity = trans.GetObject(id, OpenMode.ForWrite) as T;
            if (entity != null)
            {
                action(entity);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 批量修改实体 (Batch Modify Entities)
        /// </summary>
        /// <typeparam name="T">实体类型 (Entity Type)</typeparam>
        /// <param name="trans">事务对象 (Transaction)</param>
        /// <param name="ids">实体 ObjectId 集合</param>
        /// <param name="action">修改操作 (Modification Action)</param>
        /// <returns>成功修改的实体数量</returns>
        public static int ModifyEntities<T>(this Transaction trans, IEnumerable<ObjectId> ids, Action<T> action) where T : Entity
        {
            if (ids == null || action == null)
                return 0;

            int count = 0;
            foreach (var id in ids)
            {
                if (trans.ModifyEntity(id, action))
                {
                    count++;
                }
            }

            return count;
        }

        #endregion

        #region 实体添加 (Entity Addition)

        /// <summary>
        /// 添加实体到 ModelSpace (Add Entity to ModelSpace)
        /// </summary>
        /// <param name="trans">事务对象 (Transaction)</param>
        /// <param name="db">数据库 (Database)</param>
        /// <param name="entity">实体对象 (Entity)</param>
        /// <returns>创建的实体 ObjectId</returns>
        public static ObjectId AddToModelSpace(this Transaction trans, Database db, Entity entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            var bt = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)trans.GetObject(
                bt[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite
            );

            var id = btr.AppendEntity(entity);
            trans.AddNewlyCreatedDBObject(entity, true);
            return id;
        }

        /// <summary>
        /// 批量添加实体到 ModelSpace (Batch Add Entities to ModelSpace)
        /// </summary>
        /// <param name="trans">事务对象 (Transaction)</param>
        /// <param name="db">数据库 (Database)</param>
        /// <param name="entities">实体对象数组 (Entity Array)</param>
        /// <returns>创建的实体 ObjectId 数组</returns>
        public static ObjectId[] AddEntitiesToModelSpace(
            this Transaction trans,
            Database db,
            params Entity[] entities)
        {
            if (entities == null || entities.Length == 0)
                return new ObjectId[0];

            var bt = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)trans.GetObject(
                bt[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite
            );

            var ids = new ObjectId[entities.Length];
            for (int i = 0; i < entities.Length; i++)
            {
                if (entities[i] != null)
                {
                    ids[i] = btr.AppendEntity(entities[i]);
                    trans.AddNewlyCreatedDBObject(entities[i], true);
                }
            }

            return ids;
        }

        /// <summary>
        /// 添加实体到指定 Block (Add Entity to Specified Block)
        /// </summary>
        /// <param name="trans">事务对象 (Transaction)</param>
        /// <param name="db">数据库 (Database)</param>
        /// <param name="entity">实体对象 (Entity)</param>
        /// <param name="blockName">Block 名称 (Block Name)</param>
        /// <returns>创建的实体 ObjectId</returns>
        public static ObjectId AddToBlock(
            this Transaction trans,
            Database db,
            Entity entity,
            string blockName)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (string.IsNullOrEmpty(blockName))
                throw new ArgumentException("Block 名称不能为空 (Block name cannot be empty)", nameof(blockName));

            var bt = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);

            if (!bt.Has(blockName))
                throw new ArgumentException($"Block '{blockName}' 不存在 (Block '{blockName}' does not exist)");

            var btr = (BlockTableRecord)trans.GetObject(bt[blockName], OpenMode.ForWrite);

            var id = btr.AppendEntity(entity);
            trans.AddNewlyCreatedDBObject(entity, true);
            return id;
        }

        #endregion

        #region 符号表访问 (Symbol Table Access)

        /// <summary>
        /// 获取 BlockTable (Get BlockTable)
        /// </summary>
        public static BlockTable GetBlockTable(this Transaction trans, Database db, OpenMode mode = OpenMode.ForRead)
        {
            return (BlockTable)trans.GetObject(db.BlockTableId, mode);
        }

        /// <summary>
        /// 获取 LayerTable (Get LayerTable)
        /// </summary>
        public static LayerTable GetLayerTable(this Transaction trans, Database db, OpenMode mode = OpenMode.ForRead)
        {
            return (LayerTable)trans.GetObject(db.LayerTableId, mode);
        }

        /// <summary>
        /// 获取 TextStyleTable (Get TextStyleTable)
        /// </summary>
        public static TextStyleTable GetTextStyleTable(this Transaction trans, Database db, OpenMode mode = OpenMode.ForRead)
        {
            return (TextStyleTable)trans.GetObject(db.TextStyleTableId, mode);
        }

        /// <summary>
        /// 获取 DimStyleTable (Get DimStyleTable)
        /// </summary>
        public static DimStyleTable GetDimStyleTable(this Transaction trans, Database db, OpenMode mode = OpenMode.ForRead)
        {
            return (DimStyleTable)trans.GetObject(db.DimStyleTableId, mode);
        }

        /// <summary>
        /// 获取 ModelSpace BlockTableRecord (Get ModelSpace BlockTableRecord)
        /// </summary>
        public static BlockTableRecord GetModelSpace(this Transaction trans, Database db, OpenMode mode = OpenMode.ForRead)
        {
            var bt = trans.GetBlockTable(db, OpenMode.ForRead);
            return (BlockTableRecord)trans.GetObject(bt[BlockTableRecord.ModelSpace], mode);
        }

        #endregion

        #region 实体删除 (Entity Deletion)

        /// <summary>
        /// 删除实体 (Delete Entity)
        /// </summary>
        /// <param name="trans">事务对象 (Transaction)</param>
        /// <param name="id">实体 ObjectId</param>
        /// <returns>删除成功返回 true，实体不存在或已删除返回 false</returns>
        public static bool EraseEntity(this Transaction trans, ObjectId id)
        {
            if (id.IsNull)
                return false;

            var entity = trans.GetObject(id, OpenMode.ForWrite) as Entity;
            if (entity != null && !entity.IsErased)
            {
                entity.Erase();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 批量删除实体 (Batch Delete Entities)
        /// </summary>
        /// <param name="trans">事务对象 (Transaction)</param>
        /// <param name="ids">实体 ObjectId 集合</param>
        /// <returns>成功删除的实体数量</returns>
        public static int EraseEntities(this Transaction trans, IEnumerable<ObjectId> ids)
        {
            if (ids == null)
                return 0;

            int count = 0;
            foreach (var id in ids)
            {
                if (trans.EraseEntity(id))
                {
                    count++;
                }
            }

            return count;
        }

        #endregion
    }
}

