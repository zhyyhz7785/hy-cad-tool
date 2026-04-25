using HyCADTool.Shared.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Domain.Services.GeometryAlgorithms
{
    /// <summary>
    /// 线段算法服务接口（平台无关）
    /// 提供线段相关的几何算法
    /// </summary>
    public interface ILineAlgorithmService
    {
        // === 线段重叠检查 ===

        /// <summary>
        /// 检查两线段是否重叠
        /// </summary>
        /// <param name="line1">第一条线段</param>
        /// <param name="line2">第二条线段</param>
        /// <param name="tolerance">容差</param>
        /// <param name="mergedLine">合并后的线段（如果重叠）</param>
        /// <returns>是否重叠</returns>
        bool CheckOverlap(Line2D line1, Line2D line2, Tolerance tolerance, out Line2D? mergedLine);

        /// <summary>
        /// 检查多条线段的重叠情况
        /// </summary>
        /// <param name="lines">线段列表</param>
        /// <param name="tolerance">容差</param>
        /// <returns>合并后的线段列表</returns>
        List<Line2D> MergeOverlappingLines(List<Line2D> lines, Tolerance tolerance);

        // === 共线性检查 ===

        /// <summary>
        /// 检查两线段是否共线
        /// </summary>
        /// <param name="line1">第一条线段</param>
        /// <param name="line2">第二条线段</param>
        /// <param name="tolerance">容差</param>
        /// <returns>是否共线</returns>
        bool IsCollinear(Line2D line1, Line2D line2, Tolerance tolerance);

        /// <summary>
        /// 查找共线的线段组
        /// </summary>
        /// <param name="lines">线段列表</param>
        /// <param name="tolerance">容差</param>
        /// <returns>共线线段组列表</returns>
        List<List<Line2D>> FindCollinearGroups(List<Line2D> lines, Tolerance tolerance);

        // === 线段交点计算 ===

        /// <summary>
        /// 计算两线段的交点
        /// </summary>
        /// <param name="line1">第一条线段</param>
        /// <param name="line2">第二条线段</param>
        /// <param name="tolerance">容差</param>
        /// <param name="intersection">交点</param>
        /// <returns>是否有交点</returns>
        bool FindIntersection(Line2D line1, Line2D line2, Tolerance tolerance, out Point2D? intersection);

        /// <summary>
        /// 查找线段与多条线段的所有交点
        /// </summary>
        /// <param name="line">目标线段</param>
        /// <param name="lines">其他线段列表</param>
        /// <param name="tolerance">容差</param>
        /// <returns>交点列表</returns>
        List<Point2D> FindAllIntersections(Line2D line, List<Line2D> lines, Tolerance tolerance);

        // === 距离计算 ===

        /// <summary>
        /// 计算点到线段的距离
        /// </summary>
        /// <param name="point">点</param>
        /// <param name="line">线段</param>
        /// <returns>距离</returns>
        double CalculateDistanceToPoint(Point2D point, Line2D line);

        /// <summary>
        /// 计算两线段之间的最短距离
        /// </summary>
        /// <param name="line1">第一条线段</param>
        /// <param name="line2">第二条线段</param>
        /// <returns>最短距离</returns>
        double CalculateDistanceBetweenLines(Line2D line1, Line2D line2);

        // === 线段连接与排序 ===

        /// <summary>
        /// 按连接性对线段进行排序
        /// </summary>
        /// <param name="lines">线段列表</param>
        /// <param name="tolerance">容差</param>
        /// <returns>排序后的线段列表</returns>
        List<Line2D> SortByConnectivity(List<Line2D> lines, Tolerance tolerance);

        /// <summary>
        /// 查找两线段的公共端点
        /// </summary>
        /// <param name="line1">第一条线段</param>
        /// <param name="line2">第二条线段</param>
        /// <param name="tolerance">容差</param>
        /// <param name="commonPoint">公共端点</param>
        /// <returns>是否有公共端点</returns>
        bool FindCommonPoint(Line2D line1, Line2D line2, Tolerance tolerance, out Point2D? commonPoint);

        // === 线段属性计算 ===

        /// <summary>
        /// 计算线段长度
        /// </summary>
        /// <param name="line">线段</param>
        /// <returns>长度</returns>
        double CalculateLength(Line2D line);

        /// <summary>
        /// 计算线段中点
        /// </summary>
        /// <param name="line">线段</param>
        /// <returns>中点</returns>
        Point2D? CalculateMidpoint(Line2D line);

        /// <summary>
        /// 计算线段的方向角（弧度）
        /// </summary>
        /// <param name="line">线段</param>
        /// <returns>方向角</returns>
        double CalculateAngle(Line2D line);

        // === 线段分类 ===

        /// <summary>
        /// 判断线段是否为水平线
        /// </summary>
        /// <param name="line">线段</param>
        /// <param name="tolerance">容差</param>
        /// <returns>是否为水平线</returns>
        bool IsHorizontal(Line2D line, Tolerance tolerance);

        /// <summary>
        /// 判断线段是否为垂直线
        /// </summary>
        /// <param name="line">线段</param>
        /// <param name="tolerance">容差</param>
        /// <returns>是否为垂直线</returns>
        bool IsVertical(Line2D line, Tolerance tolerance);

        /// <summary>
        /// 判断线段是否平行
        /// </summary>
        /// <param name="line1">第一条线段</param>
        /// <param name="line2">第二条线段</param>
        /// <param name="tolerance">容差</param>
        /// <returns>是否平行</returns>
        bool AreParallel(Line2D line1, Line2D line2, Tolerance tolerance);

        /// <summary>
        /// 判断线段是否垂直
        /// </summary>
        /// <param name="line1">第一条线段</param>
        /// <param name="line2">第二条线段</param>
        /// <param name="tolerance">容差</param>
        /// <returns>是否垂直</returns>
        bool ArePerpendicular(Line2D line1, Line2D line2, Tolerance tolerance);
    }
}
