using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.Elevation.Domain.Entities;
using HyCAD.Geometry;

namespace HyCADTool.Features.Elevation.Services
{
    /// <summary>
    /// 3D几何体构建器接口
    /// 负责将平台无关的3D实体转换为AutoCAD Solid3d对象
    /// </summary>
    public interface IGeometry3DBuilder
    {
        /// <summary>
        /// 从墙体实体创建AutoCAD 3D Solid
        /// </summary>
        /// <param name="wall">墙体3D实体</param>
        /// <returns>AutoCAD Solid3d对象</returns>
        Solid3d CreateWallSolid(Wall3D wall);
        
        /// <summary>
        /// 在事务上下文中创建墙体 Solid
        /// 注意：CreateExtrudedSolid 要求 Polyline 必须在数据库中（Database-Resident）
        /// </summary>
        /// <param name="wall">墙体3D实体</param>
        /// <param name="tr">事务对象</param>
        /// <param name="modelSpace">模型空间</param>
        /// <returns>AutoCAD Solid3d对象</returns>
        Solid3d CreateWallSolidInTransaction(
            Wall3D wall,
            Transaction tr,
            BlockTableRecord modelSpace);
        
        /// <summary>
        /// 从筏板实体创建AutoCAD 3D Solid
        /// </summary>
        /// <param name="slab">筏板3D实体</param>
        /// <returns>AutoCAD Solid3d对象</returns>
        Solid3d CreateSlabSolid(Slab3D slab);
        
        /// <summary>
        /// 批量创建墙体Solid
        /// </summary>
        /// <param name="walls">墙体列表</param>
        /// <returns>Solid3d列表</returns>
        List<Solid3d> CreateWallSolids(IEnumerable<Wall3D> walls);
        
        /// <summary>
        /// 批量创建筏板Solid
        /// </summary>
        /// <param name="slabs">筏板列表</param>
        /// <returns>Solid3d列表</returns>
        List<Solid3d> CreateSlabSolids(IEnumerable<Slab3D> slabs);
        
        /// <summary>
        /// 从平台无关的Polygon2D创建AutoCAD Polyline
        /// </summary>
        /// <param name="polygon">2D多边形</param>
        /// <returns>AutoCAD Polyline对象</returns>
        Polyline CreatePolyline(Polygon2D polygon);
        
        /// <summary>
        /// 拉伸多边形创建3D Solid
        /// </summary>
        /// <param name="polygon">底面多边形</param>
        /// <param name="bottomElevation">底部标高（米）</param>
        /// <param name="topElevation">顶部标高（米）</param>
        /// <returns>AutoCAD Solid3d对象</returns>
        Solid3d ExtrudePolygon(
            Polygon2D polygon,
            double bottomElevation,
            double topElevation);
    }
}



