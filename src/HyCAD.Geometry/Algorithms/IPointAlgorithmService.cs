using HyCAD.Geometry;
using System.Collections.Generic;

namespace HyCAD.Geometry.Algorithms
{
    /// <summary>
    /// 点算法服务接口（平台无关）
    /// 提供点相关的几何算法
    /// </summary>
    public interface IPointAlgorithmService
    {
        // === 距离计算 ===

        /// <summary>
        /// 计算两点之间的距离
        /// </summary>
        /// <param name="point1">第一个点</param>
        /// <param name="point2">第二个点</param>
        /// <returns>距离</returns>
        double CalculateDistance(Point2D point1, Point2D point2);

        /// <summary>
        /// 计算点到原点的距离
        /// </summary>
        /// <param name="point">点</param>
        /// <returns>距离</returns>
        double CalculateDistanceFromOrigin(Point2D point);

        /// <summary>
        /// 计算点集合中两两之间的最短距离
        /// </summary>
        /// <param name="points">点集合</param>
        /// <returns>最短距离</returns>
        double FindMinimumDistance(List<Point2D> points);

        /// <summary>
        /// 计算点集合中两两之间的最长距离
        /// </summary>
        /// <param name="points">点集合</param>
        /// <returns>最长距离</returns>
        double FindMaximumDistance(List<Point2D> points);

        // === 点的搜索与排序 ===

        /// <summary>
        /// 在点集合中查找距离目标点最近的点
        /// </summary>
        /// <param name="targetPoint">目标点</param>
        /// <param name="points">点集合</param>
        /// <returns>最近的点</returns>
        Point2D? FindClosestPoint(Point2D targetPoint, List<Point2D> points);

        /// <summary>
        /// 在点集合中查找距离目标点最远的点
        /// </summary>
        /// <param name="targetPoint">目标点</param>
        /// <param name="points">点集合</param>
        /// <returns>最远的点</returns>
        Point2D? FindFarthestPoint(Point2D targetPoint, List<Point2D> points);

        /// <summary>
        /// 按距离目标点的远近对点集合排序
        /// </summary>
        /// <param name="targetPoint">目标点</param>
        /// <param name="points">点集合</param>
        /// <param name="ascending">是否升序排列</param>
        /// <returns>排序后的点集合</returns>
        List<Point2D> SortByDistance(Point2D targetPoint, List<Point2D> points, bool ascending = true);

        /// <summary>
        /// 在指定半径范围内查找所有点
        /// </summary>
        /// <param name="centerPoint">中心点</param>
        /// <param name="radius">搜索半径</param>
        /// <param name="points">点集合</param>
        /// <returns>范围内的点集合</returns>
        List<Point2D> FindPointsInRadius(Point2D centerPoint, double radius, List<Point2D> points);

        // === 点的空间关系判断 ===

        /// <summary>
        /// 判断两点是否在容差范围内相等
        /// </summary>
        /// <param name="point1">第一个点</param>
        /// <param name="point2">第二个点</param>
        /// <param name="tolerance">容差</param>
        /// <returns>是否相等</returns>
        bool IsPointsEqual(Point2D point1, Point2D point2, Tolerance tolerance);

        /// <summary>
        /// 判断点是否在指定的矩形区域内
        /// </summary>
        /// <param name="point">点</param>
        /// <param name="minPoint">矩形最小点</param>
        /// <param name="maxPoint">矩形最大点</param>
        /// <returns>是否在区域内</returns>
        bool IsPointInRectangle(Point2D point, Point2D minPoint, Point2D maxPoint);

        /// <summary>
        /// 判断点是否在指定的圆形区域内
        /// </summary>
        /// <param name="point">点</param>
        /// <param name="center">圆心</param>
        /// <param name="radius">半径</param>
        /// <returns>是否在区域内</returns>
        bool IsPointInCircle(Point2D point, Point2D center, double radius);

        /// <summary>
        /// 判断三点是否共线
        /// </summary>
        /// <param name="point1">第一个点</param>
        /// <param name="point2">第二个点</param>
        /// <param name="point3">第三个点</param>
        /// <param name="tolerance">容差</param>
        /// <returns>是否共线</returns>
        bool AreCollinear(Point2D point1, Point2D point2, Point2D point3, Tolerance tolerance);

        // === 角度和方向计算 ===

        /// <summary>
        /// 计算从第一个点到第二个点的角度（弧度）
        /// </summary>
        /// <param name="fromPoint">起始点</param>
        /// <param name="toPoint">目标点</param>
        /// <returns>角度（弧度）</returns>
        double CalculateAngle(Point2D fromPoint, Point2D toPoint);

        /// <summary>
        /// 计算三点形成的角度（弧度）
        /// </summary>
        /// <param name="point1">第一个点</param>
        /// <param name="vertex">顶点</param>
        /// <param name="point2">第二个点</param>
        /// <returns>角度（弧度）</returns>
        double CalculateAngleAt(Point2D point1, Point2D vertex, Point2D point2);

