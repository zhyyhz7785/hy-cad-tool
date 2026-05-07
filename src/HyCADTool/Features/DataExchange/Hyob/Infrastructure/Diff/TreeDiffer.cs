using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Models;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Infrastructure.Diff
{
    /// <summary>
    /// hyob 仓库的 Diff 引擎。沿 (commit-A.tree, commit-B.tree) 递归对比，
    /// 输出 (Path, Kind, OldHash, NewHash, Type) 列表。
    /// 设计：04 §11；当前 v1 是 hash 级 diff（"object 整体替换" 粒度）。
    /// </summary>
    public sealed class TreeDiffer
    {
        private readonly HyobObjectStore _objects;

        public TreeDiffer(HyobObjectStore objects)
        {
            _objects = objects ?? throw new ArgumentNullException(nameof(objects));
        }

        public HyobDiffReport Diff(Hash fromCommit, Hash toCommit)
        {
            var report = new HyobDiffReport(fromCommit, toCommit);
            if (fromCommit == toCommit) return report;

            var a = ReadCommit(fromCommit);
            var b = ReadCommit(toCommit);
            DiffTreesRecursive(string.Empty, a.Tree, b.Tree, report);
            return report;
        }

        /// <summary>
        /// 直接对比两棵 tree（不经过 commit 包装）。用于 hyobD 的 WIP 模式：
        /// 一边是 BuildSnapshot 产生的 root tree、另一边是 HEAD commit 的 tree。
        /// </summary>
        public HyobDiffReport DiffTrees(Hash treeA, Hash treeB, Hash fromMarker = default, Hash toMarker = default)
        {
            var report = new HyobDiffReport(fromMarker, toMarker);
            if (treeA == treeB) return report;
            DiffTreesRecursive(string.Empty, treeA, treeB, report);
            return report;
        }

        private HyobCommit ReadCommit(Hash h)
        {
            if (!_objects.TryRead(h, out var blob))
                throw new InvalidOperationException($"commit 不存在：{h.ToHex().Substring(0, 12)}");
            return HyobCommit.Decode(blob);
        }

        private void DiffTreesRecursive(string pathPrefix, Hash treeA, Hash treeB, HyobDiffReport report)
        {
            if (treeA == treeB) return;

            var ta = ReadTree(treeA);
            var tb = ReadTree(treeB);

            var byNameA = ta.Entries.ToDictionary(e => e.Name, StringComparer.Ordinal);
            var byNameB = tb.Entries.ToDictionary(e => e.Name, StringComparer.Ordinal);

            var allNames = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var n in byNameA.Keys) allNames.Add(n);
            foreach (var n in byNameB.Keys) allNames.Add(n);

            foreach (var name in allNames)
            {
                var path = pathPrefix.Length == 0 ? name : pathPrefix + "/" + name;
                byNameA.TryGetValue(name, out var ea);
                byNameB.TryGetValue(name, out var eb);

                if (ea == null)
                {
                    EmitAdded(path, eb, report);
                }
                else if (eb == null)
                {
                    EmitDeleted(path, ea, report);
                }
                else if (ea.Hash == eb.Hash && ea.Kind == eb.Kind)
                {
                    continue;
                }
                else if (ea.Kind == HyobTreeEntryKind.Tree && eb.Kind == HyobTreeEntryKind.Tree)
                {
                    DiffTreesRecursive(path, ea.Hash, eb.Hash, report);
                }
                else if (ea.Kind == HyobTreeEntryKind.Object && eb.Kind == HyobTreeEntryKind.Object)
                {
                    var oldType = ReadType(ea.Hash);
                    var newType = ReadType(eb.Hash);
                    var changes = ComputeFieldChanges(ea.Hash, eb.Hash, oldType == newType);
                    report.Entries.Add(new HyobDiffEntry(
                        path, HyobDiffKind.Modified,
                        ea.Hash, eb.Hash, oldType, newType, changes));
                }
                else
                {
                    EmitDeleted(path, ea, report);
                    EmitAdded(path, eb, report);
                }
            }
        }

        private void EmitAdded(string path, HyobTreeEntry entry, HyobDiffReport report)
        {
            if (entry.Kind == HyobTreeEntryKind.Tree)
            {
                ListAllInTree(path, entry.Hash, /*added=*/true, report);
            }
            else
            {
                var t = ReadType(entry.Hash);
                report.Entries.Add(new HyobDiffEntry(
                    path, HyobDiffKind.Added,
                    default, entry.Hash, HyobObjectKind.Unknown, t));
            }
        }

        private void EmitDeleted(string path, HyobTreeEntry entry, HyobDiffReport report)
        {
            if (entry.Kind == HyobTreeEntryKind.Tree)
            {
                ListAllInTree(path, entry.Hash, /*added=*/false, report);
            }
            else
            {
                var t = ReadType(entry.Hash);
                report.Entries.Add(new HyobDiffEntry(
                    path, HyobDiffKind.Deleted,
                    entry.Hash, default, t, HyobObjectKind.Unknown));
            }
        }

        private void ListAllInTree(string pathPrefix, Hash treeHash, bool added, HyobDiffReport report)
        {
            HyobTree tree;
            try { tree = ReadTree(treeHash); }
            catch { return; }

            foreach (var entry in tree.Entries)
            {
                var path = pathPrefix + "/" + entry.Name;
                if (entry.Kind == HyobTreeEntryKind.Tree)
                {
                    ListAllInTree(path, entry.Hash, added, report);
                }
                else
                {
                    var t = ReadType(entry.Hash);
                    if (added)
                    {
                        report.Entries.Add(new HyobDiffEntry(
                            path, HyobDiffKind.Added,
                            default, entry.Hash, HyobObjectKind.Unknown, t));
                    }
                    else
                    {
                        report.Entries.Add(new HyobDiffEntry(
                            path, HyobDiffKind.Deleted,
                            entry.Hash, default, t, HyobObjectKind.Unknown));
                    }
                }
            }
        }

        private HyobTree ReadTree(Hash h)
        {
            if (!_objects.TryRead(h, out var blob))
                throw new InvalidOperationException($"tree 不存在：{h.ToHex().Substring(0, 12)}");
            return HyobTree.Decode(blob);
        }

        private HyobObjectKind ReadType(Hash h)
        {
            if (!_objects.TryRead(h, out var blob)) return HyobObjectKind.Unknown;
            try
            {
                var (header, _) = HyobObjectHeader.Decode(blob);
                return header.TypeId;
            }
            catch { return HyobObjectKind.Unknown; }
        }

        /// <summary>
        /// 字段级 diff（M11-B）。仅当 oldType == newType 时计算，否则返回 null（diff 退化到 hash 级）。
        /// 字段名集合由 schema 决定，old 与 new 一一对应；只输出值不同的字段。
        /// </summary>
        private List<HyobFieldChange> ComputeFieldChanges(Hash oldHash, Hash newHash, bool sameType)
        {
            if (!sameType) return null;
            if (!_objects.TryRead(oldHash, out var oldBlob)) return null;
            if (!_objects.TryRead(newHash, out var newBlob)) return null;

            var oldFields = HyobFieldExtractor.Extract(oldBlob);
            var newFields = HyobFieldExtractor.Extract(newBlob);
            if (oldFields.Count == 0 || newFields.Count == 0) return null;

            int n = Math.Min(oldFields.Count, newFields.Count);
            var result = new List<HyobFieldChange>(n);
            for (int i = 0; i < n; i++)
            {
                var ko = oldFields[i].Key; var kn = newFields[i].Key;
                if (!string.Equals(ko, kn, StringComparison.Ordinal)) continue; // schema 不一致，保守跳过
                var vo = oldFields[i].Value; var vn = newFields[i].Value;
                if (!string.Equals(vo, vn, StringComparison.Ordinal))
                    result.Add(new HyobFieldChange(ko, vo, vn));
            }
            return result;
        }
    }
}
