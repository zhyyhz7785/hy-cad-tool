using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Converters
{
    /// <summary>
    /// 几何类型转换器接口
    /// 负责领域几何对象 ↔ AutoCAD 几何对象的双向转换
    /// </summary>
    public interface IGeometryConverter
    {
        // ========== Domain → AutoCAD ==========

        /// <summary>
        /// 将领域 Point2D 转换为 AutoCAD Point2d
        /// </summary>
        Point2d ToAutoCADPoint2d(Domain.ValueObjects.Geometry.Point2D domainPoint);

        /// <summary>
        /// 将领域 Point3D 转换为 AutoCAD Point3d
        /// </summary>
        Point3d ToAutoCADPoint3d(Domain.ValueObjects.Geometry.Point3D domainPoint);

        /// <summary>
        /// 将领域 Polygon2D 转换为 AutoCAD Polyline
        /// </summary>
        Polyline ToAutoCADPolyline(Domain.ValueObjects.Geometry.Polygon2D domainPolygon);

        /// <summary>
        /// 将领域 Line2D 转换为 AutoCAD Line
        /// </summary>
        Line ToAutoCADLine(Domain.ValueObjects.Geometry.Line2D domainLine);

        // ========== AutoCAD → Domain ==========

        /// <summary>
        /// 将 AutoCAD Point2d 转换为领域 Point2D
        /// </summary>
        Domain.ValueObjects.Geometry.Point2D FromAutoCADPoint2d(Point2d acPoint);

        /// <summary>
        /// 将 AutoCAD Point3d 转换为领域 Point3D
        /// </summary>
        Domain.ValueObjects.Geometry.Point3D FromAutoCADPoint3d(Point3d acPoint);

        /// <summary>
        /// 将 AutoCAD Polyline 转换为领域 Polygon2D
        /// </summary>
        Domain.ValueObjects.Geometry.Polygon2D FromAutoCADPolyline(Polyline acPolyline);

        /// <summary>
        /// 将 AutoCAD Line 转换为领域 Line2D
        /// </summary>
        Domain.ValueObjects.Geometry.Line2D FromAutoCADLine(Line acLine);

        // ========== Circle 转换 ==========

        /// <summary>
        /// 将领域 Circle2D 转换为 AutoCAD Circle
        /// </summary>
        Circle ToAutoCADCircle(Domain.ValueObjects.Geometry.Circle2D domainCircle);

        /// <summary>
        /// 将 AutoCAD Circle 转换为领域 Circle2D
        /// </summary>
        Domain.ValueObjects.Geometry.Circle2D FromAutoCADCircle(Circle acCircle);

        // ========== 批量转换 ==========

        /// <summary>
        /// 批量将领域 Line2D 转换为 AutoCAD Line
        /// </summary>
        List<Line> ToAutoCADLines(IEnumerable<Domain.ValueObjects.Geometry.Line2D> domainLines);

        /// <summary>
        /// 批量将 AutoCAD Line 转换为领域 Line2D
        /// </summary>
        List<Domain.ValueObjects.Geometry.Line2D> FromAutoCADLines(IEnumerable<Line> acLines);
    }
}

