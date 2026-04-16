using System;

namespace HyCADTool.Refactored.Domain.Events.Road
{
    /// <summary>
    /// 道路对象操作类型。
    /// </summary>
    public enum RoadChangeKind
    {
        Created = 0,
        Updated = 1,
        Deleted = 2
    }

    /// <summary>
    /// 事件基类：封装公共时间戳 / RoadDesignId / 操作类型。
    /// </summary>
    public abstract class RoadChangeEventBase : IRoadChangeEvent
    {
        public DateTime TimestampUtc { get; }
        public Guid RoadDesignId { get; }
        public RoadChangeKind Kind { get; }

        protected RoadChangeEventBase(Guid roadDesignId, RoadChangeKind kind)
        {
            TimestampUtc = DateTime.UtcNow;
            RoadDesignId = roadDesignId;
            Kind = kind;
        }

        public override string ToString()
        {
            return $"{GetType().Name}[{Kind}, RoadId={RoadDesignId:N}, Ts={TimestampUtc:HH:mm:ss.fff}]";
        }
    }

    // ===== 1~3 平面线位 =====

    /// <summary>平面线位变更事件（Created / Updated / Deleted 三合一）。</summary>
    public sealed class AlignmentChangedEvent : RoadChangeEventBase
    {
        public Guid AlignmentId { get; }

        public AlignmentChangedEvent(Guid roadDesignId, Guid alignmentId, RoadChangeKind kind)
            : base(roadDesignId, kind)
        {
            AlignmentId = alignmentId;
        }
    }

    // ===== 4~6 纵断面 =====

    /// <summary>纵断面变更事件。</summary>
    public sealed class ProfileChangedEvent : RoadChangeEventBase
    {
        public Guid AlignmentId { get; }
        public Guid ProfileId { get; }

        public ProfileChangedEvent(Guid roadDesignId, Guid alignmentId, Guid profileId, RoadChangeKind kind)
            : base(roadDesignId, kind)
        {
            AlignmentId = alignmentId;
            ProfileId = profileId;
        }
    }

    // ===== 7~9 横断面模板 =====

    /// <summary>横断面模板变更事件。</summary>
    public sealed class TemplateChangedEvent : RoadChangeEventBase
    {
        public Guid TemplateId { get; }

        public TemplateChangedEvent(Guid roadDesignId, Guid templateId, RoadChangeKind kind)
            : base(roadDesignId, kind)
        {
            TemplateId = templateId;
        }
    }

    // ===== 10~12 走廊 =====

    /// <summary>走廊变更事件。</summary>
    public sealed class CorridorChangedEvent : RoadChangeEventBase
    {
        public Guid CorridorId { get; }

        public CorridorChangedEvent(Guid roadDesignId, Guid corridorId, RoadChangeKind kind)
            : base(roadDesignId, kind)
        {
            CorridorId = corridorId;
        }
    }

    /// <summary>
    /// 聚合根变更事件：整个 <c>RoadDesign</c> 被重置 / 从 JSON 重载。
    /// 持久化层收到后应立即（跳过防抖）写入 JSON；
    /// Blender 插件收到后应整体刷新场景。
    /// </summary>
    public sealed class RoadDesignReloadedEvent : RoadChangeEventBase
    {
        public RoadDesignReloadedEvent(Guid roadDesignId)
            : base(roadDesignId, RoadChangeKind.Updated)
        {
        }
    }
}
