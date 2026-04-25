using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Shared.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Shared.AutoCAD.Converters
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
        Point2d ToAutoCADPoint2d(HyCADTool.Shared.Geometry.Point2D domainPoint);

        /// <summary>
        /// 将领域 Point3D 转换为 AutoCAD Point3d
        /// </summary>
        Point3d ToAutoCADPoint3d(HyCADTool.Shared.Geometry.Point3D domainPoint);

        /// <summary>
        /// 将领域 Polygon2D 转换为 AutoCAD Polyline
        /// </summary>
        Polyline ToAutoCADPolyline(HyCADTool.Shared.Geometry.Polygon2D domainPolygon);

        /// <summary>
        /// 将领域 Line2D 转换为 AutoCAD Line
        /// </summary>
        Line ToAutoCADLine(HyCADTool.Shared.Geometry.Line2D domainLine);

        // ========== AutoCAD → Domain ==========

        /// <summary>
        /// 将 AutoCAD Point2d 转换为领域 Point2D
        /// </summary>
        HyCADTool.Shared.Geometry.Point2D FromAutoCADPoint2d(Point2d acPoint);

        /// <summary>
        /// 将 AutoCAD Point3d 转换为领域 Point3D
        /// </summary>
        HyCADTool.Shared.Geometry.Point3D FromAutoCADPoint3d(Point3d acPoint);

        /// <summary>
        /// 将 AutoCAD Polyline 转换为领域 Polygon2D
        /// </summary>
        HyCADTool.Shared.Geometry.Polygon2D FromAutoCADPolyline(Polyline acPolyline);

        /// <summary>
        /// 将 AutoCAD Line 转换为领域 Line2D
        /// </summary>
        HyCADTool.Shared.Geometry.Line2D FromAutoCADLine(Line acLine);

        // ========== Circle 转换 ==========

        /// <summary>
        /// 将领域 Circle2D 转换为 AutoCAD Circle
        /// </summary>
        Circle ToAutoCADCircle(HyCADTool.Shared.Geometry.Circle2D domainCircle);

        /// <summary>
        /// 将 AutoCAD Circle 转换为领域 Circle2D
        /// </summary>
        HyCADTool.Shared.Geometry.Circle2D FromAutoCADCircle(Circle acCircle);

        // ========== 批量转换 ==========

        /// <summary>
        /// 批量将领域 Line2D 转换为 AutoCAD Line
        /// </summary>
        List<Line> ToAutoCADLines(IEnumerable<HyCADTool.Shared.Geometry.Line2D> domainLines);

        /// <summary>
        /// 批量将 AutoCAD Line 转换为领域 Line2D
        /// </summary>
        List<HyCADTool.Shared.Geometry.Line2D> FromAutoCADLines(IEnumerable<Line> acLines);
    }
}

