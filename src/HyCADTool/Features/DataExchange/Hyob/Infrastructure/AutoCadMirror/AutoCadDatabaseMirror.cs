using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Features.DataExchange.Hyob.Domain.Models;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Infrastructure.AutoCadMirror
{
    /// <summary>镜像统计：Typed/Opaque 命中数 + XData/ExtDict 计数 + 表/字典计数。</summary>
    public readonly struct MirrorStats
    {
        public int TotalCount { get; }
        public int TypedCount { get; }
        public int OpaqueCount { get; }
        public int XDataCount { get; }
        public int ExtDictCount { get; }
        public int LayerCount { get; }
        public int TextStyleCount { get; }
        public int DimStyleCount { get; }
        public int BlockDefCount { get; }
        public IReadOnlyDictionary<HyobObjectKind, int> ByTypeId { get; }

        public MirrorStats(int total, int typed, int opaque,
                           int xdataCount, int extDictCount,
                           int layerCount, int textStyleCount,
                           int dimStyleCount, int blockDefCount,
                           IReadOnlyDictionary<HyobObjectKind, int> byTypeId)
        {
            TotalCount = total;
            TypedCount = typed;
            OpaqueCount = opaque;
            XDataCount = xdataCount;
            ExtDictCount = extDictCount;
            LayerCount = layerCount;
            TextStyleCount = textStyleCount;
            DimStyleCount = dimStyleCount;
            BlockDefCount = blockDefCount;
            ByTypeId = byTypeId;
        }
    }

    /// <summary>
    /// AutoCAD <c>Database</c> ↔ hyob 镜像核心。设计：02 §3 / 03 §3 / 04 §1 / 04 §6。
    ///
    /// M6 root tree 布局（schema = "hyob.snapshot/v4"）：
    ///   <code>
    ///     root/
    ///       entities/
    ///         by-handle/
    ///           &lt;handle&gt;        → entity object hash         必有
    ///           &lt;handle&gt;.x      → XData object hash           可选
    ///           &lt;handle&gt;.d      → ExtensionDictionary hash    可选
    ///       tables/
    ///         layers/      &lt;name&gt; → HyobLayerDef
    ///         text_styles/ &lt;name&gt; → HyobTextStyleDef
    ///         dim_styles/  &lt;name&gt; → HyobDimStyleDef
    ///         blocks/      &lt;name&gt; → HyobBlockDef（含 Layout / 用户 block / 匿名 block）
    ///   </code>
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
        /// 内部 = <see cref="BuildSnapshot"/> + <see cref="WriteCommit"/>。
        /// </summary>
        public (Hash CommitHash, MirrorStats Stats) MirrorDatabaseIntoHyob(
            Database db,
            string author,
            string message,
            string command)
        {
            var (rootTreeHash, stats) = BuildSnapshot(db);
            var commitHash = WriteCommit(rootTreeHash, stats, author, message, command);
            return (commitHash, stats);
        }

        /// <summary>
        /// 仅构建快照（写 hyob objects + trees），返回 root tree hash + stats，**不写 commit、不动 ref**。
        /// 用途：hyobD HEAD WIP 模式（未提交变更预览）—— 算出当前 Database 的 root tree hash
        /// 后立即与 HEAD.tree 对比，再决定是否 hyobC。
        /// 因为 hyob 是 content-addressable 仓库，反复 BuildSnapshot 不会产生重复对象（写入幂等）。
        /// </summary>
        public (Hash RootTreeHash, MirrorStats Stats) BuildSnapshot(Database db)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));

            var byHandleEntries = new List<HyobTreeEntry>();
            var byTypeId = new Dictionary<HyobObjectKind, int>();
            int typedCount = 0;
            int opaqueCount = 0;
            int xdataCount = 0;
            int extDictCount = 0;
            TablesMirror.Result tablesResult;

            using (var tx = db.TransactionManager.StartOpenCloseTransaction())
            {
                EntityToHyobConverter.ObjectReader reader = (oid, mode) => tx.GetObject(oid, mode);

                tablesResult = new TablesMirror(_objects).Mirror(db, tx);

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
                new HyobTreeEntry("entities", HyobTreeEntryKind.Tree, entitiesTreeHash),
                new HyobTreeEntry("tables",   HyobTreeEntryKind.Tree, tablesResult.TablesTreeHash),
            });
            Hash rootTreeHash = _objects.Write(rootTree.EncodeBlob());

            var stats = new MirrorStats(
                total, typedCount, opaqueCount,
                xdataCount, extDictCount,
                tablesResult.LayerCount, tablesResult.TextStyleCount,
                tablesResult.DimStyleCount, tablesResult.BlockDefCount,
                byTypeId);

            return (rootTreeHash, stats);
        }

        /// <summary>
        /// 把已构建的快照 root tree 包成 commit，写入 objects 并把 main 分支 ref 推进到新 commit。
        /// 与 <see cref="BuildSnapshot"/> 配合使用。
        /// </summary>
        public Hash WriteCommit(
            Hash rootTreeHash,
            MirrorStats stats,
            string author,
            string message,
            string command)
        {
            var parents = new List<Hash>();
            if (_refs.TryReadBranchTip(DefaultBranch, out var tip))
                parents.Add(tip);

            var meta = new Dictionary<string, string>
            {
                { "entityCount",    stats.TotalCount.ToString(CultureInfo.InvariantCulture) },
                { "typedCount",     stats.TypedCount.ToString(CultureInfo.InvariantCulture) },
                { "opaqueCount",    stats.OpaqueCount.ToString(CultureInfo.InvariantCulture) },
                { "xdataCount",     stats.XDataCount.ToString(CultureInfo.InvariantCulture) },
                { "extDictCount",   stats.ExtDictCount.ToString(CultureInfo.InvariantCulture) },
                { "layerCount",     stats.LayerCount.ToString(CultureInfo.InvariantCulture) },
                { "textStyleCount", stats.TextStyleCount.ToString(CultureInfo.InvariantCulture) },
                { "dimStyleCount",  stats.DimStyleCount.ToString(CultureInfo.InvariantCulture) },
                { "blockDefCount",  stats.BlockDefCount.ToString(CultureInfo.InvariantCulture) },
                { "namespace", ModelSpaceEntityNamespace },
                { "schema", "hyob.snapshot/v4" },
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
            return commitHash;
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
