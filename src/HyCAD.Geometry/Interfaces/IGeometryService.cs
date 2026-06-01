using System.Collections.Generic;
using HyCAD.Geometry;

namespace HyCAD.Geometry.Interfaces
{
    /// <summary>
    /// 几何服务接口（平台无关）
    /// 定义几何操作的抽象契约，由具体平台实现（AutoCAD、Blender等）
    /// </summary>
    public interface IGeometryService
    {
        /// <summary>
        /// 从点集创建多边形
        /// </summary>
        /// <param name="points">点集合</param>
        /// <returns>多边形领域对象</returns>
        Polygon2D CreatePolygonFromPoints(IEnumerable<Point2D> points);

        /// <summary>
        /// 判断点是否在多边形内部
        /// </summary>
        /// <param name="point">待判断的点</param>
        /// <param name="polygon">多边形</param>
        /// <returns>如果点在多边形内返回 true</returns>
        bool IsPointInsidePolygon(Point2D point, Polygon2D polygon);

        /// <summary>
        /// 计算两条线段的交点
        /// </summary>
        /// <param name="line1">线段1</param>
        /// <param name="line2">线段2</param>
        /// <returns>交点集合（可能为空、一个或多个点）</returns>
        IEnumerable<Point2D> GetIntersectionPoints(Line2D line1, Line2D line2);

        /// <summary>
        /// 计算多个多边形的并集
        /// </summary>
        /// <param name="polygons">多边形集合</param>
        /// <returns>并集结果（可能是多个多边形）</returns>
        IEnumerable<Polygon2D> Union(IEnumerable<Polygon2D> polygons);

        /// <summary>
        /// 计算多边形差集（subject - clip）
        /// </summary>
        /// <param name="subject">被裁剪的多边形</param>
        /// <param name="clip">裁剪多边形</param>
        /// <returns>差集结果（可能是多个多边形）</returns>
        IEnumerable<Polygon2D> Difference(Polygon2D subject, Polygon2D clip);

        /// <summary>
        /// 计算多边形交集
        /// </summary>
        /// <param name="subject">多边形1</param>
        /// <param name="clip">多边形2</param>
        /// <returns>交集结果（可能是多个多边形）</returns>
        IEnumerable<Polygon2D> Intersection(Polygon2D subject, Polygon2D clip);

        /// <summary>
        /// 计算多边形偏移
        /// </summary>
        /// <param name="polygon">原多边形</param>
        /// <param name="distance">偏移距离（正值向外，负值向内）</param>
        /// <returns>偏移后的多边形集合</returns>
        IEnumerable<Polygon2D> Offset(Polygon2D polygon, double distance);

        /// <summary>
        /// 计算多边形异或（对称差）
        /// </summary>
        /// <param name="polygon1">多边形1</param>
        /// <param name="polygon2">多边形2</param>
        /// <returns>异或结果</returns>
        IEnumerable<Polygon2D> Xor(Polygon2D polygon1, Polygon2D polygon2);

        /// <summary>
        /// 计算点集的凸包
        /// </summary>
        /// <param name="points">点集合</param>
        /// <returns>凸包多边形</returns>
        Polygon2D ComputeConvexHull(IEnumerable<Point2D> points);

        /// <summary>
        /// 合并重叠的线段
        /// </summary>
        /// <param name="lines">线段集合</param>
        /// <param name="tolerance">容差</param>
        /// <returns>合并后的线段列表</returns>
        List<Line2D> MergeOverlappingLines(IEnumerable<Line2D> lines, double tolerance);
    }
}

