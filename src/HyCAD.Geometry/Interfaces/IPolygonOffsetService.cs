using HyCAD.Geometry;

namespace HyCAD.Geometry.Interfaces
{
    /// <summary>
    /// 多边形/多段线偏移服务接口（平台无关）
    /// AutoCAD 实现使用 Polyline.GetOffsetCurves
    /// Blender 实现可使用 Clipper2 等纯算法
    /// </summary>
    public interface IPolygonOffsetService
    {
        /// <summary>
        /// 对闭合多段线进行偏移
        /// 正值向外偏移，负值向内偏移
        /// </summary>
        /// <param name="polyline">输入的闭合多段线</param>
        /// <param name="offsetDistance">偏移距离（正=外扩, 负=内缩）</param>
        /// <returns>偏移后的多段线；失败时返回 null</returns>
        Polyline2D Offset(Polyline2D polyline, double offsetDistance);
    }
}
