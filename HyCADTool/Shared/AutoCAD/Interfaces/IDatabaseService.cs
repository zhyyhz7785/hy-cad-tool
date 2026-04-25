using Autodesk.AutoCAD.DatabaseServices;
using System.Collections.Generic;

namespace HyCADTool.Shared.AutoCAD.Interfaces
{
    /// <summary>
    /// 数据库服务接口
    /// 封装 AutoCAD 数据库访问操作
    /// </summary>
    public interface IDatabaseService
    {
        /// <summary>
        /// 获取当前数据库
        /// </summary>
        Database GetCurrentDatabase();

        /// <summary>
        /// 获取块表ID
        /// </summary>
        ObjectId GetBlockTableId();

        /// <summary>
        /// 获取图层表ID
        /// </summary>
        ObjectId GetLayerTableId();

        /// <summary>
        /// 获取文字样式表ID
        /// </summary>
        ObjectId GetTextStyleTableId();

        /// <summary>
        /// 获取标注样式表ID
        /// </summary>
        ObjectId GetDimStyleTableId();

        /// <summary>
        /// 获取模型空间中的所有实体
        /// </summary>
        ObjectId[] GetAllEntitiesInModelSpace();

        /// <summary>
        /// 获取指定块中的所有实体
        /// </summary>
        ObjectId[] GetAllEntitiesInBlock(string blockName);

        /// <summary>
        /// 获取指定图层上的所有实体
        /// </summary>
        ObjectId[] GetAllEntitiesOnLayer(string layerName);

        /// <summary>
        /// 获取指定类型的所有实体
        /// </summary>
        ObjectId[] GetAllEntitiesByType<T>() where T : Entity;

        /// <summary>
        /// 检查块是否存在
        /// </summary>
        bool BlockExists(string blockName);

        /// <summary>
        /// 获取块ID
        /// </summary>
        ObjectId GetBlockId(string blockName);

        /// <summary>
        /// 获取所有块引用
        /// </summary>
        ObjectId[] GetAllBlockReferences(string blockName);

        /// <summary>
        /// 检查图层是否存在
        /// </summary>
        bool LayerExists(string layerName);

        /// <summary>
        /// 获取图层ID
        /// </summary>
        ObjectId GetLayerId(string layerName);

        /// <summary>
        /// 获取所有图层名称
        /// </summary>
        List<string> GetAllLayerNames();
    }
}
