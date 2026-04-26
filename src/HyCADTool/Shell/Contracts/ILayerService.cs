using System.Collections.Generic;
using HyCADTool.Shell.Configuration.User;

namespace HyCADTool.Shell.Contracts
{
    /// <summary>
    /// 图层服务接口（平台无关）
    /// 定义图层管理的抽象操作
    /// </summary>
    public interface ILayerService
    {
        // === 原有方法 ===
        
        /// <summary>
        /// 创建图层
        /// </summary>
        /// <param name="layerName">图层名称</param>
        /// <param name="colorIndex">颜色索引（AutoCAD ACI）</param>
        void CreateLayer(string layerName, short colorIndex);

        /// <summary>
        /// 设置当前图层
        /// </summary>
        /// <param name="layerName">图层名称</param>
        void SetCurrentLayer(string layerName);

        /// <summary>
        /// 检查图层是否存在
        /// </summary>
        /// <param name="layerName">图层名称</param>
        /// <returns>如果图层存在返回 true</returns>
        bool LayerExists(string layerName);

        /// <summary>
        /// 删除图层（如果为空）
        /// </summary>
        /// <param name="layerName">图层名称</param>
        /// <returns>删除成功返回 true</returns>
        bool DeleteLayer(string layerName);

        // === ZTools迁移方法 ===

        /// <summary>
        /// 创建带完整样式的图层
        /// </summary>
        /// <param name="layerName">图层名称</param>
        /// <param name="colorIndex">颜色索引</param>
        /// <param name="lineType">线型名称</param>
        /// <param name="lineWeight">线宽</param>
        /// <returns>图层ObjectId</returns>
        string CreateLayerWithStyle(string layerName, short colorIndex, string lineType, int lineWeight);

        /// <summary>
        /// 批量创建图层
        /// </summary>
        /// <param name="layerInfos">图层信息列表（名称，颜色索引）</param>
        void CreateMultipleLayers(params (string layerName, short colorIndex)[] layerInfos);

        /// <summary>
        /// 获取图层ID
        /// </summary>
        /// <param name="layerName">图层名称</param>
        /// <returns>图层ObjectId</returns>
        string GetLayerId(string layerName);

        /// <summary>
        /// 获取所有图层名称
        /// </summary>
        /// <returns>图层名称列表</returns>
        List<string> GetAllLayerNames();

        /// <summary>
        /// 设置实体到指定图层（扩展方法的接口版本）
        /// </summary>
        /// <param name="entityId">实体ID</param>
        /// <param name="layerName">图层名称</param>
        void SetEntityLayer(string entityId, string layerName);

        /// <summary>
        /// 按用户层表在任意文档中确保图层存在并更新颜色/线型/线宽等（用于 hy-settings 图层与初始化流程）。
        /// </summary>
        void EnsureUserLayerItems(IReadOnlyList<LayerDefinitionItem> items);
    }
}

