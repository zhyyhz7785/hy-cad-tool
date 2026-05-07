using System.Collections.Generic;
using System.Linq;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Models
{
    /// <summary>Diff 条目类型（Added / Deleted / Modified 三态）。</summary>
    public enum HyobDiffKind : byte
    {
        Added    = 1,
        Deleted  = 2,
        Modified = 3,
    }

    /// <summary>对 Modified 条目内字段级变更（schema 字段名 + 旧值 / 新值字符串）。</summary>
    public sealed class HyobFieldChange
    {
        public string Field { get; }
        public string OldValue { get; }
        public string NewValue { get; }

        public HyobFieldChange(string field, string oldValue, string newValue)
        {
            Field = field ?? string.Empty;
            OldValue = oldValue ?? string.Empty;
            NewValue = newValue ?? string.Empty;
        }
    }

    /// <summary>
    /// hyob diff 单条变更。设计：04 §11（hyobD a b：commit→commit 的字段级变更追踪）。
    /// v1：hash 级（object 替换 / 新增 / 删除）。
    /// v2 (M11-B)：Modified 条目 <see cref="Changes"/> 携带字段级差异（schema 已知 typed）。
    /// </summary>
    public sealed class HyobDiffEntry
    {
        /// <summary>仓库路径（如 "entities/by-handle/AB12.x" / "tables/layers/钢筋"）。</summary>
        public string Path { get; }
        public HyobDiffKind Kind { get; }
        public Hash OldHash { get; }
        public Hash NewHash { get; }
        public HyobObjectKind OldType { get; }
        public HyobObjectKind NewType { get; }
        /// <summary>仅 Modified 条目可能非空：字段级差异。</summary>
        public IReadOnlyList<HyobFieldChange> Changes { get; }

        public HyobDiffEntry(
            string path, HyobDiffKind kind,
            Hash oldHash, Hash newHash,
            HyobObjectKind oldType, HyobObjectKind newType,
            IReadOnlyList<HyobFieldChange> changes = null)
        {
            Path = path ?? string.Empty;
            Kind = kind;
            OldHash = oldHash;
            NewHash = newHash;
            OldType = oldType;
            NewType = newType;
            Changes = changes;
        }

        public HyobObjectKind RepresentativeType =>
            Kind == HyobDiffKind.Deleted ? OldType : NewType;
    }

    /// <summary>
    /// hyob diff 报告。Entries 按 Path ordinal 升序，便于稳定输出。
    /// </summary>
    public sealed class HyobDiffReport
    {
        public Hash FromCommit { get; }
        public Hash ToCommit { get; }
        public List<HyobDiffEntry> Entries { get; } = new List<HyobDiffEntry>();

        public HyobDiffReport(Hash fromCommit, Hash toCommit)
        {
            FromCommit = fromCommit;
            ToCommit = toCommit;
        }

        public int TotalAdded    => Entries.Count(e => e.Kind == HyobDiffKind.Added);
        public int TotalDeleted  => Entries.Count(e => e.Kind == HyobDiffKind.Deleted);
        public int TotalModified => Entries.Count(e => e.Kind == HyobDiffKind.Modified);
        public bool IsEmpty      => Entries.Count == 0;

        /// <summary>按 type_id 聚合 (Add, Modify, Delete) 三联计数。</summary>
        public Dictionary<HyobObjectKind, (int Add, int Mod, int Del)> AggregateByKind()
        {
            var dict = new Dictionary<HyobObjectKind, (int, int, int)>();
            foreach (var e in Entries)
            {
                var k = e.RepresentativeType;
                dict.TryGetValue(k, out var t);
                switch (e.Kind)
                {
                    case HyobDiffKind.Added:    t.Item1++; break;
                    case HyobDiffKind.Modified: t.Item2++; break;
                    case HyobDiffKind.Deleted:  t.Item3++; break;
                }
                dict[k] = t;
            }
            return dict;
        }
    }
}
