using Autodesk.AutoCAD.DatabaseServices;
using HyCAD.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Shared.AutoCAD.Interfaces
{
    /// <summary>
    /// 曲线线段提取器接口（Curve Segment Extractor Interface）
    /// 负责从 AutoCAD 曲线提取 Domain 层的 Line2D 线段
    /// 这是平台特定实现，将 AutoCAD API 转换为平台无关的Domain对象
    /// </summary>
    public interface ICurveSegmentExtractor
    {
        /// <summary>
        /// 提取并简化所有曲线为线段，同时建立映射字典
        /// </summary>
        /// <param name="curveIds">曲线对象 ID 列表</param>
        /// <param name="simplificationService">曲线简化服务</param>
        /// <param name="tolerance">容差</param>
        /// <param name="arcSegmentCount">Arc简化分段数（null=自动）</param>
        /// <param name="ellipseSegmentCount">Ellipse简化分段数（null=自动）</param>
        /// <param name="splineSegmentCount">Spline简化分段数（null=自动）</param>
        (List<Line2D> segments, List<SimplifiedCurveMapping> mappings) ExtractAndSimplify(
            IEnumerable<ObjectId> curveIds,
            HyCAD.Geometry.Algorithms.CurveSimplificationService simplificationService,
            double tolerance,
            int? arcSegmentCount = null,
            int? ellipseSegmentCount = null,
            int? splineSegmentCount = null);
    }
}
