using HyCADTool.Shared.Geometry;

namespace HyCADTool.Domain.Interfaces
{
    /// <summary>
    /// 线段与多段线交点计算服务接口（平台无关）
    /// AutoCAD 实现使用 Line.IntersectWith + Intersect.ExtendThis
    /// 纯算法实现可遍历多段线各段求交
    /// </summary>
    public interface ILineIntersectionService
    {
        /// <summary>
        /// 获取从线段端点沿延伸方向到多段线的最近正向交点
        /// 对应旧代码 GetIntersectionByLinetWithBoundary
        /// </summary>
        /// <param name="segmentEndPoint">线段端点（射线起点）</param>
        /// <param name="direction">射线方向</param>
        /// <param name="boundary">目标多段线（边界）</param>
        /// <returns>最近的正向交点</returns>
        Point2D GetNearestForwardIntersection(Point2D segmentEndPoint, Vector2D direction, Polyline2D boundary);
    }
}
