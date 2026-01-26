using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces
{
    /// <summary>
    /// 绘图服务接口
    /// 提供创建各种AutoCAD实体的功能
    /// </summary>
    public interface IDrawingService
    {
        /// <summary>
        /// 绘制直线
        /// </summary>
        ObjectId DrawLine(Point3d start, Point3d end, string layerName = null);

        /// <summary>
        /// 绘制多段线
        /// </summary>
        ObjectId DrawPolyline(IEnumerable<Point2d> points, string layerName = null, bool isClosed = false);

        /// <summary>
        /// 绘制圆
        /// </summary>
        ObjectId DrawCircle(Point3d center, double radius, string layerName = null);

        /// <summary>
        /// 绘制圆弧
        /// </summary>
        ObjectId DrawArc(Point3d center, double radius, double startAngle, double endAngle, string layerName = null);

        /// <summary>
        /// 绘制椭圆
        /// </summary>
        ObjectId DrawEllipse(Point3d center, Vector3d majorAxis, double radiusRatio, string layerName = null);

        /// <summary>
        /// 绘制单行文本
        /// </summary>
        ObjectId DrawText(Point3d position, string content, double height, string layerName = null);

        /// <summary>
        /// 绘制多行文本
        /// </summary>
        ObjectId DrawMText(Point3d position, string content, double height, double width, string layerName = null);

        /// <summary>
        /// 绘制对齐标注
        /// </summary>
        ObjectId DrawAlignedDimension(Point3d start, Point3d end, Point3d textPoint, string layerName = null);

        /// <summary>
        /// 绘制旋转标注
        /// </summary>
        ObjectId DrawRotatedDimension(Point3d start, Point3d end, Point3d textPoint, double rotation, string layerName = null);

        /// <summary>
        /// 批量绘制实体
        /// </summary>
        ObjectId[] DrawEntities(IEnumerable<Entity> entities, string layerName = null);

        /// <summary>
        /// 绘制实体到块
        /// </summary>
        ObjectId DrawToBlock(Entity entity, string blockName, string layerName = null);

        /// <summary>
        /// 删除实体
        /// </summary>
        void DeleteEntity(ObjectId entityId);

        /// <summary>
        /// 批量删除实体
        /// </summary>
        void DeleteEntities(IEnumerable<ObjectId> entityIds);
    }
}
