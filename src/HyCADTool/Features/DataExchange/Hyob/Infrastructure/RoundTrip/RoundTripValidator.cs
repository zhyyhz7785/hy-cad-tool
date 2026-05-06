using System;
using System.Collections.Generic;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Models;
using HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Opaque;
using HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Typed;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Infrastructure.RoundTrip
{
    /// <summary>
    /// hyob round-trip 自检报告。设计：03 §3.5 / 04 §11。
    /// </summary>
    public sealed class RoundTripReport
    {
        public int CommitCount { get; internal set; }
        public int TreeCount { get; internal set; }
        public int ObjectCount { get; internal set; }
        public int OpaqueCount { get; internal set; }
        public int TypedCount { get; internal set; }
        public Dictionary<HyobObjectKind, int> TypedByKind { get; } = new Dictionary<HyobObjectKind, int>();
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public bool HasErrors => Errors.Count > 0;
    }

    /// <summary>
    /// 仓库自一致性校验：从 HEAD（或指定起点）沿 parent 链遍历所有 commit，
    /// 递归校验 commit / tree / object 全链：
    ///   1. blob 能从 store 读到（无悬空引用）
    ///   2. blob 能 Decode（CRC + schema 完整）
    ///   3. 类型分类正确（OpaqueObject 全 payload 也能 Decode）
    ///
    /// 字节级 DXF binary round-trip（OpaqueObject.RawDxf vs AutoCAD Database 真实序列化）
    /// 留到 M5 / M11，本期实现的是 hyob 仓库自身的链路完整性。
    /// </summary>
    public sealed class RoundTripValidator
    {
        private readonly HyobObjectStore _objects;
        private readonly HyobRefStore _refs;

        public RoundTripValidator(HyobObjectStore objects, HyobRefStore refs)
        {
            _objects = objects ?? throw new ArgumentNullException(nameof(objects));
            _refs = refs ?? throw new ArgumentNullException(nameof(refs));
        }

        public RoundTripReport Validate()
        {
            var report = new RoundTripReport();

            if (!_refs.TryReadHead(out var branch, out var detached))
            {
                report.Errors.Add("HEAD 未设置（仓库未初始化）");
                return report;
            }

            Hash startHash;
            if (branch != null)
            {
                if (!_refs.TryReadBranchTip(branch, out startHash))
                {
                    report.Errors.Add($"branch '{branch}' 无 tip");
                    return report;
                }
            }
            else
            {
                startHash = detached;
            }

            var visitedCommits = new HashSet<string>(StringComparer.Ordinal);
            var visitedTrees = new HashSet<string>(StringComparer.Ordinal);
            var visitedObjects = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<Hash>();
            queue.Enqueue(startHash);

            while (queue.Count > 0)
            {
                var ch = queue.Dequeue();
                var key = ch.ToHex();
                if (!visitedCommits.Add(key)) continue;

                if (!_objects.TryRead(ch, out var blob))
                {
                    report.Errors.Add($"commit 不存在：{key.Substring(0, 12)}");
                    continue;
                }

                HyobCommit commit;
                try { commit = HyobCommit.Decode(blob); }
                catch (Exception ex)
                {
                    report.Errors.Add($"commit Decode 失败 {key.Substring(0, 12)}：{ex.Message}");
                    continue;
                }
                report.CommitCount++;

                ValidateTreeRecursive(commit.Tree, visitedTrees, visitedObjects, report);

                foreach (var p in commit.Parents) queue.Enqueue(p);
            }

            return report;
        }

        private void ValidateTreeRecursive(
            Hash treeHash,
            HashSet<string> visitedTrees,
            HashSet<string> visitedObjects,
            RoundTripReport report)
        {
            var key = treeHash.ToHex();
            if (!visitedTrees.Add(key)) return;

            if (!_objects.TryRead(treeHash, out var blob))
            {
                report.Errors.Add($"tree 不存在：{key.Substring(0, 12)}");
                return;
            }

            HyobTree tree;
            try { tree = HyobTree.Decode(blob); }
            catch (Exception ex)
            {
                report.Errors.Add($"tree Decode 失败 {key.Substring(0, 12)}：{ex.Message}");
                return;
            }
            report.TreeCount++;

            foreach (var entry in tree.Entries)
            {
                if (entry.Kind == HyobTreeEntryKind.Tree)
                    ValidateTreeRecursive(entry.Hash, visitedTrees, visitedObjects, report);
                else
                    ValidateObject(entry.Hash, visitedObjects, report);
            }
        }

        private void ValidateObject(
            Hash objHash,
            HashSet<string> visitedObjects,
            RoundTripReport report)
        {
            var key = objHash.ToHex();
            if (!visitedObjects.Add(key)) return;

            if (!_objects.TryRead(objHash, out var blob))
            {
                report.Errors.Add($"object 不存在：{key.Substring(0, 12)}");
                return;
            }

            try
            {
                var (header, _) = HyobObjectHeader.Decode(blob);
                report.ObjectCount++;

                switch (header.TypeId)
                {
                    case HyobObjectKind.Opaque:
                        HyobOpaqueObject.Decode(blob);
                        report.OpaqueCount++;
                        break;
                    case HyobObjectKind.Line:
                        HyobLine.Decode(blob);
                        IncTyped(report, HyobObjectKind.Line);
                        break;
                    case HyobObjectKind.Polyline:
                        HyobPolyline.Decode(blob);
                        IncTyped(report, HyobObjectKind.Polyline);
                        break;
                    case HyobObjectKind.Arc:
                        HyobArc.Decode(blob);
                        IncTyped(report, HyobObjectKind.Arc);
                        break;
                    case HyobObjectKind.Circle:
                        HyobCircle.Decode(blob);
                        IncTyped(report, HyobObjectKind.Circle);
                        break;
                    case HyobObjectKind.DBText:
                        HyobDBText.Decode(blob);
                        IncTyped(report, HyobObjectKind.DBText);
                        break;
                    case HyobObjectKind.MText:
                        HyobMText.Decode(blob);
                        IncTyped(report, HyobObjectKind.MText);
                        break;
                    case HyobObjectKind.BlockReference:
                        HyobBlockReference.Decode(blob);
                        IncTyped(report, HyobObjectKind.BlockReference);
                        break;
                    case HyobObjectKind.Dimension:
                        HyobDimension.Decode(blob);
                        IncTyped(report, HyobObjectKind.Dimension);
                        break;
                    case HyobObjectKind.MLeader:
                        HyobMLeader.Decode(blob);
                        IncTyped(report, HyobObjectKind.MLeader);
                        break;
                    case HyobObjectKind.HatchBoundary:
                        HyobHatch.Decode(blob);
                        IncTyped(report, HyobObjectKind.HatchBoundary);
                        break;
                    case HyobObjectKind.Wipeout:
                        HyobWipeout.Decode(blob);
                        IncTyped(report, HyobObjectKind.Wipeout);
                        break;
                    case HyobObjectKind.XDataAttachment:
                        HyobXDataAttachment.Decode(blob);
                        IncTyped(report, HyobObjectKind.XDataAttachment);
                        break;
                    case HyobObjectKind.ExtensionDictionary:
                        HyobExtensionDictionary.Decode(blob);
                        IncTyped(report, HyobObjectKind.ExtensionDictionary);
                        break;
                    default:
                        report.TypedCount++;
                        report.Warnings.Add(
                            $"object {key.Substring(0, 12)} type=0x{(ushort)header.TypeId:X4} 尚未实现 Typed codec 校验");
                        break;
                }
            }
            catch (Exception ex)
            {
                report.Errors.Add($"object Decode 失败 {key.Substring(0, 12)}：{ex.Message}");
            }
        }

        private static void IncTyped(RoundTripReport report, HyobObjectKind kind)
        {
            report.TypedCount++;
            if (!report.TypedByKind.TryGetValue(kind, out int n)) n = 0;
            report.TypedByKind[kind] = n + 1;
        }
    }
}
