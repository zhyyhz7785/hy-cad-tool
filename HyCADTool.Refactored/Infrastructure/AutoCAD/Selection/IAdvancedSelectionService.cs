using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Selection
{
    /// <summary>
    /// CAD实体类型枚举
    /// CAD Entity Type Enum
    /// </summary>
    public enum CadEntityType
    {
        Curve,
        Arc,
        Circle,
        Ellipse,
        Leader,
        Line,
        Polyline,
        Spline,
        Xline,
        BlockReference,
        Hatch,
        DBPoint,
        DBText,
        Dimension,
        MLeader,
        MText,
        Region,
        DetailSymbol,
        Mline
    }

    /// <summary>
    /// 高级选择服务接口
    /// Advanced Selection Service Interface
    /// </summary>
    public interface IAdvancedSelectionService
    {
        /// <summary>
        /// 根据CAD类型选择实体
        /// Select entities by CAD type
        /// </summary>
        /// <param name="entityType">实体类型 Entity type</param>
        /// <param name="sourceIds">源实体ID集合（可选） Source entity IDs (optional)</param>
        /// <returns>选中的实体ID数组 Selected entity IDs</returns>
        ObjectId[] SelectByType(CadEntityType entityType, ObjectId[] sourceIds = null);

        /// <summary>
        /// 选择单个指定类型的实体
        /// Select a single entity of specified type
        /// </summary>
        /// <typeparam name="T">实体类型 Entity type</typeparam>
        /// <returns>选中的实体 Selected entity</returns>
        T SelectSingleEntity<T>() where T : Entity, new();

        /// <summary>
        /// 选择单个实体（任意类型）
        /// Select a single entity (any type)
        /// </summary>
        /// <returns>选中的实体 Selected entity</returns>
        Entity SelectSingleEntity();

        /// <summary>
        /// 根据类型过滤值选择实体
        /// Select entities by typed value filter
        /// </summary>
        /// <param name="typedValues">类型值过滤器 Typed value filter</param>
        /// <returns>选中的实体ID数组 Selected entity IDs</returns>
        ObjectId[] SelectByFilter(IEnumerable<TypedValue> typedValues = null);

        /// <summary>
        /// 无用户交互地选择实体（从所有或指定源）
        /// Select entities without user interaction (from all or specified source)
        /// </summary>
        /// <param name="sourceIds">源实体ID集合（可选） Source entity IDs (optional)</param>
        /// <param name="typedValues">类型值过滤器（可选） Typed value filter (optional)</param>
        /// <returns>选中的实体ID数组 Selected entity IDs</returns>
        ObjectId[] SelectWithoutUserAction(ObjectId[] sourceIds = null, IEnumerable<TypedValue> typedValues = null);

        /// <summary>
        /// 获取预选择集
        /// Get implied selection
        /// </summary>
        /// <returns>预选择的实体ID数组 Implied selection IDs</returns>
        ObjectId[] GetImpliedSelection();

        /// <summary>
        /// ID集合转换为实体集合
        /// Convert IDs to entities
        /// </summary>
        /// <param name="ids">实体ID集合 Entity IDs</param>
        /// <returns>实体数组 Entities</returns>
        Entity[] IdsToEntities(IEnumerable<ObjectId> ids);

        /// <summary>
        /// ID集合转换为指定类型实体集合
        /// Convert IDs to typed entities
        /// </summary>
        /// <typeparam name="T">实体类型 Entity type</typeparam>
        /// <param name="ids">实体ID集合 Entity IDs</param>
        /// <returns>指定类型实体数组 Typed entities</returns>
        T[] IdsToEntities<T>(IEnumerable<ObjectId> ids) where T : Entity;

        /// <summary>
        /// 单个ID转换为实体
        /// Convert single ID to entity
        /// </summary>
        /// <param name="id">实体ID Entity ID</param>
        /// <returns>实体 Entity</returns>
        Entity IdToEntity(ObjectId id);

        /// <summary>
        /// 单个ID转换为指定类型实体
        /// Convert single ID to typed entity
        /// </summary>
        /// <typeparam name="T">实体类型 Entity type</typeparam>
        /// <param name="id">实体ID Entity ID</param>
        /// <returns>指定类型实体 Typed entity</returns>
        T IdToEntity<T>(ObjectId id) where T : Entity;

        /// <summary>
        /// 实体集合转换为ID集合
        /// Convert entities to IDs
        /// </summary>
        /// <param name="entities">实体集合 Entities</param>
        /// <returns>实体ID数组 Entity IDs</returns>
        ObjectId[] EntitiesToIds(IEnumerable<Entity> entities);

        /// <summary>
        /// 设置实体可见性
        /// Set entity visibility
        /// </summary>
        /// <param name="ids">实体ID集合 Entity IDs</param>
        /// <param name="visible">是否可见 Visibility</param>
        void SetEntityVisibility(ObjectId[] ids, bool visible);

        /// <summary>
        /// 高亮显示选择集
        /// Highlight selection
        /// </summary>
        /// <param name="ids">实体ID集合 Entity IDs</param>
        void HighlightSelection(IEnumerable<ObjectId> ids);

        /// <summary>
        /// 根据谓词过滤实体
        /// Filter entities by predicate
        /// </summary>
        /// <param name="predicate">过滤谓词 Filter predicate</param>
        /// <param name="inputIds">输入实体ID集合 Input entity IDs</param>
        /// <returns>过滤后的实体ID数组 Filtered entity IDs</returns>
        ObjectId[] FilterEntities(Func<Entity, bool> predicate, ObjectId[] inputIds);
    }
}

