using System.Collections.Generic;
using HyCAD.Geometry;

namespace HyCADTool.Features.AcadDimension.Domain.Boundary
{
    /// <summary>
    /// 拓扑特征 DTO。
    /// 由 IBoundaryFeatureExtractor 从 Polyline2D 提取；不直接产生 Dimension。
    /// Domain 层纯净——不引用 AutoCAD API（07 §11 第 1 条）。
    /// </summary>
    public sealed class BoundaryFeatures
    {
        /// <summary>原始边界包围盒。</summary>
        public BoundingBox Bounds { get; set; }

        /// <summary>水平割线扫描得到的特征段（按列分组；每列内是 y=常数 的一组水平段）。Phase 2 填充。</summary>
        public IReadOnlyList<IReadOnlyList<Line2D>> HorizontalSecantColumns { get; set; }
            = new List<IReadOnlyList<Line2D>>();

        /// <summary>垂直割线扫描得到的特征段（按列分组；每列内是 x=常数 的一组垂直段）。Phase 2 填充。</summary>
        public IReadOnlyList<IReadOnlyList<Line2D>> VerticalSecantColumns { get; set; }
            = new List<IReadOnlyList<Line2D>>();

        /// <summary>输入多段线顶点（已 tessellate Bulge）。供 Q 函数与诊断使用。</summary>
        public IReadOnlyList<Point2D> Vertices { get; set; } = new List<Point2D>();

        /// <summary>诊断信息（safetyCounter 命中 / 采样过密 / 特征丢失等）。</summary>
        public IList<string> Diagnostics { get; } = new List<string>();
    }
}
