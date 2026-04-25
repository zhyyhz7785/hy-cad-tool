using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Shared.AutoCAD.Selection
{
    /// <summary>
    /// 高级选择服务实现
    /// Advanced Selection Service Implementation
    /// </summary>
    public class AdvancedSelectionService : IAdvancedSelectionService
    {
        private readonly Document _doc;
        private readonly Editor _ed;
        private readonly Database _db;

        public AdvancedSelectionService()
        {
            _doc = Application.DocumentManager.MdiActiveDocument;
            _ed = _doc.Editor;
            _db = _doc.Database;
        }

        /// <inheritdoc/>
        public ObjectId[] SelectByType(CadEntityType entityType, ObjectId[] sourceIds = null)
        {
            using (var docLock = _doc.LockDocument())
            using (var trans = _db.TransactionManager.StartTransaction())
            {
                var result = new List<ObjectId>();
                var idsToCheck = sourceIds ?? GetAllModelSpaceIds(trans);

                foreach (var id in idsToCheck)
                {
                    var ent = trans.GetObject(id, OpenMode.ForWrite, true, false);

                    if (IsEntityOfType(ent, entityType))
                    {
                        var entity = ent as Entity;
                        if (entity != null)
                        {
                            var layer = entity.LayerId.GetObject(OpenMode.ForRead) as LayerTableRecord;
                            // 只添加未加锁的图层
                            if (layer != null && !layer.IsLocked)
                            {
                                result.Add(id);
                            }
                        }
                    }
                }

                trans.Commit();
                return result.ToArray();
            }
        }

        /// <inheritdoc/>
        public T SelectSingleEntity<T>() where T : Entity, new()
        {
            var type = typeof(T);
            try
            {
                using (var docLock = _doc.LockDocument())
                using (var tr = _db.TransactionManager.StartTransaction())
                {
                    var btr = (BlockTableRecord)tr.GetObject(_db.CurrentSpaceId, OpenMode.ForWrite);
                    var peo = new PromptEntityOptions($"\n请选择一个 {type.Name}: ");
                    peo.SetRejectMessage($"\n选中的对象不是 {type.Name}。");
                    peo.AddAllowedClass(typeof(T), false);

                    var per = _ed.GetEntity(peo);
                    if (per.Status == PromptStatus.OK)
                    {
                        var entity = (T)per.ObjectId.GetObject(OpenMode.ForRead);
                        tr.Commit();
                        return entity;
                    }
                    else
                    {
                        _ed.WriteMessage("\n选择被取消或无效。");
                        return null;
                    }
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                _ed.WriteMessage($"\n错误: {ex.Message}");
                return null;
            }
        }

        /// <inheritdoc/>
        public Entity SelectSingleEntity()
        {
            try
            {
                _ed.WriteMessage("\n请选择一个实体或按 ESC 退出\n");

                var res = _ed.GetSelection();
                if (res.Status != PromptStatus.OK)
                {
                    _ed.WriteMessage("\n选择已取消或出错\n");
                    return null;
                }

                var ids = res.Value.GetObjectIds();
                if (ids.Length == 0)
                {
                    _ed.WriteMessage("\n没有选中实体\n");
                    return null;
                }
                if (ids.Length > 1)
                {
                    _ed.WriteMessage("\n选中多个实体\n");
                    return null;
                }

                using (var docLock = _doc.LockDocument())
                using (var trans = _doc.TransactionManager.StartTransaction())
                {
                    var ent = trans.GetObject(ids[0], OpenMode.ForWrite) as Entity;
                    if (ent != null)
                    {
                        _ed.WriteMessage($"\n选中单个实体成功，类型为：{ent.GetType().Name}\n");
                        trans.Commit();
                        return ent;
                    }
                }
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n系统错误：{ex.Message}\n");
            }

            return null;
        }

        /// <inheritdoc/>
        public ObjectId[] SelectByFilter(IEnumerable<TypedValue> typedValues = null)
        {
            typedValues = typedValues ?? GetAllModelSpaceFilter();
            var filter = new SelectionFilter(typedValues.ToArray());

            try
            {
                _ed.WriteMessage("\n请选择多个实体\n");
                var res = _ed.GetSelection(filter);
                if (res.Status == PromptStatus.OK)
                {
                    var ids = res.Value.GetObjectIds();
                    if (ids.Length > 0)
                    {
                        return ids;
                    }
                    else
                    {
                        _ed.WriteMessage($"\n选择失败：返回值为空，请重新选择\n");
                        return null;
                    }
                }
                else
                {
                    _ed.WriteMessage($"\n选择状态错误：返回值为空，请重新选择\n");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _ed.WriteMessage($"\n系统错误\n{ex}\n");
                return null;
            }
        }

        /// <inheritdoc/>
        public ObjectId[] SelectWithoutUserAction(ObjectId[] sourceIds = null, IEnumerable<TypedValue> typedValues = null)
        {
            typedValues = typedValues ?? GetAllModelSpaceFilter();
            sourceIds = sourceIds ?? _ed.SelectAll().Value?.GetObjectIds();

            var filter = new SelectionFilter(CombineFilters(GetAllModelSpaceFilter(), typedValues).ToArray());

            try
            {
                var res = _ed.SelectAll(filter);
                if (res.Status == PromptStatus.OK)
                {
                    var ids = res.Value.GetObjectIds();
                    if (sourceIds != null)
                    {
                        ids = ids.Intersect(sourceIds).ToArray();
                    }

                    if (ids.Length > 0)
                    {
                        return ids;
                    }
                    else
                    {
                        _ed.WriteMessage($"\n选择失败：返回值为空\n");
                        return null;
                    }
                }
                else
                {
                    _ed.WriteMessage($"\n选择状态错误：返回值为空\n");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _ed.WriteMessage($"\n系统错误\n{ex}\n");
                return null;
            }
        }

        /// <inheritdoc/>
        public ObjectId[] GetImpliedSelection()
        {
            var psr = _ed.SelectImplied();
            if (psr.Status == PromptStatus.OK)
            {
                var ids = psr.Value.GetObjectIds();
                _ed.WriteMessage($"\n获取到预选择集{ids.Length}个实体\n");
                return ids;
            }
            return null;
        }

        /// <inheritdoc/>
        public Entity[] IdsToEntities(IEnumerable<ObjectId> ids)
        {
            if (ids == null || !ids.Any())
            {
                _ed.WriteMessage($"\nId转换Entitie失败,ObjectId输入值为空");
                return null;
            }

            using (var acLckDocCur = _doc.LockDocument())
            {
                try
                {
                    var ents = new Entity[ids.Count()];
                    using (var trans = _db.TransactionManager.StartTransaction())
                    {
                        int i = 0;
                        foreach (var id in ids)
                        {
                            var ent = id.GetObject(OpenMode.ForWrite) as Entity;
                            ents[i++] = ent;
                        }
                        trans.Commit();
                    }
                    return ents;
                }
                catch (Exception ex)
                {
                    _ed.WriteMessage($"\nIds转换Entities失败\n{ex}");
                    return null;
                }
            }
        }

        /// <inheritdoc/>
        public T[] IdsToEntities<T>(IEnumerable<ObjectId> ids) where T : Entity
        {
            if (ids == null || !ids.Any())
            {
                _ed.WriteMessage($"\nId转换Entitie失败,ObjectId输入值为空");
                return null;
            }

            using (var acLckDocCur = _doc.LockDocument())
            {
                try
                {
                    var ents = new List<T>();
                    using (var trans = _db.TransactionManager.StartTransaction())
                    {
                        foreach (var id in ids)
                        {
                            var ent = id.GetObject(OpenMode.ForWrite) as T;
                            if (ent != null)
                            {
                                ents.Add(ent);
                            }
                        }
                        trans.Commit();
                    }
                    return ents.ToArray();
                }
                catch (Exception ex)
                {
                    _ed.WriteMessage($"\nIds转换Entities失败\n{ex}");
                    return null;
                }
            }
        }

        /// <inheritdoc/>
        public Entity IdToEntity(ObjectId id)
        {
            try
            {
                if (id.IsNull)
                {
                    _ed.WriteMessage($"\nId转换Entitie失败,ObjectId输入值为空");
                    return null;
                }

                using (var trans = _db.TransactionManager.StartTransaction())
                {
                    var ent = id.GetObject(OpenMode.ForWrite) as Entity;
                    trans.Commit();
                    return ent;
                }
            }
            catch (Exception ex)
            {
                _ed.WriteMessage($"\nIds转换Entities失败\n{ex}");
                return null;
            }
        }

        /// <inheritdoc/>
        public T IdToEntity<T>(ObjectId id) where T : Entity
        {
            try
            {
                if (id.IsNull)
                {
                    _ed.WriteMessage($"\nId转换Entitie失败,ObjectId输入值为空");
                    return null;
                }

                using (var trans = _db.TransactionManager.StartTransaction())
                {
                    var ent = id.GetObject(OpenMode.ForWrite) as T;
                    trans.Commit();
                    return ent;
                }
            }
            catch (Exception ex)
            {
                _ed.WriteMessage($"\nIds转换Entities失败\n{ex}");
                return null;
            }
        }

        /// <inheritdoc/>
        public ObjectId[] EntitiesToIds(IEnumerable<Entity> entities)
        {
            try
            {
                if (entities == null || !entities.Any())
                {
                    _ed.WriteMessage($"\nEntities转换Ids失败,Entities输入值为空");
                    return null;
                }

                return entities.Select(e => e.ObjectId).ToArray();
            }
            catch (Exception ex)
            {
                _ed.WriteMessage($"\nEntities转换Ids失败\n{ex}");
                return null;
            }
        }

        /// <inheritdoc/>
        public void SetEntityVisibility(ObjectId[] ids, bool visible)
        {
            using (var lockDoc = _doc.LockDocument())
            {
                try
                {
                    if (ids == null || ids.Length == 0)
                    {
                        _ed.WriteMessage($"\n输入值为空\n");
                        return;
                    }

                    using (var trans = _db.TransactionManager.StartTransaction())
                    {
                        foreach (var id in ids)
                        {
                            var ent = id.GetObject(OpenMode.ForWrite) as Entity;
                            if (ent != null)
                            {
                                ent.Visible = visible;
                            }
                        }
                        trans.Commit();
                    }
                }
                catch (Exception ex)
                {
                    _ed.WriteMessage($"\n{ex}\n");
                }
            }
        }

        /// <inheritdoc/>
        public void HighlightSelection(IEnumerable<ObjectId> ids)
        {
            var ents = IdsToEntities(ids);
            if (ents != null)
            {
                foreach (var ent in ents)
                {
                    if (ent != null)
                    {
                        ent.Highlight();
                    }
                }
            }
        }

        /// <inheritdoc/>
        public ObjectId[] FilterEntities(Func<Entity, bool> predicate, ObjectId[] inputIds)
        {
            var result = new List<ObjectId>();

            using (var tr = _db.TransactionManager.StartTransaction())
            {
                foreach (var id in inputIds)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity ent))
                        continue;

                    try
                    {
                        if (predicate(ent))
                            result.Add(id);
                    }
                    catch
                    {
                        // 忽略无效的属性或转换错误
                    }
                }

                tr.Commit();
            }

            return result.ToArray();
        }

        #region Private Helper Methods

        private ObjectId[] GetAllModelSpaceIds(Transaction trans)
        {
            var bt = (BlockTable)trans.GetObject(_db.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            return btr.Cast<ObjectId>().ToArray();
        }

        private IEnumerable<TypedValue> GetAllModelSpaceFilter()
        {
            return new[] { new TypedValue(410, "Model") };
        }

        private IEnumerable<TypedValue> CombineFilters(params IEnumerable<TypedValue>[] filters)
        {
            yield return new TypedValue((int)DxfCode.Operator, "<AND");
            foreach (var filter in filters)
            {
                foreach (var tv in filter)
                {
                    yield return tv;
                }
            }
            yield return new TypedValue((int)DxfCode.Operator, "AND>");
        }

        private bool IsEntityOfType(DBObject dbObj, CadEntityType entityType)
        {
            switch (entityType)
            {
                case CadEntityType.Line:
                    return dbObj is Line;
                case CadEntityType.Polyline:
                    return dbObj is Polyline;
                case CadEntityType.Circle:
                    return dbObj is Circle;
                case CadEntityType.Arc:
                    return dbObj is Arc;
                case CadEntityType.Ellipse:
                    return dbObj is Ellipse;
                case CadEntityType.Spline:
                    return dbObj is Spline;
                case CadEntityType.Xline:
                    return dbObj is Xline;
                case CadEntityType.BlockReference:
                    return dbObj is BlockReference;
                case CadEntityType.Hatch:
                    return dbObj is Hatch;
                case CadEntityType.DBPoint:
                    return dbObj is DBPoint;
                case CadEntityType.DBText:
                    return dbObj is DBText;
                case CadEntityType.MText:
                    return dbObj is MText;
                case CadEntityType.Dimension:
                    return dbObj is Dimension;
                case CadEntityType.MLeader:
                    return dbObj is MLeader;
                case CadEntityType.Region:
                    return dbObj is Region;
                case CadEntityType.Curve:
                    return dbObj is Curve;
                case CadEntityType.Mline:
                    return dbObj is Mline;
                default:
                    return false;
            }
        }

        #endregion
    }
}

