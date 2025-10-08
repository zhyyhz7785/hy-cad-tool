namespace HyCADTool.Refactored.Domain.Interfaces
{
    /// <summary>
    /// 图层服务接口（平台无关）
    /// 定义图层管理的抽象操作
    /// </summary>
    public interface ILayerService
    {
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
    }
}

