using System;
using System.Collections.Generic;
using HyCADTool.Features.DataExchange.Hyob.Domain.Models;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;

namespace HyCADTool.Features.DataExchange.Hyob.Infrastructure.Merge
{
    /// <summary>
    /// 三路 tree 合并（M8-B）。递归比较 (base, ours, theirs) 三棵树，对每个 entry 应用规则：
    /// <list type="bullet">
    ///   <item>三方相同 → 保留</item>
    ///   <item>仅 ours 改了 → 保留 ours</item>
    ///   <item>仅 theirs 改了 → 保留 theirs</item>
    ///   <item>两侧改成相同（hash 等） → 保留</item>
    ///   <item>两侧都改且子树都是 Tree → 递归合并</item>
    ///   <item>其余 → 冲突；保留 ours 作"最优努力"占位</item>
    /// </list>
    /// <para>
    /// "删除" 在三方表里表示为 entry 不存在；缺席 entry 视作 hash=default、Kind=Object 占位。
    /// 删除/修改 冲突单独识别。
    /// </para>
    /// 合并产生的新 tree 会被 Write 到 ObjectStore（content-addressable，无副作用）。
    /// </summary>
    public sealed class TreeMerger
    {
        private readonly HyobObjectStore _objects;

        public TreeMerger(HyobObjectStore objects)
        {
            _objects = objects ?? throw new ArgumentNullException(nameof(objects));
        }

        public HyobMergeResult Merge(Hash baseTree, Hash oursTree, Hash theirsTree)
        {
            // FF 检测由调用方负责（提前比较 commit），这里只看 tree。
            var conflicts = new List<HyobMergeConflict>();
            int auto = 0;
            var merged = MergeTreeRecursive(
                baseTree, oursTree, theirsTree,
                pathPrefix: string.Empty,
                conflicts: conflicts,
                autoMerged: ref auto);
            return new HyobMergeResult(merged, conflicts, auto, false, default);
        }

        private Hash MergeTreeRecursive(
            Hash baseTreeHash, Hash oursTreeHash, Hash theirsTreeHash,
            string pathPrefix,
            List<HyobMergeConflict> conflicts,
            ref int autoMerged)
        {
            if (oursTreeHash == theirsTreeHash) return oursTreeHash;
            if (oursTreeHash == baseTreeHash)
            {
                autoMerged += CountSubtreeChanges(baseTreeHash, theirsTreeHash);
                return theirsTreeHash;
            }
            if (theirsTreeHash == baseTreeHash)
            {
                autoMerged += CountSubtreeChanges(baseTreeHash, oursTreeHash);
                return oursTreeHash;
            }

            var baseTree = LoadTree(baseTreeHash);
            var oursTree = LoadTree(oursTreeHash);
            var theirsTree = LoadTree(theirsTreeHash);

            var merged = new List<HyobTreeEntry>();

            var allNames = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var e in baseTree.Entries) allNames.Add(e.Name);
            foreach (var e in oursTree.Entries) allNames.Add(e.Name);
            foreach (var e in theirsTree.Entries) allNames.Add(e.Name);

            foreach (var name in allNames)
            {
                baseTree.TryFind(name, out var b);
                oursTree.TryFind(name, out var o);
                theirsTree.TryFind(name, out var t);

                string subPath = string.IsNullOrEmpty(pathPrefix) ? name : pathPrefix + "/" + name;

                if (Equal(o, t)) { if (o != null) merged.Add(o); continue; }
                if (Equal(o, b)) { if (t != null) { merged.Add(t); autoMerged++; } continue; }
                if (Equal(t, b)) { if (o != null) { merged.Add(o); autoMerged++; } continue; }

                // 双方都改 / 删，base 也可能不存在
                if (o != null && t != null
                    && o.Kind == HyobTreeEntryKind.Tree
                    && t.Kind == HyobTreeEntryKind.Tree)
                {
                    Hash bh = b != null && b.Kind == HyobTreeEntryKind.Tree ? b.Hash : EmptyTreeHash();
                    var subMerged = MergeTreeRecursive(bh, o.Hash, t.Hash, subPath, conflicts, ref autoMerged);
                    merged.Add(new HyobTreeEntry(name, HyobTreeEntryKind.Tree, subMerged));
                    continue;
                }

                if (o == null && t == null)
                {
                    continue;
                }

                if (o == null && t != null)
                {
                    conflicts.Add(new HyobMergeConflict(
                        subPath, HyobConflictKind.DeleteModify,
                        b?.Hash ?? default, default, t.Hash));
                    continue;
                }

                if (o != null && t == null)
                {
                    conflicts.Add(new HyobMergeConflict(
                        subPath, HyobConflictKind.ModifyDelete,
                        b?.Hash ?? default, o.Hash, default));
                    merged.Add(o);
                    continue;
                }

                var kind = b == null ? HyobConflictKind.AddAdd : HyobConflictKind.ModifyModify;
                conflicts.Add(new HyobMergeConflict(
                    subPath, kind,
                    b?.Hash ?? default, o.Hash, t.Hash));
                merged.Add(o);
            }

            var newTree = new HyobTree(merged);
            return _objects.Write(newTree.EncodeBlob());
        }

        private static bool Equal(HyobTreeEntry a, HyobTreeEntry b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            return a.Kind == b.Kind && a.Hash == b.Hash;
        }

        private HyobTree LoadTree(Hash h)
        {
            if (h.IsZero) return new HyobTree(new HyobTreeEntry[0]);
            return HyobTree.Decode(_objects.Read(h));
        }

        private Hash EmptyTreeHash()
        {
            var t = new HyobTree(new HyobTreeEntry[0]);
            return _objects.Write(t.EncodeBlob());
        }

        /// <summary>
        /// 估算两棵树间的 entry 数量差（用于 "auto-merged" 计数，浅层近似）。
        /// 不递归子树以保持开销可控；准确数值由 hyobD 提供。
        /// </summary>
        private int CountSubtreeChanges(Hash baseHash, Hash newHash)
        {
            if (baseHash == newHash) return 0;
            var b = LoadTree(baseHash);
            var n = LoadTree(newHash);
            var bMap = new Dictionary<string, HyobTreeEntry>(StringComparer.Ordinal);
            foreach (var e in b.Entries) bMap[e.Name] = e;
            int diff = 0;
            foreach (var e in n.Entries)
            {
                if (!bMap.TryGetValue(e.Name, out var be)) diff++;
                else if (be.Hash != e.Hash || be.Kind != e.Kind) diff++;
                bMap.Remove(e.Name);
            }
            diff += bMap.Count;
            return diff;
        }
    }
}
