using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Models;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Infrastructure.AutoCadMirror
{
    /// <summary>
    /// hyob → AutoCAD Database 反向 patch 调度器（M9-A）。
    ///
    /// 思路：
    ///   1. 从目标 root tree 解出 entities/by-handle/* 目标对象集合（仅主对象，跳过 .x / .d 兄弟）
    ///   2. 扫 ModelSpace 当前 entities，按 Handle 建索引
    ///   3. 三类操作：
    ///        Add    : target 有，DB 无                          → 构造新 Entity 追加到 ModelSpace
    ///        Modify : 双方都有，对象 hash 不一致                 → 擦除旧 + 追加新（M9-A 不保留 Handle）
    ///        Delete : DB 中 handle 在 by-handle 之外的目标里没有  → Erase
    ///   4. 不支持的 type_id（Polyline/MText/Dim/Hatch/.../Opaque）一律计入 skip 计数，不动 DWG
    ///   5. 不动 Layer / TextStyle / DimStyle / Block 表（M9-C 范畴）
    ///
    /// 安全：所有操作在单一 Transaction 内完成；任一阶段抛异常都不写入 → AutoCAD 自动回滚事务。
    /// </summary>
    public sealed class DatabaseReverseMirror
    {
        private const string EntitiesByHandleNs = "entities/by-handle";

        private readonly HyobObjectStore _objects;

        public DatabaseReverseMirror(HyobObjectStore objects)
        {
            _objects = objects ?? throw new ArgumentNullException(nameof(objects));
        }

        public sealed class ApplyStats
        {
            public int Added { get; internal set; }
            public int Modified { get; internal set; }
            public int Deleted { get; internal set; }
            public int SkippedUnsupported { get; internal set; }
            public int SkippedMissing { get; internal set; }
            public int Unchanged { get; internal set; }
            public int LayersAdded { get; internal set; }
            public int LayersUpdated { get; internal set; }
            public int LayersSkipped { get; internal set; }
            public Dictionary<HyobObjectKind, int> AddedByKind { get; } = new Dictionary<HyobObjectKind, int>();
            public Dictionary<HyobObjectKind, int> ModifiedByKind { get; } = new Dictionary<HyobObjectKind, int>();
            public Dictionary<HyobObjectKind, int> SkippedByKind { get; } = new Dictionary<HyobObjectKind, int>();
        }

        /// <summary>
        /// 把 <paramref name="targetRootTree"/> 的状态应用到 <paramref name="db"/> 的 ModelSpace。
        /// 使用单一 <see cref="Transaction"/>。失败时由 AutoCAD 事务自动回滚。
        /// </summary>
        public ApplyStats Apply(Database db, Hash targetRootTree)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));

            var targetByHandle = LoadTargetByHandle(targetRootTree);
            var stats = new ApplyStats();

            using (var tx = db.TransactionManager.StartTransaction())
            {
                // 先同步 Layer 表 —— 让后续 entity 反向 build 能挂到正确 Layer
                var layerStats = new LayerReverseMirror(_objects).Sync(db, tx, targetRootTree);
                stats.LayersAdded = layerStats.Added;
                stats.LayersUpdated = layerStats.Updated;
                stats.LayersSkipped = layerStats.Skipped;

                var bt = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                var dbByHandle = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
                foreach (ObjectId id in ms)
                {
                    if (id.IsErased) continue;
                    var dbo = tx.GetObject(id, OpenMode.ForRead) as Entity;
                    if (dbo == null) continue;
                    dbByHandle[dbo.Handle.ToString()] = id;
                }

                foreach (var kv in targetByHandle)
                {
                    string handle = kv.Key;
                    Hash targetObjHash = kv.Value;

                    if (!_objects.TryRead(targetObjHash, out var blob))
                    {
                        stats.SkippedMissing++;
                        continue;
                    }
                    var (header, _) = HyobObjectHeader.Decode(blob);
                    var kind = header.TypeId;

                    if (!HyobToEntityConverter.IsSupported(kind))
                    {
                        IncKind(stats.SkippedByKind, kind);
                        stats.SkippedUnsupported++;
                        if (dbByHandle.ContainsKey(handle)) dbByHandle.Remove(handle); // 不删
                        continue;
                    }

                    if (dbByHandle.TryGetValue(handle, out var existingId))
                    {
                        var existing = (Entity)tx.GetObject(existingId, OpenMode.ForRead);
                        var existingResult = EntityToHyobConverter.Convert(
                            existing,
                            (oid, mode) => tx.GetObject(oid, mode));
                        Hash existingHash = Hash.OfPayload(existingResult.Object.EncodeBlob());

                        if (existingHash == targetObjHash)
                        {
                            stats.Unchanged++;
                            dbByHandle.Remove(handle);
                            continue;
                        }

                        // hash 不一致 → 擦除旧 + 新建（M9-A 不保留 Handle）
                        existing.UpgradeOpen();
                        existing.Erase();
                        dbByHandle.Remove(handle);

                        if (HyobToEntityConverter.TryBuild(blob, db, tx, out var newEnt, out var layer))
                        {
                            ApplyLayerSafely(newEnt, layer, db, tx);
                            ms.AppendEntity(newEnt);
                            tx.AddNewlyCreatedDBObject(newEnt, true);
                            HyobToEntityConverter.ApplyPostAppend(newEnt, blob, db, tx);
                            stats.Modified++;
                            IncKind(stats.ModifiedByKind, kind);
                        }
                    }
                    else
                    {
                        if (HyobToEntityConverter.TryBuild(blob, db, tx, out var newEnt, out var layer))
                        {
                            ApplyLayerSafely(newEnt, layer, db, tx);
                            ms.AppendEntity(newEnt);
                            tx.AddNewlyCreatedDBObject(newEnt, true);
                            HyobToEntityConverter.ApplyPostAppend(newEnt, blob, db, tx);
                            stats.Added++;
                            IncKind(stats.AddedByKind, kind);
                        }
                    }
                }

                // 剩下还在 dbByHandle 中（target 中不存在）→ 删除前先看类型是否在白名单
                foreach (var kv in dbByHandle)
                {
                    var existing = (Entity)tx.GetObject(kv.Value, OpenMode.ForRead);
                    var probe = EntityToHyobConverter.Convert(
                        existing,
                        (oid, mode) => tx.GetObject(oid, mode));
                    if (!HyobToEntityConverter.IsSupported(probe.TypeId))
                    {
                        IncKind(stats.SkippedByKind, probe.TypeId);
                        stats.SkippedUnsupported++;
                        continue;
                    }
                    existing.UpgradeOpen();
                    existing.Erase();
                    stats.Deleted++;
                }

                tx.Commit();
            }

            return stats;
        }

        /// <summary>
        /// 解析 root → entities → by-handle → 主对象（不带 .x / .d 后缀）→ Hash。
        /// 不存在时返回空字典（视作 "目标 = 空 ModelSpace"）。
        /// </summary>
        private Dictionary<string, Hash> LoadTargetByHandle(Hash rootTree)
        {
            var result = new Dictionary<string, Hash>(StringComparer.OrdinalIgnoreCase);
            if (rootTree.IsZero) return result;
            var root = HyobTree.Decode(_objects.Read(rootTree));
            if (!root.TryFind("entities", out var entitiesEntry)
                || entitiesEntry.Kind != HyobTreeEntryKind.Tree) return result;
            var entities = HyobTree.Decode(_objects.Read(entitiesEntry.Hash));
            if (!entities.TryFind("by-handle", out var byHandleEntry)
                || byHandleEntry.Kind != HyobTreeEntryKind.Tree) return result;
            var byHandle = HyobTree.Decode(_objects.Read(byHandleEntry.Hash));
            foreach (var e in byHandle.Entries)
            {
                if (e.Kind != HyobTreeEntryKind.Object) continue;
                if (e.Name.EndsWith(".x", StringComparison.Ordinal)
                 || e.Name.EndsWith(".d", StringComparison.Ordinal)) continue;
                result[e.Name] = e.Hash;
            }
            return result;
        }

        private static void ApplyLayerSafely(Entity ent, string layerName, Database db, Transaction tx)
        {
            if (string.IsNullOrEmpty(layerName)) return;
            try
            {
                var lt = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (lt.Has(layerName)) ent.Layer = layerName;
                // 否则保留默认层（"0" 或 ByBlock），等 M9-C 反向同步图层表后再补
            }
            catch
            {
                // 任何失败都退回默认层，不阻断 patch 主流程
            }
        }

        private static void IncKind(Dictionary<HyobObjectKind, int> dict, HyobObjectKind k)
        {
            if (!dict.TryGetValue(k, out int n)) n = 0;
            dict[k] = n + 1;
        }
    }
}
