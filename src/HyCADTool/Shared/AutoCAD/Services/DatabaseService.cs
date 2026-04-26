using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Domain.Interfaces;
using HyCADTool.Shared.AutoCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Shared.AutoCAD.Services
{
    /// <summary>
    /// 数据库操作服务实现 (Database Service Implementation)
    /// 封装 AutoCAD 数据库访问操作
    /// </summary>
    public class DatabaseService : IDatabaseService
    {
        #region 数据库访问 (Database Access)

        public Database GetCurrentDatabase()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            return doc.Database;
        }

        #endregion

        #region 符号表访问 (Symbol Table Access)

        public ObjectId GetBlockTableId()
        {
            var db = GetCurrentDatabase();
            return db.BlockTableId;
        }

        public ObjectId GetLayerTableId()
        {
            var db = GetCurrentDatabase();
            return db.LayerTableId;
        }

        public ObjectId GetTextStyleTableId()
        {
            var db = GetCurrentDatabase();
            return db.TextStyleTableId;
        }

        public ObjectId GetDimStyleTableId()
        {
            var db = GetCurrentDatabase();
            return db.DimStyleTableId;
        }

        #endregion

        #region 实体查询 (Entity Query)

        public ObjectId[] GetAllEntitiesInModelSpace()
        {
            var db = GetCurrentDatabase();
            var entities = new List<ObjectId>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForRead
                    );

                    foreach (ObjectId id in btr)
                    {
                        entities.Add(id);
                    }

                    tr.Commit();
                    return entities.ToArray();
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        public ObjectId[] GetAllEntitiesInBlock(string blockName)
        {
            if (string.IsNullOrEmpty(blockName))
                throw new ArgumentException("Block 名称不能为空 (Block name cannot be empty)", nameof(blockName));

            var db = GetCurrentDatabase();
            var entities = new List<ObjectId>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                    if (!bt.Has(blockName))
                    {
                        tr.Commit();
                        return new ObjectId[0];
                    }

                    var btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);

                    foreach (ObjectId id in btr)
                    {
                        entities.Add(id);
                    }

                    tr.Commit();
                    return entities.ToArray();
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        public ObjectId[] GetAllEntitiesByType<T>() where T : Entity
        {
            var db = GetCurrentDatabase();
            var entities = new List<ObjectId>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForRead
                    );

                    foreach (ObjectId id in btr)
                    {
                        var entity = tr.GetObject(id, OpenMode.ForRead) as T;
                        if (entity != null)
                        {
                            entities.Add(id);
                        }
                    }

                    tr.Commit();
                    return entities.ToArray();
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        public ObjectId[] GetAllEntitiesOnLayer(string layerName)
        {
            if (string.IsNullOrEmpty(layerName))
                throw new ArgumentException("图层名称不能为空 (Layer name cannot be empty)", nameof(layerName));

            var db = GetCurrentDatabase();
            var entities = new List<ObjectId>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForRead
                    );

                    foreach (ObjectId id in btr)
                    {
                        var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (entity != null && entity.Layer.Equals(layerName, StringComparison.OrdinalIgnoreCase))
                        {
                            entities.Add(id);
                        }
                    }

                    tr.Commit();
                    return entities.ToArray();
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        #endregion

        #region 实体访问 (Entity Access)

        public T GetEntity<T>(ObjectId entityId) where T : Entity
        {
            if (entityId.IsNull)
                throw new ArgumentException("实体 ObjectId 无效 (Entity ObjectId is invalid)", nameof(entityId));

            var db = GetCurrentDatabase();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var entity = tr.GetObject(entityId, OpenMode.ForRead) as T;
                    tr.Commit();
                    return entity;
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        public void ModifyEntity<T>(ObjectId entityId, Action<T> action) where T : Entity
        {
            if (entityId.IsNull)
                throw new ArgumentException("实体 ObjectId 无效 (Entity ObjectId is invalid)", nameof(entityId));

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var db = GetCurrentDatabase();
            var doc = AcApp.DocumentManager.MdiActiveDocument;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var entity = tr.GetObject(entityId, OpenMode.ForWrite) as T;
                    if (entity != null)
                    {
                        action(entity);
                    }
                    tr.Commit();
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        public List<T> GetEntities<T>(IEnumerable<ObjectId> entityIds) where T : Entity
        {
            if (entityIds == null)
                throw new ArgumentNullException(nameof(entityIds));

            var db = GetCurrentDatabase();
            var entities = new List<T>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    foreach (var id in entityIds)
                    {
                        if (!id.IsNull)
                        {
                            var entity = tr.GetObject(id, OpenMode.ForRead) as T;
                            if (entity != null)
                            {
                                entities.Add(entity);
                            }
                        }
                    }

                    tr.Commit();
                    return entities;
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        #endregion

        #region Block 操作 (Block Operations)

        public bool BlockExists(string blockName)
        {
            if (string.IsNullOrEmpty(blockName))
                return false;

            var db = GetCurrentDatabase();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    bool exists = bt.Has(blockName);
                    tr.Commit();
                    return exists;
                }
                catch
                {
                    tr.Abort();
                    return false;
                }
            }
        }

        public ObjectId GetBlockId(string blockName)
        {
            if (string.IsNullOrEmpty(blockName))
                return ObjectId.Null;

            var db = GetCurrentDatabase();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    
                    if (bt.Has(blockName))
                    {
                        var id = bt[blockName];
                        tr.Commit();
                        return id;
                    }

                    tr.Commit();
                    return ObjectId.Null;
                }
                catch
                {
                    tr.Abort();
                    return ObjectId.Null;
                }
            }
        }

        public ObjectId[] GetAllBlockReferences(string blockName)
        {
            if (string.IsNullOrEmpty(blockName))
                throw new ArgumentException("Block 名称不能为空 (Block name cannot be empty)", nameof(blockName));

            var db = GetCurrentDatabase();
            var references = new List<ObjectId>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                    if (!bt.Has(blockName))
                    {
                        tr.Commit();
                        return new ObjectId[0];
                    }

                    var blockId = bt[blockName];
                    var btr = (BlockTableRecord)tr.GetObject(blockId, OpenMode.ForRead);

                    // 获取该 Block 的所有引用
                    var ids = btr.GetBlockReferenceIds(true, true);
                    
                    foreach (ObjectId id in ids)
                    {
                        references.Add(id);
                    }

                    tr.Commit();
                    return references.ToArray();
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        #endregion

        #region 图层操作 (Layer Operations)

        public bool LayerExists(string layerName)
        {
            if (string.IsNullOrEmpty(layerName))
                return false;

            var db = GetCurrentDatabase();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    bool exists = lt.Has(layerName);
                    tr.Commit();
                    return exists;
                }
                catch
                {
                    tr.Abort();
                    return false;
                }
            }
        }

        public ObjectId GetLayerId(string layerName)
        {
            if (string.IsNullOrEmpty(layerName))
                return ObjectId.Null;

            var db = GetCurrentDatabase();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    
                    if (lt.Has(layerName))
                    {
                        var id = lt[layerName];
                        tr.Commit();
                        return id;
                    }

                    tr.Commit();
                    return ObjectId.Null;
                }
                catch
                {
                    tr.Abort();
                    return ObjectId.Null;
                }
            }
        }

        public List<string> GetAllLayerNames()
        {
            var db = GetCurrentDatabase();
            var layerNames = new List<string>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                    foreach (ObjectId id in lt)
                    {
                        var ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                        layerNames.Add(ltr.Name);
                    }

                    tr.Commit();
                    return layerNames;
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        #endregion
    }
}

