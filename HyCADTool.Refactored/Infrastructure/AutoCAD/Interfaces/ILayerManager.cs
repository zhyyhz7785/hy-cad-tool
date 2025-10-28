using Autodesk.AutoCAD.DatabaseServices;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces
{
    /// <summary>
    /// 图层管理器接口
    /// 负责图层的创建、配置和管理
    /// </summary>
    public interface ILayerManager
    {
        /// <summary>
        /// 确保图层存在，如果不存在则创建
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="layerName">图层名称</param>
        /// <param name="colorIndex">颜色索引</param>
        void EnsureLayer(Transaction tr, string layerName, short colorIndex);
        
        /// <summary>
        /// 批量创建图层
        /// </summary>
        void EnsureLayers(Transaction tr, params (string name, short colorIndex)[] layers);
    }
}

