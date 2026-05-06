using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Features.DataExchange.Hyob.Domain.Models;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Infrastructure.AutoCadMirror
{
    /// <summary>镜像统计：Typed/Opaque 命中数 + XData/ExtDict 计数。</summary>
    public readonly struct MirrorStats
    {
        public int TotalCount { get; }
        public int TypedCount { get; }
        public int OpaqueCount { get; }
        public int XDataCount { get; }
        public int ExtDictCount { get; }
        public IReadOnlyDictionary<HyobObjectKind, int> ByTypeId { get; }

        public MirrorStats(int total, int typed, int opaque,
                           int xdataCount, int extDictCount,
                           IReadOnlyDictionary<HyobObjectKind, int> byTypeId)
        {
            TotalCount = total;
            TypedCount = typed;
            OpaqueCount = opaque;
            XDataCount = xdataCount;
            ExtDictCount = extDictCount;
            ByTypeId = byTypeId;
        }
    }

    /// <summary>
    /// AutoCAD <c>Database</c> ↔ hyob 镜像核心。设计：02 §3 / 03 §3 / 04 §1。
    ///
    /// M5 by-handle 树布局（每 entity 0-3 个 entry）：
    ///   <code>
    ///     by-handle/
    ///       &lt;handle&gt;        → entity object hash         必有
    ///       &lt;handle&gt;.x      → XData object hash           可选（M5 起）
    ///       &lt;handle&gt;.d      → ExtensionDictionary 对象 hash 可选（M5 起）
    ///   </code>
    /// 这种扁平布局保持 entity hash 不变（schema 不动），但 XData/ExtDict 任何变化
    /// 都会让 by-handle tree 的 hash 改变（兄弟 entry 集合变了）→ commit hash 变。
    /// </summary>
    public sealed class AutoCadDatabaseMirror
    {
        private const string DefaultBranch = "main";
        private const string ModelSpaceEntityNamespace = "entities/by-handle";

        private readonly HyobObjectStore _objects;
        private readonly HyobRefStore _refs;

        public AutoCadDatabaseMirror(HyobObjectStore objects, HyobRefStore refs)
        {
            _objects = objects ?? throw new ArgumentNullException(nameof(objects));
            _refs = refs ?? throw new ArgumentNullException(nameof(refs));
        }

        /// <summary>
        /// 把当前 <paramref name="db"/> 的 ModelSpace 快照写一个 commit 到 main 分支。
        /// 返回 (commitHash, stats)。
        /// </summary>
        public (Hash CommitHash, MirrorStats Stats) MirrorDatabaseIntoHyob(
            Database db,
            string author,
            string message,
            string command)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));

            var byHandleEntries = new List<HyobTreeEntry>();
            var byTypeId = new Dictionary<HyobObjectKind, int>();
            int typedCount = 0;
            int opaqueCount = 0;
            int xdataCount = 0;
            int extDictCount = 0;

            using (var tx = db.TransactionManager.StartOpenCloseTransaction())
            {
                EntityToHyobConverter.ObjectReader reader = (oid, mode) => tx.GetObject(oid, mode);

                var bt = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId id in ms)
                {
                    if (id.IsErased) continue;
                    var dbo = tx.GetObject(id, OpenMode.ForRead) as Entity;
                    if (dbo == null) continue;

                    var result = EntityToHyobConverter.Convert(dbo, reader);

                    Hash objHash = _objects.Write(result.Object.EncodeBlob());
                    byHandleEntries.Add(new HyobTreeEntry(result.HandleHex, HyobTreeEntryKind.Object, objHash));

                    if (result.XData != null)
                    {
                        Hash xh = _objects.Write(result.XData.EncodeBlob());
                        byHandleEntries.Add(new HyobTreeEntry(result.HandleHex + ".x", HyobTreeEntryKind.Object, xh));
                        xdataCount++;
                    }
                    if (result.ExtDict != null)
                    {
                        Hash dh = _objects.Write(result.ExtDict.EncodeBlob());
                        byHandleEntries.Add(new HyobTreeEntry(result.HandleHex + ".d", HyobTreeEntryKind.Object, dh));
                        extDictCount++;
                    }

                    if (result.IsTyped) typedCount++;
                    else opaqueCount++;

                    if (!byTypeId.TryGetValue(result.TypeId, out int n)) n = 0;
                    byTypeId[result.TypeId] = n + 1;
                }

                tx.Commit();
            }

            int total = typedCount + opaqueCount;

            var byHandleTree = new HyobTree(byHandleEntries);
            Hash byHandleTreeHash = _objects.Write(byHandleTree.EncodeBlob());

            var entitiesTree = new HyobTree(new[]
            {
                new HyobTreeEntry("by-handle", HyobTreeEntryKind.Tree, byHandleTreeHash)
            });
            Hash entitiesTreeHash = _objects.Write(entitiesTree.EncodeBlob());

            var rootTree = new HyobTree(new[]
            {
                new HyobTreeEntry("entities", HyobTreeEntryKind.Tree, entitiesTreeHash)
            });
            Hash rootTreeHash = _objects.Write(rootTree.EncodeBlob());

            var parents = new List<Hash>();
            if (_refs.TryReadBranchTip(DefaultBranch, out var tip))
                parents.Add(tip);

            var meta = new Dictionary<string, string>
            {
                { "entityCount", total.ToString(CultureInfo.InvariantCulture) },
                { "typedCount",  typedCount.ToString(CultureInfo.InvariantCulture) },
                { "opaqueCount", opaqueCount.ToString(CultureInfo.InvariantCulture) },
                { "xdataCount",  xdataCount.ToString(CultureInfo.InvariantCulture) },
                { "extDictCount", extDictCount.ToString(CultureInfo.InvariantCulture) },
                { "namespace", ModelSpaceEntityNamespace },
                { "schema", "hyob.snapshot/v3" },
            };

            var commit = new HyobCommit(
                tree: rootTreeHash,
                author: author,
                message: message,
                parents: parents,
                command: command,
                meta: meta);

            Hash commitHash = _objects.Write(commit.EncodeBlob());
            _refs.WriteBranchTip(DefaultBranch, commitHash);

            return (commitHash, new MirrorStats(total, typedCount, opaqueCount, xdataCount, extDictCount, byTypeId));
        }

        /// <summary>沿 parents 链遍历 commit 历史（从 startHash 开始，最多 limit 条）。</summary>
        public IEnumerable<(Hash CommitHash, HyobCommit Commit)> WalkHistory(Hash startHash, int limit = int.MaxValue)
        {
            var visited = new HashSet<string>();
            var queue = new Queue<Hash>();
            queue.Enqueue(startHash);
            int n = 0;

            while (queue.Count > 0 && n < limit)
            {
                var h = queue.Dequeue();
                var key = h.ToHex();
                if (!visited.Add(key)) continue;
                if (!_objects.TryRead(h, out var blob)) yield break;

                HyobCommit commit;
                try { commit = HyobCommit.Decode(blob); }
                catch { yield break; }

                yield return (h, commit);
                n++;

                foreach (var p in commit.Parents) queue.Enqueue(p);
            }
        }
    }
}
