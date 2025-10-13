using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 选择服务实现 - AutoCAD 平台特定实现
    /// </summary>
    /// <remarks>
    /// 该类封装了 AutoCAD 的 Editor.GetSelection() 和 SelectionFilter 功能。
    /// 设计原则：
    /// 1. 统一选择接口 - 简化 AutoCAD 选择 API 的使用
    /// 2. 类型安全 - 提供泛型方法返回具体实体类型
    /// 3. 错误处理 - 优雅处理用户取消和选择失败
    /// </remarks>
    public class SelectionService : ISelectionService
    {
        private readonly ISelectionFilterService _filterService;

        public SelectionService(ISelectionFilterService filterService)
        {
            _filterService = filterService;
        }
        #region 基础选择方法

        /// <summary>
        /// 提示用户选择实体（带过滤条件）
        /// </summary>
        public ObjectId[] SelectEntities(string prompt, params string[] entityTypes)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                // 创建选择选项
                var options = new PromptSelectionOptions
                {
                    MessageForAdding = string.IsNullOrEmpty(prompt) ? "\n请选择对象: " : $"\n{prompt}: "
                };

                // 创建过滤器
                SelectionFilter filter = null;
                if (entityTypes != null && entityTypes.Length > 0)
                {
                    filter = CreateTypeFilter(entityTypes);
                }

                // 执行选择
                PromptSelectionResult result = filter != null
                    ? ed.GetSelection(options, filter)
                    : ed.GetSelection(options);

                // 检查结果
                if (result.Status == PromptStatus.OK)
                {
                    return result.Value.GetObjectIds();
                }

                return new ObjectId[0];
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n选择失败: {ex.Message}");
                return new ObjectId[0];
            }
        }

        /// <summary>
        /// 选择所有符合条件的实体（不提示用户）
        /// </summary>
        public ObjectId[] SelectAll(params string[] entityTypes)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                // 创建过滤器
                SelectionFilter filter = null;
                if (entityTypes != null && entityTypes.Length > 0)
                {
                    filter = CreateTypeFilter(entityTypes);
                }

                // 执行全选
                PromptSelectionResult result = filter != null
                    ? ed.SelectAll(filter)
                    : ed.SelectAll();

                // 检查结果
                if (result.Status == PromptStatus.OK)
                {
                    return result.Value.GetObjectIds();
                }

                return new ObjectId[0];
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n全选失败: {ex.Message}");
                return new ObjectId[0];
            }
        }

        #endregion

        #region 类型化选择方法

        /// <summary>
        /// 选择线段（Line）
        /// </summary>
        public List<Line> SelectLines(string prompt = null)
        {
            prompt = prompt ?? "请选择线段";
            var ids = SelectEntities(prompt, "LINE");
            return GetEntitiesOfType<Line>(ids);
        }

        /// <summary>
        /// 选择圆（Circle）
        /// </summary>
        public List<Circle> SelectCircles(string prompt = null)
        {
            prompt = prompt ?? "请选择圆";
            var ids = SelectEntities(prompt, "CIRCLE");
            return GetEntitiesOfType<Circle>(ids);
        }

        /// <summary>
        /// 选择多段线（Polyline/LWPolyline）
        /// </summary>
        public List<Polyline> SelectPolylines(string prompt = null)
        {
            prompt = prompt ?? "请选择多段线";
            var ids = SelectEntities(prompt, "LWPOLYLINE", "POLYLINE");
            return GetEntitiesOfType<Polyline>(ids);
        }

        /// <summary>
        /// 选择文本（DBText）
        /// </summary>
        public List<DBText> SelectTexts(string prompt = null)
        {
            prompt = prompt ?? "请选择文本";
            var ids = SelectEntities(prompt, "TEXT");
            return GetEntitiesOfType<DBText>(ids);
        }

        /// <summary>
        /// 选择多行文本（MText）
        /// </summary>
        public List<MText> SelectMTexts(string prompt = null)
        {
            prompt = prompt ?? "请选择多行文本";
            var ids = SelectEntities(prompt, "MTEXT");
            return GetEntitiesOfType<MText>(ids);
        }

        #endregion

        #region 过滤方法

        /// <summary>
        /// 按图层过滤实体
        /// </summary>
        public ObjectId[] FilterByLayer(ObjectId[] baseIds, string layerName)
        {
            if (baseIds == null || baseIds.Length == 0)
                return new ObjectId[0];

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            var filtered = new List<ObjectId>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var id in baseIds)
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity != null && entity.Layer.Equals(layerName, StringComparison.OrdinalIgnoreCase))
                    {
                        filtered.Add(id);
                    }
                }
                tr.Commit();
            }

            return filtered.ToArray();
        }

        /// <summary>
        /// 按颜色索引过滤实体
        /// </summary>
        public ObjectId[] FilterByColor(ObjectId[] baseIds, short colorIndex)
        {
            if (baseIds == null || baseIds.Length == 0)
                return new ObjectId[0];

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            var filtered = new List<ObjectId>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var id in baseIds)
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity != null && entity.ColorIndex == colorIndex)
                    {
                        filtered.Add(id);
                    }
                }
                tr.Commit();
            }

            return filtered.ToArray();
        }

        /// <summary>
        /// 按线型过滤实体
        /// </summary>
        public ObjectId[] FilterByLineType(ObjectId[] baseIds, string lineTypeName)
        {
            if (baseIds == null || baseIds.Length == 0)
                return new ObjectId[0];

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            var filtered = new List<ObjectId>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var id in baseIds)
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity != null && entity.Linetype.Equals(lineTypeName, StringComparison.OrdinalIgnoreCase))
                    {
                        filtered.Add(id);
                    }
                }
                tr.Commit();
            }

            return filtered.ToArray();
        }

        /// <summary>
        /// 按线宽过滤实体
        /// </summary>
        public ObjectId[] FilterByLineWeight(ObjectId[] baseIds, LineWeight lineWeight)
        {
            if (baseIds == null || baseIds.Length == 0)
                return new ObjectId[0];

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            var filtered = new List<ObjectId>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var id in baseIds)
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity != null && entity.LineWeight == lineWeight)
                    {
                        filtered.Add(id);
                    }
                }
                tr.Commit();
            }

            return filtered.ToArray();
        }

        /// <summary>
        /// 按实体类型过滤
        /// </summary>
        public ObjectId[] FilterByType(ObjectId[] baseIds, string entityType)
        {
            if (baseIds == null || baseIds.Length == 0)
                return new ObjectId[0];

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            var filtered = new List<ObjectId>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var id in baseIds)
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity != null)
                    {
                        var dxfName = entity.GetRXClass().DxfName;
                        if (dxfName.Equals(entityType, StringComparison.OrdinalIgnoreCase))
                        {
                            filtered.Add(id);
                        }
                    }
                }
                tr.Commit();
            }

            return filtered.ToArray();
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取当前选择集中的实体 ObjectId 数组
        /// </summary>
        public ObjectId[] GetCurrentSelection()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                var result = ed.SelectImplied();
                if (result.Status == PromptStatus.OK)
                {
                    return result.Value.GetObjectIds();
                }

                return new ObjectId[0];
            }
            catch
            {
                return new ObjectId[0];
            }
        }

        /// <summary>
        /// 高亮显示指定的实体
        /// </summary>
        public void HighlightEntities(ObjectId[] objectIds)
        {
            if (objectIds == null || objectIds.Length == 0)
                return;

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.SetImpliedSelection(objectIds);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n高亮显示失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消高亮显示
        /// </summary>
        public void UnhighlightEntities(ObjectId[] objectIds)
        {
            if (objectIds == null || objectIds.Length == 0)
                return;

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.SetImpliedSelection(new ObjectId[0]);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n取消高亮失败: {ex.Message}");
            }
        }

        #endregion

        #region 组合过滤（阶段 5 新增）

        public ObjectId[] SelectAllWithFilter(string dxfType = null, string layerName = null, short? colorIndex = null, string linetypeName = null, LineWeight? lineWeight = null)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                var filter = _filterService.Build(dxfType, layerName, colorIndex, linetypeName, lineWeight);
                var result = ed.SelectAll(filter);
                if (result.Status == PromptStatus.OK)
                {
                    return result.Value.GetObjectIds();
                }
                return new ObjectId[0];
            }
            catch
            {
                return new ObjectId[0];
            }
        }

        public ObjectId[] SelectEntitiesWithFilter(string prompt, string dxfType = null, string layerName = null, short? colorIndex = null, string linetypeName = null, LineWeight? lineWeight = null)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                var options = new PromptSelectionOptions
                {
                    MessageForAdding = string.IsNullOrEmpty(prompt) ? "\n请选择对象: " : $"\n{prompt}: "
                };
                var filter = _filterService.Build(dxfType, layerName, colorIndex, linetypeName, lineWeight);
                var result = ed.GetSelection(options, filter);
                if (result.Status == PromptStatus.OK)
                {
                    return result.Value.GetObjectIds();
                }
                return new ObjectId[0];
            }
            catch
            {
                return new ObjectId[0];
            }
        }

        #endregion

        #region 私有辅助方法

        /// <summary>
        /// 创建实体类型过滤器
        /// </summary>
        private SelectionFilter CreateTypeFilter(params string[] entityTypes)
        {
            if (entityTypes == null || entityTypes.Length == 0)
                return null;

            if (entityTypes.Length == 1)
            {
                // 单一类型过滤
                return new SelectionFilter(new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, entityTypes[0])
                });
            }
            else
            {
                // 多类型过滤（使用 OR 逻辑）
                var values = new List<TypedValue>
                {
                    new TypedValue((int)DxfCode.Operator, "<OR")
                };

                foreach (var type in entityTypes)
                {
                    values.Add(new TypedValue((int)DxfCode.Start, type));
                }

                values.Add(new TypedValue((int)DxfCode.Operator, "OR>"));

                return new SelectionFilter(values.ToArray());
            }
        }

        /// <summary>
        /// 从 ObjectId 数组获取指定类型的实体列表
        /// </summary>
        private List<T> GetEntitiesOfType<T>(ObjectId[] ids) where T : Entity
        {
            var entities = new List<T>();

            if (ids == null || ids.Length == 0)
                return entities;

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var id in ids)
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead) as T;
                    if (entity != null)
                    {
                        entities.Add(entity);
                    }
                }
                tr.Commit();
            }

            return entities;
        }

        #endregion
    }
}
