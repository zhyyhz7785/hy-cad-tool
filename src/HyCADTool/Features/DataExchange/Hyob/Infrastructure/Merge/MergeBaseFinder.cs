using System;
using System.Collections.Generic;
using HyCADTool.Features.DataExchange.Hyob.Domain.Models;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;

namespace HyCADTool.Features.DataExchange.Hyob.Infrastructure.Merge
{
    /// <summary>
    /// 沿 parent DAG 找两个 commit 的最近公共祖先（merge-base，LCA）。
    /// 算法：双 BFS，相互向 parent 方向推进；任一侧到达对方已访问集合即命中。
    /// hyob 当前 commit 多 parent 极少（仅 merge commit），此简单实现足够。
    /// 设计：04 §8（M8 三路合并）。
    /// </summary>
    public sealed class MergeBaseFinder
    {
        private readonly HyobObjectStore _objects;

        public MergeBaseFinder(HyobObjectStore objects)
        {
            _objects = objects ?? throw new ArgumentNullException(nameof(objects));
        }

        /// <summary>
        /// 返回 (a, b) 的最近公共祖先 commit hash。
        /// 不存在公共祖先（独立历史）时 found = false。
        /// 若 a == b，直接返回 a。
        /// </summary>
        public bool TryFind(Hash a, Hash b, out Hash mergeBase)
        {
            if (a == b) { mergeBase = a; return true; }

            var visitedA = new HashSet<string>(StringComparer.Ordinal) { a.ToHex() };
            var visitedB = new HashSet<string>(StringComparer.Ordinal) { b.ToHex() };
            var qa = new Queue<Hash>(); qa.Enqueue(a);
            var qb = new Queue<Hash>(); qb.Enqueue(b);

            while (qa.Count > 0 || qb.Count > 0)
            {
                if (qa.Count > 0 && Step(qa, visitedA, visitedB, out mergeBase)) return true;
                if (qb.Count > 0 && Step(qb, visitedB, visitedA, out mergeBase)) return true;
            }
            mergeBase = default;
            return false;
        }

        private bool Step(
            Queue<Hash> queue,
            HashSet<string> myVisited,
            HashSet<string> otherVisited,
            out Hash mergeBase)
        {
            var cur = queue.Dequeue();
            if (!_objects.TryRead(cur, out var blob))
            {
                mergeBase = default;
                return false;
            }
            HyobCommit commit;
            try { commit = HyobCommit.Decode(blob); }
            catch { mergeBase = default; return false; }

            foreach (var p in commit.Parents)
            {
                var hex = p.ToHex();
                if (otherVisited.Contains(hex)) { mergeBase = p; return true; }
                if (myVisited.Add(hex)) queue.Enqueue(p);
            }
            mergeBase = default;
            return false;
        }

        /// <summary>判定 ancestor 是否是 descendant 的祖先（含等于自身）。</summary>
        public bool IsAncestor(Hash ancestor, Hash descendant)
        {
            if (ancestor == descendant) return true;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var q = new Queue<Hash>();
            q.Enqueue(descendant);
            visited.Add(descendant.ToHex());
            string target = ancestor.ToHex();
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                if (!_objects.TryRead(cur, out var blob)) continue;
                HyobCommit commit;
                try { commit = HyobCommit.Decode(blob); }
                catch { continue; }
                foreach (var p in commit.Parents)
                {
                    var hex = p.ToHex();
                    if (string.Equals(hex, target, StringComparison.Ordinal)) return true;
                    if (visited.Add(hex)) q.Enqueue(p);
                }
            }
            return false;
        }
    }
}
