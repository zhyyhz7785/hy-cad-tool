using HyCADTool.Shared.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Domain.Services.GeometryAlgorithms
{
    /// <summary>
    /// 多边形算法服务接口（平台无关）
    /// 提供多边形相关的几何算法
    /// </summary>
    public interface IPolygonAlgorithmService
    {
        // === 多边形顶点处理 ===

        /// <summary>
        /// 去除多边形中的重复顶点
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <param name="tolerance">容差</param>
        /// <returns>去重后的多边形</returns>
        Polygon2D RemoveDuplicateVertices(Polygon2D polygon, Tolerance tolerance);

        /// <summary>
        /// 设置多边形为顺时针方向
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <returns>顺时针方向的多边形</returns>
        Polygon2D SetClockwise(Polygon2D polygon);

        /// <summary>
        /// 设置多边形为逆时针方向
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <returns>逆时针方向的多边形</returns>
        Polygon2D SetCounterclockwise(Polygon2D polygon);

        /// <summary>
        /// 判断多边形是否为顺时针方向
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <returns>是否为顺时针</returns>
        bool IsClockwise(Polygon2D polygon);

        /// <summary>
        /// 重置多边形顶点顺序（从最小点开始）
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <returns>重置后的多边形</returns>
        Polygon2D ResetVertexOrder(Polygon2D polygon);

        // === 多边形属性计算 ===

        /// <summary>
        /// 计算多边形面积
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <returns>面积</returns>
        double CalculateArea(Polygon2D polygon);

        /// <summary>
        /// 计算多边形周长
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <returns>周长</returns>
        double CalculatePerimeter(Polygon2D polygon);

        /// <summary>
        /// 计算多边形质心
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <returns>质心</returns>
        Point2D CalculateCentroid(Polygon2D polygon);

        /// <summary>
        /// 计算多边形的边界框
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <returns>边界框（最小点，最大点）</returns>
        (Point2D MinPoint, Point2D MaxPoint) CalculateBoundingBox(Polygon2D polygon);

        // === 多边形空间关系 ===

        /// <summary>
        /// 判断点是否在多边形内部
        /// </summary>
        /// <param name="point">点</param>
        /// <param name="polygon">多边形</param>
        /// <param name="tolerance">容差</param>
        /// <returns>是否在内部</returns>
        bool IsPointInside(Point2D point, Polygon2D polygon, Tolerance tolerance);

        /// <summary>
        /// 判断点是否在多边形边界上
        /// </summary>
        /// <param name="point">点</param>
        /// <param name="polygon">多边形</param>
        /// <param name="tolerance">容差</param>
        /// <returns>是否在边界上</returns>
        bool IsPointOnBoundary(Point2D point, Polygon2D polygon, Tolerance tolerance);

        /// <summary>
        /// 判断两个多边形是否相交
        /// </summary>
        /// <param name="polygon1">第一个多边形</param>
        /// <param name="polygon2">第二个多边形</param>
        /// <param name="tolerance">容差</param>
        /// <returns>是否相交</returns>
        bool DoPolygonsIntersect(Polygon2D polygon1, Polygon2D polygon2, Tolerance tolerance);

        /// <summary>
        /// 计算多边形与多边形的交集
        /// </summary>
        /// <param name="polygon1">第一个多边形</param>
        /// <param name="polygon2">第二个多边形</param>
        /// <param name="tolerance">容差</param>
        /// <returns>交集多边形列表</returns>
        List<Polygon2D> CalculateIntersection(Polygon2D polygon1, Polygon2D polygon2, Tolerance tolerance);

        /// <summary>
        /// 计算多边形与多边形的并集
        /// </summary>
        /// <param name="polygon1">第一个多边形</param>
        /// <param name="polygon2">第二个多边形</param>
        /// <param name="tolerance">容差</param>
        /// <returns>并集多边形列表</returns>
        List<Polygon2D> CalculateUnion(Polygon2D polygon1, Polygon2D polygon2, Tolerance tolerance);

        // === 多边形变换 ===

        /// <summary>
        /// 平移多边形
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <param name="deltaX">X方向偏移</param>
        /// <param name="deltaY">Y方向偏移</param>
        /// <returns>平移后的多边形</returns>
        Polygon2D Translate(Polygon2D polygon, double deltaX, double deltaY);

        /// <summary>
        /// 旋转多边形
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <param name="angle">旋转角度（弧度）</param>
        /// <param name="center">旋转中心</param>
        /// <returns>旋转后的多边形</returns>
        Polygon2D Rotate(Polygon2D polygon, double angle, Point2D center);

        /// <summary>
        /// 缩放多边形
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <param name="scaleX">X方向缩放比例</param>
        /// <param name="scaleY">Y方向缩放比例</param>
        /// <param name="center">缩放中心</param>
        /// <returns>缩放后的多边形</returns>
        Polygon2D Scale(Polygon2D polygon, double scaleX, double scaleY, Point2D center);

        // === 多边形构造 ===

        /// <summary>
        /// 从线段列表创建多边形
        /// </summary>
        /// <param name="lines">线段列表</param>
        /// <param name="tolerance">容差</param>
        /// <returns>多边形（如果可以构成闭合多边形）</returns>
        Polygon2D CreateFromLines(List<Line2D> lines, Tolerance tolerance);

        /// <summary>
        /// 从点列表创建多边形
        /// </summary>
        /// <param name="points">点列表</param>
        /// <param name="isClosed">是否闭合</param>
        /// <returns>多边形</returns>
        Polygon2D CreateFromPoints(List<Point2D> points, bool isClosed = true);

        /// <summary>
        /// 创建矩形多边形
        /// </summary>
        /// <param name="minPoint">最小点</param>
        /// <param name="maxPoint">最大点</param>
        /// <returns>矩形多边形</returns>
        Polygon2D CreateRectangle(Point2D minPoint, Point2D maxPoint);

        /// <summary>
        /// 创建圆形近似多边形
        /// </summary>
        /// <param name="center">圆心</param>
        /// <param name="radius">半径</param>
        /// <param name="segments">分段数</param>
        /// <returns>圆形近似多边形</returns>
        Polygon2D CreateCircle(Point2D center, double radius, int segments = 32);

        // === 多边形简化 ===

        /// <summary>
        /// 简化多边形（减少顶点数，保持形状）
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <param name="tolerance">简化容差</param>
        /// <returns>简化后的多边形</returns>
        Polygon2D Simplify(Polygon2D polygon, double tolerance);

        /// <summary>
        /// 多边形光滑处理
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <param name="iterations">光滑迭代次数</param>
        /// <returns>光滑后的多边形</returns>
        Polygon2D Smooth(Polygon2D polygon, int iterations = 1);

        // === 多边形分析 ===

        /// <summary>
        /// 判断多边形是否为凸多边形
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <param name="tolerance">容差</param>
        /// <returns>是否为凸多边形</returns>
        bool IsConvex(Polygon2D polygon, Tolerance tolerance);

        /// <summary>
        /// 判断多边形是否为简单多边形（无自相交）
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <param name="tolerance">容差</param>
        /// <returns>是否为简单多边形</returns>
        bool IsSimple(Polygon2D polygon, Tolerance tolerance);

        /// <summary>
        /// 获取多边形的所有边（线段）
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <returns>边的列表</returns>
        List<Line2D> GetEdges(Polygon2D polygon);

        /// <summary>
        /// 获取多边形与线段的交点
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <param name="line">线段</param>
        /// <param name="tolerance">容差</param>
        /// <returns>交点列表</returns>
        List<Point2D> GetIntersectionPoints(Polygon2D polygon, Line2D line, Tolerance tolerance);
    }
}

