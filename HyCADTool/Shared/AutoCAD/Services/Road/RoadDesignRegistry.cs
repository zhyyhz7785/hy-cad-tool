using System;
using System.Collections.Concurrent;
using HyCADTool.Domain.Events.Road;
using HyCADTool.Domain.Models.Road;

namespace HyCADTool.Shared.AutoCAD.Services.Road
{
    /// <summary>
    /// 活动 RoadDesign 注册表：按 AutoCAD 文档名缓存内存中的 Domain 聚合根。
    ///
    /// 职责（决策 1 - 单文件 JSON / 决策 3 - 真实事件总线）：
    /// - 按 AutoCAD MdiActiveDocument.Name 索引 <see cref="RoadDesign"/> 实例；
    /// - 切换文档 / 打开 DWG 时懒加载；关闭文档时清理；
    /// - 作为命令层 Save 前的读取入口（<c>RoadJsonExportService.SaveForDocument</c> 调用方先 TryGet 再写盘）。
    ///
    /// 线程模型：AutoCAD 命令线程顺序访问，文档事件（DocumentActivated / DocumentCreated）亦在主线程；
    /// 仍采用 <see cref="ConcurrentDictionary{TKey, TValue}"/> 防止未来并发扩展。
    /// </summary>
    public sealed class RoadDesignRegistry
    {
        private readonly ConcurrentDictionary<string, RoadDesign> _designs
            = new ConcurrentDictionary<string, RoadDesign>(StringComparer.OrdinalIgnoreCase);

        private readonly IRoadEventBus _eventBus;

        public RoadDesignRegistry(IRoadEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        /// <summary>
        /// 获取或创建指定文档的 RoadDesign。
        /// </summary>
        public RoadDesign GetOrCreate(string documentName)
        {
            if (string.IsNullOrEmpty(documentName)) documentName = "default";
            return _designs.GetOrAdd(documentName, name => new RoadDesign
            {
                ProjectName = System.IO.Path.GetFileNameWithoutExtension(name) ?? name
            });
        }

        /// <summary>
        /// 查询但不创建。
        /// </summary>
        public bool TryGet(string documentName, out RoadDesign roadDesign)
        {
            return _designs.TryGetValue(documentName ?? string.Empty, out roadDesign);
        }

        /// <summary>
        /// 替换指定文档的 RoadDesign（通常来自 JSON 加载），并发布 <see cref="RoadDesignReloadedEvent"/>。
        /// </summary>
        public void Replace(string documentName, RoadDesign newDesign)
        {
            if (string.IsNullOrEmpty(documentName)) documentName = "default";
            if (newDesign == null) throw new ArgumentNullException(nameof(newDesign));

            _designs[documentName] = newDesign;
            _eventBus.Publish(new RoadDesignReloadedEvent(newDesign.Id));
        }

        /// <summary>
        /// 移除指定文档的 RoadDesign（文档关闭时调用）。
        /// </summary>
        public void Remove(string documentName)
        {
            _designs.TryRemove(documentName ?? string.Empty, out _);
        }

        /// <summary>
        /// 把 <paramref name="oldKey"/> 下的 <see cref="RoadDesign"/> 迁移到 <paramref name="newKey"/>。
        ///
        /// 场景：
        /// - DWG 另存为（SAVEAS）后 <c>MdiActiveDocument.Name</c> 改变，
        ///   老 key 下的 design 成为孤儿（自动保存会写到老路径，命令会 <c>GetOrCreate</c> 出一个空壳）；
        /// - 打开"同一份道路 JSON 属于的另一份 DWG 副本"时，用户可能希望把内存里的 design 绑到新 DWG。
        ///
        /// 语义：
        /// - <paramref name="oldKey"/> 不存在：返回 false，不改动；
        /// - <paramref name="oldKey"/> 与 <paramref name="newKey"/> 相等（忽略大小写）：返回 false；
        /// - <paramref name="newKey"/> 已有 design：为防止静默覆盖历史数据，返回 false；调用方需先决定策略（通常是 <see cref="Replace"/>）；
        /// - 否则：迁移并移除老 key，返回 true。不触发事件 —— 迁移是基础设施级操作，对 UI 不可见。
        /// </summary>
        public bool Rekey(string oldKey, string newKey)
        {
            if (string.IsNullOrEmpty(oldKey) || string.IsNullOrEmpty(newKey)) return false;
            if (string.Equals(oldKey, newKey, StringComparison.OrdinalIgnoreCase)) return false;

            if (!_designs.TryGetValue(oldKey, out var design)) return false;
            if (_designs.ContainsKey(newKey)) return false;

            if (!_designs.TryAdd(newKey, design)) return false;
            _designs.TryRemove(oldKey, out _);
            return true;
        }

        /// <summary>
        /// 快照所有文档的 RoadDesign（诊断 / 调试用）。
        /// </summary>
        public System.Collections.Generic.IReadOnlyDictionary<string, RoadDesign> Snapshot()
        {
            return new System.Collections.Generic.Dictionary<string, RoadDesign>(_designs);
        }
    }
}
