using HyCADTool.Shared.AutoCAD.Session;
using System;
using System.Collections.Generic;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>
    /// 构件识别会话（内存缓存，供预览与配筋命令共享）。
    /// 按文档对象分槽：插件注入 <see cref="SetDocumentKeyResolver"/> 后，切换活动图不会读到另一张图的区域。
    /// 键应是 Database（另存为后对象不变）；未注入时共用进程级默认槽。
    /// </summary>
    public static class ComponentSession
    {
        private static readonly object Gate = new object();
        private static readonly object FallbackKey = new object();
        private static Func<object> _documentKey;

        /// <summary>
        /// 由插件启动时注入。返回当前活动图的 Database（或测试用对象）；null 落在默认槽。
        /// </summary>
        public static void SetDocumentKeyResolver(Func<object> resolver)
        {
            lock (Gate)
                _documentKey = resolver;
        }

        /// <summary>文档关闭时丢掉该图的槽，避免句柄复用后读到旧区域。</summary>
        public static void ForgetDocument(object documentKey)
        {
            DocumentSessionStore.Forget(documentKey);
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

        private static ComponentSessionState GetSlot()
        {
            return DocumentSessionStore.Get<ComponentSessionState>(ResolveKey());
        }

        private static object ResolveKey()
        {
            var resolver = _documentKey;
            if (resolver == null)
                return FallbackKey;
            try
            {
                return resolver() ?? FallbackKey;
            }
            catch
            {
                return FallbackKey;
            }
        }
    }

    /// <summary>单张图上的构件会话槽，由 <see cref="DocumentSessionStore"/> 按文档键持有。</summary>
    public sealed class ComponentSessionState
    {
        public Guid CurrentSessionId;
        public List<ComponentRegion> Regions = new List<ComponentRegion>();
        public List<ReinRegion> ReinRegions = new List<ReinRegion>();
    }
}
