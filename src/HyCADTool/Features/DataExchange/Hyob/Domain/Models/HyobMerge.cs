using System.Collections.Generic;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Models
{
    /// <summary>三路合并冲突类型（M8-B）。</summary>
    public enum HyobConflictKind : byte
    {
        /// <summary>双方都修改同一对象到不同内容。</summary>
        ModifyModify = 1,
        /// <summary>双方都新增同一路径但内容不同（add/add，base 中不存在）。</summary>
        AddAdd       = 2,
        /// <summary>ours 修改 / theirs 删除。</summary>
        ModifyDelete = 3,
        /// <summary>ours 删除 / theirs 修改。</summary>
        DeleteModify = 4,
    }

    /// <summary>单条三路合并冲突（M8-B）。</summary>
    public sealed class HyobMergeConflict
    {
        public string Path { get; }
        public HyobConflictKind Kind { get; }
        /// <summary>对于 AddAdd 为 default(Hash)。</summary>
        public Hash BaseHash { get; }
        /// <summary>对于 ours 删除（DeleteModify）为 default(Hash)。</summary>
        public Hash OursHash { get; }
        /// <summary>对于 theirs 删除（ModifyDelete）为 default(Hash)。</summary>
        public Hash TheirsHash { get; }

        public HyobMergeConflict(string path, HyobConflictKind kind,
            Hash baseHash, Hash oursHash, Hash theirsHash)
        {
            Path = path ?? string.Empty;
            Kind = kind;
            BaseHash = baseHash;
            OursHash = oursHash;
            TheirsHash = theirsHash;
        }
    }

    /// <summary>
    /// 三路合并产物（M8-B）。
    /// <para>
    /// IsFastForward = true：ours 是 theirs 祖先（或反之），不需要新建 merge commit；
    /// MergedTreeHash 给出胜方 tree。
    /// </para>
    /// <para>
    /// 当 Conflicts.Count &gt; 0 时，<see cref="MergedTreeHash"/> 为部分合并结果（无冲突子树已合，
    /// 冲突路径默认保留 ours 内容），<b>不应直接写 commit</b>，由调用方决定是否写入"含冲突标记"的 tree。
    /// </para>
    /// </summary>
    public sealed class HyobMergeResult
    {
        public Hash MergedTreeHash { get; }
        public IReadOnlyList<HyobMergeConflict> Conflicts { get; }
        public int AutoMergedCount { get; }
        public bool IsFastForward { get; }
        public Hash FastForwardTarget { get; }

        public HyobMergeResult(
            Hash mergedTreeHash,
            IReadOnlyList<HyobMergeConflict> conflicts,
            int autoMerged,
            bool isFastForward,
            Hash fastForwardTarget)
        {
            MergedTreeHash = mergedTreeHash;
            Conflicts = conflicts ?? new List<HyobMergeConflict>();
            AutoMergedCount = autoMerged;
            IsFastForward = isFastForward;
            FastForwardTarget = fastForwardTarget;
        }

        public bool HasConflicts => Conflicts.Count > 0;
    }
}
