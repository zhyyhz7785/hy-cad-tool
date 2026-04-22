using System;

namespace HyCADTool.Refactored.Domain.Models.Road.Civil
{
    /// <summary>
    /// 隧道（045 / M2 占位）。v2.0 仅最小字段；v3+ 扩展断面型式、衬砌类型、通风方式。
    /// </summary>
    public sealed class Tunnel
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; }

        /// <summary>所属 Alignment；null 表示未归属到任何路线。</summary>
        public Guid? OwnerAlignmentId { get; set; }

        /// <summary>起始桩号（m）。</summary>
        public double StartStation { get; set; }

        /// <summary>结束桩号（m）。</summary>
        public double EndStation { get; set; }

        /// <summary>隧道类型（明挖/暗挖/盾构 等）。</summary>
        public string TunnelType { get; set; }

        public override string ToString()
            => $"Tunnel[{Name}, Id={Id:N}, K{StartStation:F0}~K{EndStation:F0}]";
    }
}
