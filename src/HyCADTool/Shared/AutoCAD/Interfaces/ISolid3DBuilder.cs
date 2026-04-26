using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Shared.AutoCAD.Interfaces
{
    /// <summary>
    /// 3D 实体构建器接口
    /// 负责创建各种 AutoCAD 3D 实体
    /// </summary>
    public interface ISolid3DBuilder
    {
        /// <summary>
        /// 创建墙体实体（矩形拉伸）
        /// </summary>
        /// <param name="v1">边的起点</param>
        /// <param name="v2">边的终点</param>
        /// <param name="bottomElevation">底部标高</param>
        /// <param name="wallHeight">墙体高度</param>
        /// <param name="outwardNormal">外向法向量</param>
        /// <param name="wallThickness">墙体厚度</param>
        /// <param name="offsetDirection">偏移方向（1 或 -1）</param>
        /// <returns>墙体 Solid3d 对象</returns>
        Solid3d CreateWallSolid(
            Point2D v1, 
            Point2D v2, 
            double bottomElevation, 
            double wallHeight, 
            Vector2D outwardNormal, 
            double wallThickness, 
            int offsetDirection = 1);
        
        /// <summary>
        /// 创建筏板实体（多边形拉伸）
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <param name="bottomElevation">底部标高</param>
        /// <param name="height">筏板高度</param>
        /// <returns>筏板 Solid3d 对象</returns>
        Solid3d CreateSlabSolid(
            Polygon2D polygon, 
            double bottomElevation, 
            double height);
    }
}

