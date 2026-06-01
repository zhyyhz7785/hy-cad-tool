using System.Collections.Generic;
using HyCAD.Geometry;

namespace HyCADTool.Features.AcadDimension.Domain.Context
{
    /// <summary>
    /// 跑一次 NewDDS 链路所需的全部可变状态（per-run scratchpad）。
    /// 修 06 §7 #10：Service 必须 stateless，所有 per-run 状态进此对象，避免 _alongLines / _verticalLines 全局可变字段重用导致前一次结果污染下一次。
    /// </summary>
    public sealed class NewDdsContext
    {
        /// <summary>原始边界（已 tessellate Bulge 段为多顶点）。Phase 2 由 Service 入口填充。</summary>
        public Polyline2D Boundary { get; set; }

        /// <summary>边界包围盒。</summary>
        public BoundingBox Bounds { get; set; }

        /// <summary>本次实际使用的扫描步长（Adaptive 计算后或 Fixed 取值）。</summary>
        public double EffectiveStep { get; set; }

        /// <summary>本次链路的诊断信息（safetyCounter 命中 / Bulge 采样统计 / 退化几何告警等）。</summary>
        public IList<string> Diagnostics { get; } = new List<string>();
    }
}
