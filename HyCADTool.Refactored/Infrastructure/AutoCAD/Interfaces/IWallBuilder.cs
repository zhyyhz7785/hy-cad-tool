using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces
{
    /// <summary>
    /// 墙体构建器接口
    /// 负责创建 AutoCAD 墙体实体
    /// </summary>
    public interface IWallBuilder
    {
        /// <summary>
        /// 创建墙体
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="edge">墙体边（内侧边）</param>
        /// <param name="wallThickness">墙体厚度（mm）</param>
        /// <param name="wallHeight">墙体高度（mm）</param>
        /// <param name="offsetDirection">偏移方向（1=向外，-1=向内）</param>
        /// <param name="baseElevation">基础标高（mm）</param>
        /// <param name="layerName">图层名称</param>
        /// <returns>创建的墙体实体</returns>
        Solid3d CreateWall(
            Transaction tr,
            Line2D edge,
            double wallThickness,
            double wallHeight,
            int offsetDirection,
            double baseElevation,
            string layerName);
    }
}

