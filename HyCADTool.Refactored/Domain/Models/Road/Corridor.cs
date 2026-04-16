using System;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Domain.Models.Road
{
    /// <summary>
    /// 走廊（Corridor）：Alignment + Profile + Template 组合，按桩号驱动生成道路 3D 几何。
    ///
    /// v1 只保存"引用 + 区段配置"；v2 通过 <c>ICorridorMeshBuilder</c> 生成 <see cref="ValueObjects.Geometry.Mesh3D"/>。
    /// v1 不计算网格，仅作为 JSON 持久化对象。
    /// </summary>
    public sealed class Corridor
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; }

        /// <summary>引用的平面线位 ID。</summary>
        public Guid AlignmentId { get; set; }

        /// <summary>引用的设计纵断面 ID（为空则取 <see cref="Alignment.Profiles"/> 的首个设计线）。</summary>
        public Guid DesignProfileId { get; set; }

        /// <summary>
        /// 走廊分段（不同桩号区间可使用不同模板）。
        /// 未分段时默认使用首个 <see cref="CorridorSegment"/>。
        /// </summary>
        public List<CorridorSegment> Segments { get; } = new List<CorridorSegment>();

        public override string ToString() => $"Corridor[{Name}, Id={Id:N}, Segments={Segments.Count}]";
    }

    /// <summary>
    /// 走廊区段：在 [StartStation, EndStation] 内使用指定模板。
    /// </summary>
    public sealed class CorridorSegment
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>起始桩号（m）。</summary>
        public double StartStation { get; set; }

        /// <summary>结束桩号（m）。</summary>
        public double EndStation { get; set; }

        /// <summary>使用的模板 ID。</summary>
        public Guid TemplateId { get; set; }

        /// <summary>采样步距（m），v2 生成网格时按此离散。默认 5m。</summary>
        public double SamplingStep { get; set; } = 5.0;
    }
}
