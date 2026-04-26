using HyCADTool.Shared.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Shared.Geometry.Algorithms
{
    /// <summary>
    /// 几何转换服务接口（平台无关）
    /// 提供几何对象之间的转换算法
    /// </summary>
    public interface IGeometryConverterService
    {
        // === 线段与多边形转换 ===

        /// <summary>
        /// 将线段转换为两点多边形
        /// </summary>
        /// <param name="line">线段</param>
        /// <returns>多边形</returns>
        Polygon2D LineToPolygon(Line2D line);

        /// <summary>
        /// 将多条线段连接为多边形
        /// </summary>
        /// <param name="lines">线段列表</param>
        /// <param name="tolerance">容差</param>
        /// <returns>多边形（如果可以形成闭合多边形）</returns>
        Polygon2D LinesToPolygon(List<Line2D> lines, Tolerance tolerance);

        /// <summary>
        /// 将多边形分解为线段
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <returns>线段列表</returns>
        List<Line2D> PolygonToLines(Polygon2D polygon);

        // === 多边形简化与复杂化 ===

        /// <summary>
        /// 将多边形转换为矩形边界框
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <returns>矩形多边形</returns>
        Polygon2D PolygonToBoundingBox(Polygon2D polygon);

        /// <summary>
        /// 将多边形转换为凸包
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <returns>凸包多边形</returns>
        Polygon2D PolygonToConvexHull(Polygon2D polygon);

        /// <summary>
        /// 将矩形转换为多边形
        /// </summary>
        /// <param name="minPoint">最小点</param>
        /// <param name="maxPoint">最大点</param>
        /// <returns>矩形多边形</returns>
        Polygon2D RectangleToPolygon(Point2D minPoint, Point2D maxPoint);

        /// <summary>
        /// 将圆近似为多边形
        /// </summary>
        /// <param name="center">圆心</param>
        /// <param name="radius">半径</param>
        /// <param name="segments">分段数</param>
        /// <returns>圆形近似多边形</returns>
        Polygon2D CircleToPolygon(Point2D center, double radius, int segments = 32);

        // === 点集合转换 ===

        /// <summary>
        /// 将点集合转换为多边形
        /// </summary>
        /// <param name="points">点列表</param>
        /// <param name="isClosed">是否闭合</param>
        /// <returns>多边形</returns>
        Polygon2D PointsToPolygon(List<Point2D> points, bool isClosed = true);

        /// <summary>
        /// 将多边形转换为点集合
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <returns>点列表</returns>
        List<Point2D> PolygonToPoints(Polygon2D polygon);

        /// <summary>
        /// 将点集合转换为线段集合（连续连线）
        /// </summary>
        /// <param name="points">点列表</param>
        /// <param name="isClosed">是否闭合</param>
        /// <returns>线段列表</returns>
        List<Line2D> PointsToLines(List<Point2D> points, bool isClosed = false);

        // === 几何格式化 ===

        /// <summary>
        /// 标准化多边形（去重、设置方向、重排顶点）
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <param name="clockwise">是否设置为顺时针</param>
        /// <param name="tolerance">容差</param>
        /// <returns>标准化后的多边形</returns>
        Polygon2D NormalizePolygon(Polygon2D polygon, bool clockwise, Tolerance tolerance);

        /// <summary>
        /// 标准化线段（确保起点在终点之前）
        /// </summary>
        /// <param name="line">线段</param>
        /// <returns>标准化后的线段</returns>
        Line2D NormalizeLine(Line2D line);

        /// <summary>
        /// 标准化点（四舍五入到指定精度）
        /// </summary>
        /// <param name="point">点</param>
        /// <param name="precision">精度（小数位数）</param>
        /// <returns>标准化后的点</returns>
        Point2D NormalizePoint(Point2D point, int precision = 6);

        // === 几何分割 ===

        /// <summary>
        /// 将线段分割为多个等长段
        /// </summary>
        /// <param name="line">线段</param>
        /// <param name="segments">分段数</param>
        /// <returns>分割后的线段列表</returns>
        List<Line2D> DivideLine(Line2D line, int segments);

        /// <summary>
        /// 在线段上按指定间距分割点
        /// </summary>
        /// <param name="line">线段</param>
        /// <param name="interval">间距</param>
        /// <returns>分割点列表</returns>
        List<Point2D> DivideLineByDistance(Line2D line, double interval);

        /// <summary>
        /// 将多边形按网格分割
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <param name="gridSizeX">X方向网格大小</param>
        /// <param name="gridSizeY">Y方向网格大小</param>
        /// <returns>网格交点列表</returns>
        List<Point2D> DividePolygonByGrid(Polygon2D polygon, double gridSizeX, double gridSizeY);

        // === 几何合并 ===

        /// <summary>
        /// 合并共线的线段
        /// </summary>
        /// <param name="lines">线段列表</param>
        /// <param name="tolerance">容差</param>
        /// <returns>合并后的线段列表</returns>
        List<Line2D> MergeCollinearLines(List<Line2D> lines, Tolerance tolerance);

        /// <summary>
        /// 合并相邻的多边形
        /// </summary>
        /// <param name="polygons">多边形列表</param>
        /// <param name="tolerance">容差</param>
        /// <returns>合并后的多边形列表</returns>
        List<Polygon2D> MergeAdjacentPolygons(List<Polygon2D> polygons, Tolerance tolerance);

        // === 坐标系转换 ===

        /// <summary>
        /// 坐标系变换（平移、旋转、缩放）
        /// </summary>
        /// <param name="point">点</param>
        /// <param name="translation">平移向量</param>
        /// <param name="rotation">旋转角度（弧度）</param>
        /// <param name="scale">缩放比例</param>
        /// <param name="origin">变换原点</param>
        /// <returns>变换后的点</returns>
        Point2D TransformPoint(Point2D point, Point2D translation, double rotation, double scale, Point2D origin);

        /// <summary>
        /// 批量坐标系变换
        /// </summary>
        /// <param name="points">点列表</param>
        /// <param name="translation">平移向量</param>
        /// <param name="rotation">旋转角度（弧度）</param>
        /// <param name="scale">缩放比例</param>
        /// <param name="origin">变换原点</param>
        /// <returns>变换后的点列表</returns>
        List<Point2D> TransformPoints(List<Point2D> points, Point2D translation, double rotation, double scale, Point2D origin);

        /// <summary>
        /// 线段坐标系变换
        /// </summary>
        /// <param name="line">线段</param>
        /// <param name="translation">平移向量</param>
        /// <param name="rotation">旋转角度（弧度）</param>
        /// <param name="scale">缩放比例</param>
        /// <param name="origin">变换原点</param>
        /// <returns>变换后的线段</returns>
        Line2D TransformLine(Line2D line, Point2D translation, double rotation, double scale, Point2D origin);

        /// <summary>
        /// 多边形坐标系变换
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <param name="translation">平移向量</param>
        /// <param name="rotation">旋转角度（弧度）</param>
        /// <param name="scale">缩放比例</param>
        /// <param name="origin">变换原点</param>
        /// <returns>变换后的多边形</returns>
        Polygon2D TransformPolygon(Polygon2D polygon, Point2D translation, double rotation, double scale, Point2D origin);

        // === 几何验证与修复 ===

        /// <summary>
        /// 验证多边形的有效性
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <param name="tolerance">容差</param>
        /// <returns>验证结果</returns>
        GeometryValidationResult ValidatePolygon(Polygon2D polygon, Tolerance tolerance);

        /// <summary>
        /// 修复无效的多边形
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <param name="tolerance">容差</param>
        /// <returns>修复后的多边形</returns>
        Polygon2D RepairPolygon(Polygon2D polygon, Tolerance tolerance);

        /// <summary>
        /// 验证线段的有效性
        /// </summary>
        /// <param name="line">线段</param>
        /// <param name="tolerance">容差</param>
        /// <returns>验证结果</returns>
        GeometryValidationResult ValidateLine(Line2D line, Tolerance tolerance);

        // === 几何属性提取 ===

        /// <summary>
        /// 提取多边形的特征点（角点、关键点）
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <param name="tolerance">容差</param>
        /// <returns>特征点列表</returns>
        List<Point2D> ExtractFeaturePoints(Polygon2D polygon, Tolerance tolerance);

        /// <summary>
        /// 提取几何对象的统计信息
        /// </summary>
        /// <param name="polygon">多边形</param>
        /// <returns>几何统计信息</returns>
        GeometryStatistics CalculateStatistics(Polygon2D polygon);
    }

    /// <summary>
    /// 几何验证结果
    /// </summary>
    public class GeometryValidationResult
    {
        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 错误消息列表
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// 警告消息列表
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// 几何统计信息
    /// </summary>
    public class GeometryStatistics
    {
        /// <summary>
        /// 顶点数量
        /// </summary>
        public int VertexCount { get; set; }

        /// <summary>
        /// 边数量
        /// </summary>
        public int EdgeCount { get; set; }

        /// <summary>
        /// 面积
        /// </summary>
        public double Area { get; set; }

        /// <summary>
        /// 周长
        /// </summary>
        public double Perimeter { get; set; }

        /// <summary>
        /// 质心
        /// </summary>
        public Point2D Centroid { get; set; }

        /// <summary>
        /// 边界框
        /// </summary>
        public (Point2D MinPoint, Point2D MaxPoint) BoundingBox { get; set; }

        /// <summary>
        /// 是否为凸多边形
        /// </summary>
        public bool IsConvex { get; set; }

        /// <summary>
        /// 是否为简单多边形（无自相交）
        /// </summary>
        public bool IsSimple { get; set; }

        /// <summary>
        /// 是否为顺时针方向
        /// </summary>
        public bool IsClockwise { get; set; }
    }
}

