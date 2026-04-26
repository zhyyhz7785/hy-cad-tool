using HyCADTool.Shared.Geometry;
using System.Linq;

namespace HyCADTool.Shared.Geometry.Offset
{
    /// <summary>
    /// 多边形偏移服务（公共接口）
    /// 算法逻辑完全源自 Clipper2 库
    /// Original: Clipper2/CPP/Clipper2Lib/src/Clipper.Offset.cpp
    /// License: Boost Software License 1.0
    /// Author: Angus Johnson (Clipper2)
    /// Adapted by: HyCADTool Team
    /// Date: 2025-10-27
    /// 
    /// 说明：
    /// 本服务将 Clipper2 的核心偏移算法逻辑移植到 Domain 层，
    /// 避免对外部库的依赖，保持架构纯净性和平台无关性。
    /// </summary>
    public static class PolygonOffsetService
    {
        /// <summary>
        /// 多边形偏移（对外主接口）
        /// </summary>
        /// <param name="polygon">原始多边形</param>
        /// <param name="distance">偏移距离（mm）</param>
        /// <param name="isOutward">true=外扩，false=内缩</param>
        /// <param name="joinType">转角连接类型（默认 Round 圆角）</param>
        /// <returns>偏移后的多边形</returns>
        public static Polygon2D Offset(
            Polygon2D polygon, 
            double distance, 
            bool isOutward, 
            OffsetJoinType joinType = OffsetJoinType.Round)
        {
            var vertices = polygon.Vertices.ToList();
            
            var offsetVertices = OffsetBuilder.BuildOffset(
                vertices, distance, isOutward, joinType);
            
            return new Polygon2D(offsetVertices, polygon.IsClosed);
        }
    }
}












