using Autodesk.AutoCAD.DatabaseServices;
using System;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Domain.Interfaces
{
    /// <summary>
    /// 数据库操作服务接口 (Database Service Interface)
    /// 负责访问 AutoCAD 数据库、查询实体和表对象
    /// </summary>
    /// <remarks>
    /// 设计原则 (Design Principles):
    /// 1. 统一数据访问 (Unified Data Access) - 封装数据库操作细节
    /// 2. 类型安全 (Type Safety) - 提供泛型方法返回具体类型
    /// 3. 查询优化 (Query Optimization) - 提供高效的查询方法
    /// </remarks>
    public interface IDatabaseService
    {
        #region 数据库访问 (Database Access)

        /// <summary>
        /// 获取当前数据库 (Get Current Database)
        /// </summary>
        /// <returns>当前 AutoCAD 数据库</returns>
        Database GetCurrentDatabase();

        #endregion

        #region 符号表访问 (Symbol Table Access)

        /// <summary>
        /// 获取 Block 表 (Get Block Table)
        /// </summary>
        /// <returns>Block 表 ObjectId</returns>
        ObjectId GetBlockTableId();

        /// <summary>
        /// 获取图层表 (Get Layer Table)
        /// </summary>
        /// <returns>图层表 ObjectId</returns>
        ObjectId GetLayerTableId();

        /// <summary>
        /// 获取文本样式表 (Get Text Style Table)
        /// </summary>
        /// <returns>文本样式表 ObjectId</returns>
        ObjectId GetTextStyleTableId();

        /// <summary>
        /// 获取标注样式表 (Get Dimension Style Table)
        /// </summary>
        /// <returns>标注样式表 ObjectId</returns>
        ObjectId GetDimStyleTableId();

        #endregion

        #region 实体查询 (Entity Query)

        /// <summary>
        /// 获取 ModelSpace 中的所有实体 (Get All Entities in ModelSpace)
        /// </summary>
        /// <returns>实体 ObjectId 数组</returns>
        ObjectId[] GetAllEntitiesInModelSpace();

        /// <summary>
        /// 获取指定 Block 中的所有实体 (Get All Entities in Block)
        /// </summary>
        /// <param name="blockName">Block 名称 (Block Name)</param>
        /// <returns>实体 ObjectId 数组</returns>
        ObjectId[] GetAllEntitiesInBlock(string blockName);

        /// <summary>
        /// 获取指定类型的所有实体 (Get All Entities by Type)
        /// </summary>
        /// <typeparam name="T">实体类型 (Entity Type)</typeparam>
        /// <returns>实体 ObjectId 数组</returns>
        ObjectId[] GetAllEntitiesByType<T>() where T : Entity;

        /// <summary>
        /// 获取指定图层上的所有实体 (Get All Entities on Layer)
        /// </summary>
        /// <param name="layerName">图层名称 (Layer Name)</param>
        /// <returns>实体 ObjectId 数组</returns>
        ObjectId[] GetAllEntitiesOnLayer(string layerName);

        #endregion

        #region 实体访问 (Entity Access)

        /// <summary>
        /// 获取实体对象（只读） (Get Entity - Read Only)
        /// </summary>
        /// <typeparam name="T">实体类型 (Entity Type)</typeparam>
        /// <param name="entityId">实体 ObjectId</param>
        /// <returns>实体对象</returns>
        T GetEntity<T>(ObjectId entityId) where T : Entity;

        /// <summary>
        /// 修改实体对象 (Modify Entity)
        /// </summary>
        /// <typeparam name="T">实体类型 (Entity Type)</typeparam>
        /// <param name="entityId">实体 ObjectId</param>
        /// <param name="action">修改操作 (Modification Action)</param>
        void ModifyEntity<T>(ObjectId entityId, Action<T> action) where T : Entity;

        /// <summary>
        /// 批量获取实体对象 (Batch Get Entities)
        /// </summary>
        /// <typeparam name="T">实体类型 (Entity Type)</typeparam>
        /// <param name="entityIds">实体 ObjectId 集合</param>
        /// <returns>实体对象列表</returns>
        List<T> GetEntities<T>(IEnumerable<ObjectId> entityIds) where T : Entity;

        #endregion

        #region Block 操作 (Block Operations)

        /// <summary>
        /// 检查 Block 是否存在 (Check if Block Exists)
        /// </summary>
        /// <param name="blockName">Block 名称 (Block Name)</param>
        /// <returns>存在返回 true，否则返回 false</returns>
        bool BlockExists(string blockName);

        /// <summary>
        /// 获取 Block 的 ObjectId (Get Block ObjectId)
        /// </summary>
        /// <param name="blockName">Block 名称 (Block Name)</param>
        /// <returns>Block ObjectId，不存在则返回 ObjectId.Null</returns>
        ObjectId GetBlockId(string blockName);

        /// <summary>
        /// 获取 Block 的所有引用 (Get All Block References)
        /// </summary>
        /// <param name="blockName">Block 名称 (Block Name)</param>
        /// <returns>BlockReference ObjectId 数组</returns>
        ObjectId[] GetAllBlockReferences(string blockName);

        #endregion

        #region 图层操作 (Layer Operations)

        /// <summary>
        /// 检查图层是否存在 (Check if Layer Exists)
        /// </summary>
        /// <param name="layerName">图层名称 (Layer Name)</param>
        /// <returns>存在返回 true，否则返回 false</returns>
        bool LayerExists(string layerName);

        /// <summary>
        /// 获取图层 ObjectId (Get Layer ObjectId)
        /// </summary>
        /// <param name="layerName">图层名称 (Layer Name)</param>
        /// <returns>图层 ObjectId，不存在则返回 ObjectId.Null</returns>
        ObjectId GetLayerId(string layerName);

        /// <summary>
        /// 获取所有图层名称 (Get All Layer Names)
        /// </summary>
        /// <returns>图层名称列表</returns>
        List<string> GetAllLayerNames();

        #endregion
    }
}

