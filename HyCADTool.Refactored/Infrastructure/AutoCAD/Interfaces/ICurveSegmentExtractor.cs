using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces
{
    /// <summary>
    /// 曲线线段提取器接口（Curve Segment Extractor Interface）
    /// 负责从 AutoCAD 曲线提取 Domain 层的 Line2D 线段
    /// 这是平台特定实现，将 AutoCAD API 转换为平台无关的Domain对象
    /// </summary>
    public interface ICurveSegmentExtractor
    {
        /// <summary>
        /// 从 Curve 对象提取线段
        /// 对于直线，直接返回一条线段
        /// 对于多段线，提取所有顶点之间的线段
        /// 对于曲线（如圆弧、圆、样条），未来会进行离散化
        /// </summary>
        /// <param name="curve">AutoCAD 曲线对象</param>
        /// <param name="tolerance">离散化容差（当前未使用，为未来扩展预留）</param>
        /// <returns>Line2D 线段数组</returns>
        Line2D[] ExtractFromCurve(Curve curve, double tolerance);

        /// <summary>
        /// 从 ObjectId 集合提取所有线段
        /// </summary>
        /// <param name="curveIds">曲线对象 ID 列表</param>
        /// <param name="tolerance">离散化容差</param>
        /// <returns>Line2D 线段列表</returns>
        List<Line2D> ExtractSegments(IEnumerable<ObjectId> curveIds, double tolerance);

        /// <summary>
        /// 从 ObjectId 集合提取所有曲线段（包含完整曲线信息）
        /// 支持 Polyline 多顶点、Arc、Spline、Ellipse
        /// </summary>
        /// <param name="curveIds">曲线对象 ID 列表</param>
        /// <param name="tolerance">离散化容差</param>
        /// <returns>曲线段列表（包含原始曲线信息）</returns>
        List<CurveSegment2D> ExtractSegmentsWithCurveInfo(IEnumerable<ObjectId> curveIds, double tolerance);
        
        /// <summary>
        /// 提取并简化所有曲线为线段，同时建立映射字典
        /// </summary>
        (List<Line2D> segments, List<SimplifiedCurveMapping> mappings) ExtractAndSimplify(
            IEnumerable<ObjectId> curveIds,
            Domain.Services.CurveSimplificationService simplificationService,
            double tolerance,
            int? arcSegmentCount = null,
            int? ellipseSegmentCount = null,
            int? splineSegmentCount = null);
    }
}







