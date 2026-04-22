using System;

namespace HyCADTool.Refactored.Domain.Models.Road.Civil
{
    /// <summary>
    /// 桥梁（045 / M2 占位）。
    ///
    /// 用于路线「构造物与附属」节点下的子分组；v2.0 仅持久化最小字段，
    /// v3+ 扩展结构形式、墩台、跨径组合等。
    /// </summary>
    public sealed class Bridge
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; }

        /// <summary>所属 Alignment；null 表示未归属到任何路线（比如独立跨越式桥梁）。</summary>
        public Guid? OwnerAlignmentId { get; set; }

        /// <summary>起始桩号（m）。</summary>
        public double StartStation { get; set; }

        /// <summary>结束桩号（m）。</summary>
        public double EndStation { get; set; }

        /// <summary>桥梁类型（简梁/连续梁/斜拉等，v2.0 仅字符串占位）。</summary>
        public string BridgeType { get; set; }

        public override string ToString()
            => $"Bridge[{Name}, Id={Id:N}, K{StartStation:F0}~K{EndStation:F0}]";
    }
}
