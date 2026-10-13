using HyCADTool.Features.Reinforcement.Domain.Components;
using System;
using System.Collections.Generic;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>
    /// 构件识别会话（内存缓存，供预览与配筋命令共享）。
    /// 按文档键分槽：插件注入 <see cref="SetDocumentKeyResolver"/> 后，切换活动图不会读到另一张图的区域。
    /// 未注入时所有调用共用空键，保持单文档进程里的原行为。
    /// </summary>
    public static class ComponentSession
    {
        private static readonly object Gate = new object();
        private static Func<string> _documentKey;
        private static readonly Dictionary<string, Slot> Slots =
            new Dictionary<string, Slot>(StringComparer.Ordinal);

        private sealed class Slot
        {
            public Guid CurrentSessionId;
            public List<ComponentRegion> Regions = new List<ComponentRegion>();
            public List<ReinRegion> ReinRegions = new List<ReinRegion>();
        }

        /// <summary>
        /// 由插件启动时注入。返回值是当前活动图的稳定键；null 与空字符串都落在同一默认槽。
        /// </summary>
        public static void SetDocumentKeyResolver(Func<string> resolver)
        {
            lock (Gate)
                _documentKey = resolver;
        }

        /// <summary>文档关闭时丢掉该图的槽，避免句柄复用后读到旧区域。</summary>
        public static void ForgetDocument(string documentKey)
        {
            if (string.IsNullOrEmpty(documentKey))
                return;
            lock (Gate)
                Slots.Remove(documentKey);
        }

        public static Guid CurrentSessionId
        {
            get
            {
                lock (Gate)
                    return GetSlot().CurrentSessionId;
            }
        }

        public static List<ComponentRegion> Regions
        {
            get
            {
                lock (Gate)
                    return GetSlot().Regions;
            }
        }

        public static List<ReinRegion> ReinRegions
        {
            get
            {
                lock (Gate)
                    return GetSlot().ReinRegions;
            }
        }

        public static void Set(Guid sessionId, IReadOnlyList<ReinRegion> reinRegions, IReadOnlyList<ComponentRegion> regions)
        {
            lock (Gate)
            {
                var slot = GetSlot();
                slot.CurrentSessionId = sessionId;
                slot.ReinRegions = reinRegions != null ? new List<ReinRegion>(reinRegions) : new List<ReinRegion>();
                slot.Regions = regions != null ? new List<ComponentRegion>(regions) : new List<ComponentRegion>();
            }
        }

        public static void UpdateRegionType(Guid regionId, ComponentType type)
        {
            lock (Gate)
            {
                var region = GetSlot().Regions.Find(r => r.Id == regionId);
                if (region != null)
                    region.Type = type;
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                var slot = GetSlot();
                slot.CurrentSessionId = Guid.Empty;
                slot.Regions = new List<ComponentRegion>();
                slot.ReinRegions = new List<ReinRegion>();
            }
        }

        private static Slot GetSlot()
        {
            string key = ResolveKey();
            if (!Slots.TryGetValue(key, out var slot))
            {
                slot = new Slot();
                Slots[key] = slot;
            }
            return slot;
        }

        private static string ResolveKey()
        {
            var resolver = _documentKey;
            if (resolver == null)
                return "";
            try
            {
                return resolver() ?? "";
            }
            catch
            {
                return "";
            }
        }
    }
}