        /// <summary>
        /// 计算三点的叉积（判断转向）
        /// </summary>
        /// <param name="point1">第一个点</param>
        /// <param name="point2">第二个点</param>
        /// <param name="point3">第三个点</param>
        /// <returns>叉积值（正值表示逆时针，负值表示顺时针）</returns>
        double CalculateCrossProduct(Point2D point1, Point2D point2, Point2D point3);

        // === 点的变换 ===

        /// <summary>
        /// 平移点
        /// </summary>
        /// <param name="point">原点</param>
        /// <param name="deltaX">X方向偏移</param>
        /// <param name="deltaY">Y方向偏移</param>
        /// <returns>平移后的点</returns>
        Point2D? Translate(Point2D point, double deltaX, double deltaY);

        /// <summary>
        /// 绕指定中心旋转点
        /// </summary>
        /// <param name="point">原点</param>
        /// <param name="center">旋转中心</param>
        /// <param name="angle">旋转角度（弧度）</param>
        /// <returns>旋转后的点</returns>
        Point2D? Rotate(Point2D point, Point2D center, double angle);

        /// <summary>
        /// 相对于指定中心缩放点
        /// </summary>
        /// <param name="point">原点</param>
        /// <param name="center">缩放中心</param>
        /// <param name="scaleX">X方向缩放比例</param>
        /// <param name="scaleY">Y方向缩放比例</param>
        /// <returns>缩放后的点</returns>
        Point2D? Scale(Point2D point, Point2D center, double scaleX, double scaleY);

        /// <summary>
        /// 将点投影到直线上
        /// </summary>
        /// <param name="point">原点</param>
        /// <param name="line">目标直线</param>
        /// <returns>投影点</returns>
        Point2D? ProjectToLine(Point2D point, Line2D line);

        // === 点集合处理 ===

        /// <summary>
        /// 去除点集合中的重复点
        /// </summary>
        /// <param name="points">点集合</param>
        /// <param name="tolerance">容差</param>
        /// <returns>去重后的点集合</returns>
        List<Point2D> RemoveDuplicates(List<Point2D> points, Tolerance tolerance);

        /// <summary>
        /// 计算点集合的质心
        /// </summary>
        /// <param name="points">点集合</param>
        /// <returns>质心</returns>
        Point2D? CalculateCentroid(List<Point2D> points);

        /// <summary>
        /// 计算点集合的边界框
        /// </summary>
        /// <param name="points">点集合</param>
        /// <returns>边界框（最小点，最大点）</returns>
        (Point2D? MinPoint, Point2D? MaxPoint) CalculateBoundingBox(List<Point2D> points);

        /// <summary>
        /// 计算凸包（Graham扫描算法）
        /// </summary>
        /// <param name="points">点集合</param>
        /// <returns>凸包顶点</returns>
        List<Point2D> CalculateConvexHull(List<Point2D> points);

        // === 点的比较器创建 ===

        /// <summary>
        /// 创建基于容差的点比较器
        /// </summary>
        /// <param name="tolerance">容差</param>
        /// <returns>点比较器</returns>
        IEqualityComparer<Point2D> CreateComparer(Tolerance tolerance);

        /// <summary>
        /// 创建基于距离的点比较器
        /// </summary>
        /// <param name="referencePoint">参考点</param>
        /// <returns>点比较器</returns>
        IComparer<Point2D> CreateDistanceComparer(Point2D referencePoint);

        /// <summary>
        /// 创建基于极角的点比较器（用于凸包算法）
        /// </summary>
        /// <param name="referencePoint">参考点</param>
        /// <returns>点比较器</returns>
        IComparer<Point2D> CreatePolarAngleComparer(Point2D referencePoint);

        // === 特殊点计算 ===

        /// <summary>
        /// 计算两点的中点
        /// </summary>
        /// <param name="point1">第一个点</param>
        /// <param name="point2">第二个点</param>
        /// <returns>中点</returns>
        Point2D? CalculateMidpoint(Point2D point1, Point2D point2);

        /// <summary>
        /// 在两点之间按比例插值
        /// </summary>
        /// <param name="point1">第一个点</param>
        /// <param name="point2">第二个点</param>
        /// <param name="ratio">插值比例（0-1）</param>
        /// <returns>插值点</returns>
        Point2D? Interpolate(Point2D point1, Point2D point2, double ratio);

        /// <summary>
        /// 计算三角形的外心
        /// </summary>
        /// <param name="point1">第一个顶点</param>
        /// <param name="point2">第二个顶点</param>
        /// <param name="point3">第三个顶点</param>
        /// <returns>外心（如果三点共线返回null）</returns>
        Point2D? CalculateCircumcenter(Point2D point1, Point2D point2, Point2D point3);

        /// <summary>
        /// 计算三角形的内心
        /// </summary>
        /// <param name="point1">第一个顶点</param>
        /// <param name="point2">第二个顶点</param>
        /// <param name="point3">第三个顶点</param>
        /// <returns>内心</returns>
        Point2D? CalculateIncenter(Point2D point1, Point2D point2, Point2D point3);
    }
}
