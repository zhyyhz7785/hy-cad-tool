using System;

namespace HyCADTool.Refactored.Domain.Models.Road.Civil
{
    /// <summary>
    /// 路外交通设施（045 / M2 占位）。
    ///
    /// 对应用户草案图中「其他 → 交通设施」——路外标志牌、信号灯、电子警察、监控。
    /// 与路内的 <c>LaneMarking / StopLine / Crosswalk</c> 分开（后者挂在 Alignment 下）。
    /// v2.0 仅最小字段。
    /// </summary>
    public sealed class TrafficFacility
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; }

        /// <summary>交通设施类型。</summary>
        public TrafficFacilityKind Kind { get; set; } = TrafficFacilityKind.Custom;

        /// <summary>就近 Alignment；null 表示独立于任何路线。</summary>
        public Guid? OwnerAlignmentId { get; set; }

        public override string ToString()
            => $"TrafficFacility[{Name}, Id={Id:N}, Kind={Kind}]";
    }

    /// <summary>路外交通设施种类。</summary>
    public enum TrafficFacilityKind
    {
        Custom = 0,
        /// <summary>标志牌。</summary>
        Sign = 1,
        /// <summary>信号灯。</summary>
        Signal = 2,
        /// <summary>电子警察 / 监控。</summary>
        Camera = 3
    }
}
