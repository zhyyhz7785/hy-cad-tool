using System;
using System.Globalization;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Models;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Infrastructure.Diff
{
    /// <summary>
    /// 把用户输入的 ref 表达式解析成具体 commit Hash。设计：git rev-parse 简化版。
    ///
    /// 支持：
    /// <list type="bullet">
    ///   <item><c>HEAD</c>、<c>head</c>（不区分大小写）→ 当前 HEAD（branch tip 或 detached）</item>
    ///   <item><c>HEAD~N</c>（N 为非负整数）→ 沿 first-parent 链回退 N 步</item>
    ///   <item>分支名（如 <c>main</c>）→ 该分支 tip</item>
    ///   <item>commit hash 前缀（≥4 字符，全 16 进制小写）→ 唯一匹配的 commit</item>
    /// </list>
    /// </summary>
    public sealed class RefResolver
    {
        private readonly HyobObjectStore _objects;
        private readonly HyobRefStore _refs;

        public RefResolver(HyobObjectStore objects, HyobRefStore refs)
        {
            _objects = objects ?? throw new ArgumentNullException(nameof(objects));
            _refs = refs ?? throw new ArgumentNullException(nameof(refs));
        }

        public Hash Resolve(string refExpr)
        {
            if (string.IsNullOrWhiteSpace(refExpr))
                throw new ArgumentException("ref 表达式为空", nameof(refExpr));

            refExpr = refExpr.Trim();

            if (refExpr.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
                return ResolveHead();

            if (refExpr.StartsWith("HEAD~", StringComparison.OrdinalIgnoreCase))
            {
                var nStr = refExpr.Substring(5);
                if (!int.TryParse(nStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) || n < 0)
                    throw new InvalidOperationException($"HEAD~N 中 N 必须是非负整数：{refExpr}");
                return ResolveAncestor(ResolveHead(), n);
            }

            if (_refs.TryReadBranchTip(refExpr, out var branchTip))
                return branchTip;

            if (TryResolveCommitPrefix(refExpr, out var prefixHash))
                return prefixHash;

            throw new InvalidOperationException(
                $"无法解析 ref：'{refExpr}'。支持 HEAD / HEAD~N / 分支名 / commit hash 前缀（≥4 字符）。");
        }

        private Hash ResolveHead()
        {
            if (!_refs.TryReadHead(out var branchName, out var detached))
                throw new InvalidOperationException("HEAD 未设置（仓库未初始化）");
            if (branchName != null)
            {
                if (!_refs.TryReadBranchTip(branchName, out var tip))
                    throw new InvalidOperationException($"branch '{branchName}' 无 tip");
                return tip;
            }
            return detached;
        }

        private Hash ResolveAncestor(Hash start, int n)
        {
            var current = start;
            for (int i = 0; i < n; i++)
            {
                if (!_objects.TryRead(current, out var blob))
                    throw new InvalidOperationException($"commit 不存在：{current.ToHex().Substring(0, 12)}");
                HyobCommit commit;
                try { commit = HyobCommit.Decode(blob); }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"commit Decode 失败 {current.ToHex().Substring(0, 12)}：{ex.Message}");
                }
                if (commit.Parents.Count == 0)
                    throw new InvalidOperationException(
                        $"沿 parent 回退第 {i + 1} 步时已到达 root commit（无 parent）");
                current = commit.Parents[0];
            }
            return current;
        }

        private bool TryResolveCommitPrefix(string prefix, out Hash hash)
        {
            hash = default;
            if (prefix.Length < 4 || prefix.Length > Hash.HexLength) return false;
            for (int i = 0; i < prefix.Length; i++)
            {
                char c = prefix[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            }
            var lower = prefix.ToLowerInvariant();

            int matchCount = 0;
            Hash matched = default;
            foreach (var h in _objects.EnumerateAll())
            {
                var hex = h.ToHex();
                if (!hex.StartsWith(lower, StringComparison.Ordinal)) continue;
                if (!_objects.TryRead(h, out var blob)) continue;
                try
                {
                    var (header, _) = HyobObjectHeader.Decode(blob);
                    if (header.TypeId != HyobObjectKind.Commit) continue;
                }
                catch { continue; }

                if (++matchCount > 1)
                    throw new InvalidOperationException(
                        $"hash 前缀 '{prefix}' 歧义（匹配 ≥2 个 commit），请加长前缀");
                matched = h;
            }
            if (matchCount == 1) { hash = matched; return true; }
            return false;
        }
    }
}
