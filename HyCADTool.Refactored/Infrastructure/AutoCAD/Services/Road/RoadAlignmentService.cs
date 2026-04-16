using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Refactored.Domain.Events.Road;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road
{
    /// <summary>
    /// 平面线位服务。
    ///
    /// 职责：
    /// - P0：打通"命令 → 服务 → Domain → 事件总线 → JSON 持久化"闭环（<see cref="Create"/> 空白创建）。
    /// - P1：从 AutoCAD 多段线导入 / 同步 Alignment（<see cref="ImportFromPolyline"/>），写 Xdata 挂 GUID。
    /// - 后续（P1.b）：从 Domain 反向绘制 → <see cref="RedrawCenterline"/>（另见 <c>RoadGeometryBridge.CreatePolylineFrom3d</c>）。
    ///
    /// Xdata 策略：
    /// - 同一条 AutoCAD 多段线重复拾取时，若已挂 HY_ROAD/ID 则按 Updated 同步几何，否则按 Created 新建。
    /// - 若 Xdata 记录的 GUID 在 Domain 里不存在（文件被删 / 剪切等），按"Xdata 孤儿"处理：
    ///   沿用原 GUID 重建 Alignment，保证 DWG ↔ JSON 同步。
    /// </summary>
    public sealed class RoadAlignmentService
    {
        private readonly RoadDesignRegistry _registry;
        private readonly IRoadEventBus _eventBus;

        public RoadAlignmentService(RoadDesignRegistry registry, IRoadEventBus eventBus)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        /// <summary>
        /// P0 占位：为指定文档创建一个空白平面线位（仅 Domain 层，不拾取）。
        /// </summary>
        public Alignment Create(string documentName, string displayName = null)
        {
            var design = _registry.GetOrCreate(documentName);
            var alignment = new Alignment
            {
                Name = string.IsNullOrWhiteSpace(displayName)
                    ? $"Alignment {design.Alignments.Count + 1}"
                    : displayName
            };
            design.Alignments.Add(alignment);
            design.LastModifiedUtc = DateTime.UtcNow;

            _eventBus.Publish(new AlignmentChangedEvent(design.Id, alignment.Id, RoadChangeKind.Created));
            return alignment;
        }

        /// <summary>
        /// P1：从 AutoCAD 2D 多段线导入 / 同步平面线位。
        /// </summary>
        /// <param name="documentName">DWG 文档名（用于索引 <see cref="RoadDesignRegistry"/>）。</param>
        /// <param name="transaction">调用方开启的 Transaction（本方法不自主提交，由调用方负责 <c>Commit</c>）。</param>
        /// <param name="database">活动 Database（用于注册 RegApp）。</param>
        /// <param name="polyline">已在 <paramref name="transaction"/> 作用域内打开的多段线（需 ForRead 或更高）。</param>
        /// <param name="displayName">可选显示名；为空时自动生成 <c>"Alignment N"</c>。</param>
        /// <returns>创建或更新后的 <see cref="Alignment"/>。</returns>
        public Alignment ImportFromPolyline(
            string documentName,
            Transaction transaction,
            Database database,
            Polyline polyline,
            string displayName = null)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            var design = _registry.GetOrCreate(documentName);
            var centerline = RoadGeometryBridge.ToDomain(polyline);

            var existingId = HyRoadXdata.ReadId(transaction, polyline);
            Alignment alignment;
            RoadChangeKind kind;

            if (existingId != Guid.Empty)
            {
                alignment = design.Alignments.FirstOrDefault(a => a.Id == existingId);
                if (alignment != null)
                {
                    alignment.Centerline = centerline;
                    kind = RoadChangeKind.Updated;
                }
                else
                {
                    // Xdata 孤儿：沿用 Xdata 的 Id 重建，避免破坏 AutoCAD ↔ JSON 的身份一致性。
                    alignment = new Alignment
                    {
                        Id = existingId,
                        Name = string.IsNullOrWhiteSpace(displayName)
                            ? $"Alignment {design.Alignments.Count + 1}"
                            : displayName,
                        Centerline = centerline
                    };
                    design.Alignments.Add(alignment);
                    kind = RoadChangeKind.Created;
                }
            }
            else
            {
                alignment = new Alignment
                {
                    Name = string.IsNullOrWhiteSpace(displayName)
                        ? $"Alignment {design.Alignments.Count + 1}"
                        : displayName,
                    Centerline = centerline
                };
                design.Alignments.Add(alignment);
                kind = RoadChangeKind.Created;
            }

            HyRoadXdata.Write(transaction, database, polyline, alignment.Id, "Alignment", SchemaVersion.Current);

            design.LastModifiedUtc = DateTime.UtcNow;
            _eventBus.Publish(new AlignmentChangedEvent(design.Id, alignment.Id, kind));
            return alignment;
        }

        /// <summary>
        /// P1.b：从 Domain（JSON 权威源）反向对齐 DWG 的中心线图元。
        ///
        /// Load 语义是"以 JSON 为准覆盖 DWG 几何"，所以策略是 update-in-place-or-create：
        /// - 扫描 ModelSpace，收集已挂 HY_ROAD/ID=Alignment 的 Polyline，建立 <c>Guid → Polyline</c> 字典；
        /// - 对 Domain 中每个 <see cref="Alignment"/>：
        ///     · 字典命中 ⇒ 通过 <see cref="RoadGeometryBridge.UpdateAutoCadPolyline"/> 就地替换顶点 + bulge，
        ///       保留 ObjectId / 图层 / Xdata / 用户自定义属性；
        ///     · 字典未命中 ⇒ 新建 Polyline + 写 HY_ROAD Xdata；
        /// - 用户未挂 Xdata 的普通 polyline 不受影响（道路模块只管"道路图元"）。
        ///
        /// 这个行为切换修复了 P1 初版的致命 bug：原策略"已存在就跳过"会让 Load 无法更新几何，
        /// 导致 JSON 里再改什么都拉不进 DWG —— 用户看到的永远是"第一次生成的直线"。
        /// </summary>
        /// <returns>新建与更新的 polyline 数量。</returns>
        public (int Created, int Updated) RedrawCenterlines(
            string documentName,
            Transaction transaction,
            Database database,
            string layerName = null)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (database == null) throw new ArgumentNullException(nameof(database));

            if (!_registry.TryGet(documentName, out var design)) return (0, 0);
            if (design.Alignments.Count == 0) return (0, 0);

            var bt = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)transaction.GetObject(
                bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            // 只收集 HY_ROAD/KIND=Alignment 的 polyline；
            // 避免未来 Profile / Template 用同机制时相互误伤。
            var existing = new Dictionary<Guid, Polyline>();
            foreach (ObjectId id in ms)
            {
                var ent = transaction.GetObject(id, OpenMode.ForRead);
                if (!(ent is Polyline poly)) continue;
                var gid = HyRoadXdata.ReadId(transaction, ent);
                if (gid == Guid.Empty) continue;
                var kind = HyRoadXdata.ReadKind(transaction, ent);
                if (!string.Equals(kind, "Alignment", StringComparison.Ordinal)) continue;
                existing[gid] = poly;
            }

            int created = 0;
            int updated = 0;
            string effectiveLayer = string.IsNullOrWhiteSpace(layerName)
                ? HyRoadLayers.AlignmentLayer
                : layerName;
            bool layerUsable = LayerExists(transaction, database, effectiveLayer);

            foreach (var a in design.Alignments)
            {
                if (a.Centerline == null || a.Centerline.VertexCount < 2) continue;

                if (existing.TryGetValue(a.Id, out var oldPoly))
                {
                    if (!oldPoly.IsWriteEnabled) oldPoly.UpgradeOpen();
                    RoadGeometryBridge.UpdateAutoCadPolyline(oldPoly, a.Centerline);
                    updated++;
                }
                else
                {
                    var poly = RoadGeometryBridge.ToAutoCadPolyline(a.Centerline);
                    if (layerUsable) poly.Layer = effectiveLayer;
                    ms.AppendEntity(poly);
                    transaction.AddNewlyCreatedDBObject(poly, true);

                    HyRoadXdata.Write(transaction, database, poly, a.Id, "Alignment", SchemaVersion.Current);
                    created++;
                }
            }

            return (created, updated);
        }

        private static bool LayerExists(Transaction tr, Database db, string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return false;
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            return lt.Has(layerName);
        }

        /// <summary>
        /// 从 Domain 聚合根删除平面线位。真实 DWG 清理留给命令层（可选）。
        /// </summary>
        public bool Delete(string documentName, Guid alignmentId)
        {
            if (!_registry.TryGet(documentName, out var design)) return false;
            int removed = design.Alignments.RemoveAll(a => a.Id == alignmentId);
            if (removed == 0) return false;

            design.LastModifiedUtc = DateTime.UtcNow;
            _eventBus.Publish(new AlignmentChangedEvent(design.Id, alignmentId, RoadChangeKind.Deleted));
            return true;
        }

        /// <summary>
        /// 扫描当前 DWG 的 HY_ROAD Xdata，必要时把 Registry 里 "属于本 DWG 但挂在老 key" 的 design
        /// 迁到 <paramref name="documentName"/> 这个新 key。
        ///
        /// 场景：
        /// - SAVEAS 把 <c>Drawing1.dwg</c> 另存为 <c>Drawing3.dwg</c> 后，<c>MdiActiveDocument.Name</c> 变成新路径，
        ///   但 Registry 里的道路数据还挂在老 key 下。直接跑 <c>hyRoadSave</c> / <c>hyRoadA</c> 会 <c>GetOrCreate</c>
        ///   出一个同名但空的 design → 最终产生"空 <c>.roaddesign.json</c>"、"hyRoadLoad 一直跑回老数据"等诡异现象。
        /// - 打开另一个"Xdata 相同但 DWG 不同名"的副本时，也希望优先复用内存中的 design 而不是新建。
        ///
        /// 决策：
        /// - 以 <strong>Xdata 里的 <see cref="Alignment.Id"/> 作为身份锚</strong>（决策 4 - DWG Xdata）；
        /// - 当前 doc.Name 对应的 design 已包含至少一个 Xdata Guid ⇒ 不动；
        /// - 其他 key 下的 design 包含 Xdata Guid ⇒ 通过 <see cref="RoadDesignRegistry.Rekey"/> 迁移，
        ///   或（当前 key 已有空壳时）<see cref="RoadDesignRegistry.Replace"/> 覆盖，顺带发布 Reloaded 事件、触发写盘；
        /// - DWG 里根本没有 HY_ROAD Xdata ⇒ 不动（Registry 应保持和 DWG 一致）。
        /// </summary>
        /// <returns>绑定到 <paramref name="documentName"/> 的 design；没有可绑定的候选时返回 <c>null</c>（或当前 key 下已有的空 design）。</returns>
        public RoadDesign RebindForDocument(string documentName, Transaction transaction, Database database)
        {
            if (string.IsNullOrEmpty(documentName)) return null;
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (database == null) throw new ArgumentNullException(nameof(database));

            // 1) 扫 Xdata：收集本 DWG 的所有 HY_ROAD Alignment Guid
            var xdataIds = new HashSet<Guid>();
            var bt = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)transaction.GetObject(
                bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                var ent = transaction.GetObject(id, OpenMode.ForRead);
                if (ent == null) continue;
                var gid = HyRoadXdata.ReadId(transaction, ent);
                if (gid == Guid.Empty) continue;
                var kind = HyRoadXdata.ReadKind(transaction, ent);
                if (!string.Equals(kind, "Alignment", StringComparison.Ordinal)) continue;
                xdataIds.Add(gid);
            }

            _registry.TryGet(documentName, out var current);

            // 2) 当前 key 已经能覆盖所有 Xdata 身份 ⇒ 无需 rebind
            if (current != null)
            {
                if (xdataIds.Count == 0) return current;
                bool allCovered = true;
                foreach (var xid in xdataIds)
                {
                    if (!current.Alignments.Any(a => a.Id == xid))
                    {
                        allCovered = false;
                        break;
                    }
                }
                if (allCovered) return current;
            }

            if (xdataIds.Count == 0) return current;

            // 3) 找"至少含一个 Xdata Guid"的旧 key
            string oldKey = null;
            RoadDesign oldDesign = null;
            foreach (var kv in _registry.Snapshot())
            {
                if (string.Equals(kv.Key, documentName, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var a in kv.Value.Alignments)
                {
                    if (xdataIds.Contains(a.Id))
                    {
                        oldKey = kv.Key;
                        oldDesign = kv.Value;
                        break;
                    }
                }
                if (oldDesign != null) break;
            }

            if (oldDesign == null) return current;

            // 4) 执行迁移
            if (current == null)
            {
                // 新 key 空 → 直接 rekey，不发事件、不触发写盘；调用方会按需发起 Save
                _registry.Rekey(oldKey, documentName);
                return oldDesign;
            }

            // 新 key 已存在（多半是 GetOrCreate 出来的空壳）：
            // 先把老 key 删掉避免孤儿，再 Replace 新 key（Replace 会发布 Reloaded 事件，
            // 自动持久化服务会据此把 JSON 写到新 path；老 path 的 .roaddesign.json 作为历史残留）
            _registry.Remove(oldKey);
            _registry.Replace(documentName, oldDesign);
            return oldDesign;
        }
    }
}
