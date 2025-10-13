using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Domain.Interfaces
{
    /// <summary>
    /// 实体绘制服务接口 (Entity Drawing Service Interface)
    /// 负责在 AutoCAD 中绘制各种图形实体
    /// </summary>
    /// <remarks>
    /// 设计原则 (Design Principles):
    /// 1. 简化绘制操作 (Simplified Drawing Operations) - 封装复杂的事务管理
    /// 2. 图层支持 (Layer Support) - 允许指定实体所在图层
    /// 3. 批量操作 (Batch Operations) - 支持批量绘制以提高性能
    /// </remarks>
    public interface IDrawingService
    {
        #region 基础几何图形绘制 (Basic Geometry Drawing)

        /// <summary>
        /// 绘制直线 (Draw Line)
        /// </summary>
        /// <param name="start">起点 (Start Point)</param>
        /// <param name="end">终点 (End Point)</param>
        /// <param name="layerName">图层名称 (Layer Name)，为空则使用当前图层</param>
        /// <returns>创建的实体 ObjectId</returns>
        ObjectId DrawLine(Point3d start, Point3d end, string layerName = null);

        /// <summary>
        /// 绘制多段线 (Draw Polyline)
        /// </summary>
        /// <param name="points">顶点集合 (Vertex Collection)</param>
        /// <param name="layerName">图层名称 (Layer Name)</param>
        /// <param name="isClosed">是否闭合 (Is Closed)</param>
        /// <returns>创建的实体 ObjectId</returns>
        ObjectId DrawPolyline(IEnumerable<Point2d> points, string layerName = null, bool isClosed = false);

        /// <summary>
        /// 绘制圆 (Draw Circle)
        /// </summary>
        /// <param name="center">圆心 (Center Point)</param>
        /// <param name="radius">半径 (Radius)</param>
        /// <param name="layerName">图层名称 (Layer Name)</param>
        /// <returns>创建的实体 ObjectId</returns>
        ObjectId DrawCircle(Point3d center, double radius, string layerName = null);

        /// <summary>
        /// 绘制圆弧 (Draw Arc)
        /// </summary>
        /// <param name="center">圆心 (Center Point)</param>
        /// <param name="radius">半径 (Radius)</param>
        /// <param name="startAngle">起始角度 (Start Angle)，单位弧度</param>
        /// <param name="endAngle">终止角度 (End Angle)，单位弧度</param>
        /// <param name="layerName">图层名称 (Layer Name)</param>
        /// <returns>创建的实体 ObjectId</returns>
        ObjectId DrawArc(Point3d center, double radius, double startAngle, double endAngle, string layerName = null);

        /// <summary>
        /// 绘制椭圆 (Draw Ellipse)
        /// </summary>
        /// <param name="center">中心点 (Center Point)</param>
        /// <param name="majorAxis">长轴向量 (Major Axis Vector)</param>
        /// <param name="radiusRatio">短轴与长轴的比率 (Radius Ratio)</param>
        /// <param name="layerName">图层名称 (Layer Name)</param>
        /// <returns>创建的实体 ObjectId</returns>
        ObjectId DrawEllipse(Point3d center, Vector3d majorAxis, double radiusRatio, string layerName = null);

        #endregion

        #region 文本绘制 (Text Drawing)

        /// <summary>
        /// 绘制单行文本 (Draw Single-line Text)
        /// </summary>
        /// <param name="position">插入点 (Insertion Point)</param>
        /// <param name="content">文本内容 (Text Content)</param>
        /// <param name="height">文字高度 (Text Height)</param>
        /// <param name="layerName">图层名称 (Layer Name)</param>
        /// <returns>创建的实体 ObjectId</returns>
        ObjectId DrawText(Point3d position, string content, double height, string layerName = null);

        /// <summary>
        /// 绘制多行文本 (Draw Multi-line Text)
        /// </summary>
        /// <param name="position">插入点 (Insertion Point)</param>
        /// <param name="content">文本内容 (Text Content)</param>
        /// <param name="height">文字高度 (Text Height)</param>
        /// <param name="width">文本框宽度 (Text Box Width)</param>
        /// <param name="layerName">图层名称 (Layer Name)</param>
        /// <returns>创建的实体 ObjectId</returns>
        ObjectId DrawMText(Point3d position, string content, double height, double width, string layerName = null);

        #endregion

        #region 标注绘制 (Dimension Drawing)

        /// <summary>
        /// 绘制对齐标注 (Draw Aligned Dimension)
        /// </summary>
        /// <param name="start">起点 (Start Point)</param>
        /// <param name="end">终点 (End Point)</param>
        /// <param name="textPoint">文字位置 (Text Position)</param>
        /// <param name="layerName">图层名称 (Layer Name)</param>
        /// <returns>创建的实体 ObjectId</returns>
        ObjectId DrawAlignedDimension(Point3d start, Point3d end, Point3d textPoint, string layerName = null);

        /// <summary>
        /// 绘制旋转标注 (Draw Rotated Dimension)
        /// </summary>
        /// <param name="start">起点 (Start Point)</param>
        /// <param name="end">终点 (End Point)</param>
        /// <param name="textPoint">文字位置 (Text Position)</param>
        /// <param name="rotation">旋转角度 (Rotation Angle)，单位弧度</param>
        /// <param name="layerName">图层名称 (Layer Name)</param>
        /// <returns>创建的实体 ObjectId</returns>
        ObjectId DrawRotatedDimension(Point3d start, Point3d end, Point3d textPoint, double rotation, string layerName = null);

        #endregion

        #region 批量绘制 (Batch Drawing)

        /// <summary>
        /// 批量绘制实体 (Batch Draw Entities)
        /// </summary>
        /// <param name="entities">实体集合 (Entity Collection)</param>
        /// <param name="layerName">图层名称 (Layer Name)，为空则保持各实体原图层</param>
        /// <returns>创建的实体 ObjectId 数组</returns>
        ObjectId[] DrawEntities(IEnumerable<Entity> entities, string layerName = null);

        #endregion

        #region Block 绘制 (Block Drawing)

        /// <summary>
        /// 绘制实体到指定 Block (Draw Entity to Block)
        /// </summary>
        /// <param name="entity">实体对象 (Entity Object)</param>
        /// <param name="blockName">Block 名称 (Block Name)</param>
        /// <param name="layerName">图层名称 (Layer Name)</param>
        /// <returns>创建的实体 ObjectId</returns>
        ObjectId DrawToBlock(Entity entity, string blockName, string layerName = null);

        #endregion

        #region 实体删除 (Entity Deletion)

        /// <summary>
        /// 删除实体 (Delete Entity)
        /// </summary>
        /// <param name="entityId">实体 ObjectId</param>
        void DeleteEntity(ObjectId entityId);

        /// <summary>
        /// 批量删除实体 (Batch Delete Entities)
        /// </summary>
        /// <param name="entityIds">实体 ObjectId 集合</param>
        void DeleteEntities(IEnumerable<ObjectId> entityIds);

        #endregion
    }
}

