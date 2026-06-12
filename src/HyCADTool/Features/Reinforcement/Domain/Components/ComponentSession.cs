using HyCADTool.Features.Reinforcement.Domain.Components;
using System;
using System.Collections.Generic;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>
    /// 当前文档构件识别会话（内存缓存，供预览与配筋命令共享）。
    /// </summary>
    public static class ComponentSession
    {
        public static Guid CurrentSessionId { get; private set; } = Guid.Empty;
        public static List<ComponentRegion> Regions { get; private set; } = new List<ComponentRegion>();
        public static List<ReinRegion> ReinRegions { get; private set; } = new List<ReinRegion>();

        public static void Set(Guid sessionId, IReadOnlyList<ReinRegion> reinRegions, IReadOnlyList<ComponentRegion> regions)
        {
            CurrentSessionId = sessionId;
            ReinRegions = reinRegions != null ? new List<ReinRegion>(reinRegions) : new List<ReinRegion>();
            Regions = regions != null ? new List<ComponentRegion>(regions) : new List<ComponentRegion>();
        }

        public static void UpdateRegionType(Guid regionId, ComponentType type)
        {
            var region = Regions.Find(r => r.Id == regionId);
            if (region != null)
                region.Type = type;
        }

        public static void Clear()
        {
            CurrentSessionId = Guid.Empty;
            Regions = new List<ComponentRegion>();
            ReinRegions = new List<ReinRegion>();
        }
    }
}
