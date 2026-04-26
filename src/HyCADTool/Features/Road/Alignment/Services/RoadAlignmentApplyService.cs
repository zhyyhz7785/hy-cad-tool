using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Features.Road.Events;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.AutoCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Features.Road.PlanAlignment.Domain;

namespace HyCADTool.Features.Road.PlanAlignment.Services
{
    /// <summary>
    /// 「应用 / 一键定稿」服务 — 路线工作台新工作流的终端操作。
    /// 合并并取代原先两步流水：
    /// <list type="bullet">
    ///   <item><c>RoadAlignmentCommitService.Commit</c>（UserPicked 草稿 → 正式 Polyline）</item>
    ///   <item><c>RoadAlignmentPiPipeline.RebuildAndPersist</c>（已有正式线的 PI 编辑落地）</item>
    /// </list>
    ///
    /// <para>Apply 的一次调用同时覆盖两种场景：</para>
    /// <list type="number">
    ///   <item>
    ///     <b>首次定稿</b>：DWG 里尚无本 alignment 的 <see cref="HyRoadXdata.KindAlignment"/> Polyline。
    ///     在 <see cref="HyRoadLayers.AlignmentLayer"/> 新建一条并挂 HY_ROAD Xdata。
    ///   </item>
    ///   <item>
    ///     <b>再次定稿（就地更新）</b>：已有 KindAlignment Polyline（同 id）。
    ///     用 <see cref="RoadGeometryBridge.UpdateAutoCadPolyline"/> 把顶点改写为新几何，<b>不删原实体</b>。
    ///     这避免了「ObjectId 变化」对后续标注 / Corridor 关联的破坏。
    ///   </item>
    /// </list>
    ///
    /// <para>Apply 定稿后的清理边界（v2 · 2026-04-21 简化）：</para>
    /// <list type="bullet">
    ///   <item><b>临时解锁</b> <see cref="HyRoadLayers.RawPolylineLayer"/> → 擦本 alignment 的
    ///     <see cref="HyRoadXdata.KindAlignmentRawPick"/>（拾取档案，定稿后用户不再需要对照"原始形态"）
    ///     + <see cref="HyRoadXdata.KindAlignmentDesignPreview"/>（历史 DWG 可能残留的自动彩色，
    ///     v2 工作流已不再生成）→ 恢复锁定；</item>
    ///   <item><b>不</b>擦 <see cref="HyRoadXdata.KindAlignmentLivePreview"/>（用户快照 — 用户自管，Apply 不动）；</item>
    ///   <item><b>不</b>擦正式线本身（KIND=Alignment 就地 UpdateAutoCadPolyline，ObjectId 保持不变）。</item>
    /// </list>
    ///
    /// <para>最后同步 <see cref="AlignmentSource"/>（UserPicked → PiTable）、更新 <see cref="RoadDesign.LastModifiedUtc"/>、
    /// 写 <c>.roaddesign.json</c>、发布 <see cref="AlignmentChangedEvent"/>（Created 首次 / Updated 再次）。</para>
    /// </summary>
    public sealed class RoadAlignmentApplyService
    {
        private readonly RoadDesignRegistry _registry;
        private readonly IRoadEventBus _eventBus;
        private readonly RoadJsonExportService _jsonExport;
        private readonly RoadAlignmentService _alignmentSvc;

        public RoadAlignmentApplyService(
            RoadDesignRegistry registry,
            IRoadEventBus eventBus,
            RoadJsonExportService jsonExport,
            RoadAlignmentService alignmentSvc)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _jsonExport = jsonExport ?? throw new ArgumentNullException(nameof(jsonExport));
            _alignmentSvc = alignmentSvc ?? throw new ArgumentNullException(nameof(alignmentSvc));
        }

        /// <summary>Apply 的结果汇总（给 VM 做 UI 提示用）。</summary>
        public sealed class ApplyResult
        {
            /// <summary>几何重建 + DWG 落地 + JSON 落盘全部成功。</summary>
            public bool Success;
            /// <summary>本次是否在 05_hy_道路_平面线位 新建了一条正式 Polyline（首次定稿）。</summary>
            public bool CreatedNewFormalPolyline;
            /// <summary>本次是否对已有正式 Polyline 做了就地几何更新（再次定稿）。</summary>
            public bool UpdatedExistingFormalPolyline;
            /// <summary>定稿后在 <c>05_hy_道路_原线</c> 擦除的 HY_ROAD 实体总数
            /// （本 Id 的 RawPick 档案 + 历史 DesignPreview 残留）。</summary>
            public int ErasedRawAndPreviewCount;
            /// <summary>AlignmentPiDesigner.Build 的结果（圆角 / 缓和段数 / 警告列表）。</summary>
            public PiDesignResult GeometryResult;
            /// <summary>JSON 落盘路径；DWG 未保存时为 null。</summary>
            public string JsonPath;
            /// <summary>失败时的说明；Success=true 时也可携带次要提示（如 JSON 落盘异常）。</summary>
            public string Message;
        }

        /// <summary>
        /// 把 <paramref name="elements"/>（工作台当前 PI 表）定稿到 <paramref name="alignment"/>。
        /// 本方法自行 <c>LockDocument</c> + 开事务 + Commit，调用方无需再包事务。
        /// </summary>
        public ApplyResult Apply(
            Document doc,
            Alignment alignment,
            IReadOnlyList<PiElement> elements,
            PiDesignOptions options = null)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            var r = new ApplyResult();

            if (alignment == null)
            {
                r.Message = "Alignment 为空。";
                return r;
            }
            if (elements == null || elements.Count < 2)
            {
                r.Message = "PI 元素不足 2 点，无法定稿。";
                return r;
            }
            if (options == null) options = new PiDesignOptions();

            PiDesignResult buildResult;
            try
            {
                buildResult = AlignmentPiDesigner.Build(elements, options);
            }
            catch (Exception ex)
            {
                r.Message = "几何重建失败：" + ex.Message;
                return r;
            }
            r.GeometryResult = buildResult;

            if (buildResult.Polyline == null || buildResult.Polyline.VertexCount < 2)
            {
                r.Message = "重建得到的 Polyline 无效（顶点 < 2）。";
                return r;
            }

            var db = doc.Database;
            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    _alignmentSvc.RebindForDocument(doc.Name, tr, db);

                    var target = FindFormalAlignmentPolyline(tr, db, alignment.Id);
                    if (target != null)
                    {
                        if (!target.IsWriteEnabled) target.UpgradeOpen();
                        RoadGeometryBridge.UpdateAutoCadPolyline(target, buildResult.Polyline);
                        r.UpdatedExistingFormalPolyline = true;
                    }
                    else
                    {
                        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                        var poly = RoadGeometryBridge.ToAutoCadPolyline(buildResult.Polyline);
                        if (LayerExists(tr, db, HyRoadLayers.AlignmentLayer))
                        {
                            poly.Layer = HyRoadLayers.AlignmentLayer;
                        }
                        ms.AppendEntity(poly);
                        tr.AddNewlyCreatedDBObject(poly, true);
                        HyRoadXdata.Write(tr, db, poly, alignment.Id, HyRoadXdata.KindAlignment, SchemaVersion.Current);
                        r.CreatedNewFormalPolyline = true;
                    }

                    alignment.Centerline = buildResult.Polyline;
                    alignment.Source = BuildSource(elements);

                    // v2 工作流：Apply = 路线调整结束；原线层 05_hy_道路_原线 上本 Alignment 的
                    // 所有 HY_ROAD 实体（RawPick 档案 + 任何残留 DesignPreview）都清掉。
                    // 该层启动即锁定，故须 LayerLockScope.Unlock 临时解锁再擦。
                    // 用户快照 LivePreview（05_hy_道路_预览）不动 — 用户自管。
                    int erased = 0;
                    using (LayerLockScope.Unlock(tr, db, HyRoadLayers.RawPolylineLayer))
                    {
                        erased += EraseByKindAndId(tr, db, HyRoadXdata.KindAlignmentRawPick, alignment.Id);
                        erased += EraseByKindAndId(tr, db, HyRoadXdata.KindAlignmentDesignPreview, alignment.Id);
                    }
                    r.ErasedRawAndPreviewCount = erased;

                    tr.Commit();
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                r.Message = $"DWG 更新失败：{ex.Message}（ErrorStatus={ex.ErrorStatus}）。";
                return r;
            }
            catch (Exception ex)
            {
                r.Message = "DWG 更新失败：" + ex.Message;
                return r;
            }

            if (_registry.TryGet(doc.Name, out var design))
            {
                design.LastModifiedUtc = DateTime.UtcNow;
                try
                {
                    r.JsonPath = _jsonExport.SaveForDocument(design, doc.Name);
                }
                catch (Exception ex)
                {
                    r.Message = "JSON 落盘异常：" + ex.Message;
                }

                _eventBus.Publish(new AlignmentChangedEvent(
                    design.Id,
                    alignment.Id,
                    r.CreatedNewFormalPolyline ? RoadChangeKind.Created : RoadChangeKind.Updated));
            }

            r.Success = true;
            if (string.IsNullOrEmpty(r.Message))
            {
                r.Message = r.CreatedNewFormalPolyline
                    ? $"首次定稿：已在 {HyRoadLayers.AlignmentLayer} 新建 Polyline；擦除原线层本 Id {r.ErasedRawAndPreviewCount} 条（RawPick + 预览残留）。"
                    : $"已就地更新 {HyRoadLayers.AlignmentLayer} Polyline；擦除原线层本 Id {r.ErasedRawAndPreviewCount} 条（RawPick + 预览残留）。";
            }
            return r;
        }

        private static Polyline FindFormalAlignmentPolyline(Transaction tr, Database db, Guid alignmentId)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                if (!(ent is Polyline poly)) continue;
                var k = HyRoadXdata.ReadKind(tr, ent);
                if (!string.Equals(k, HyRoadXdata.KindAlignment, StringComparison.Ordinal)) continue;
                var gid = HyRoadXdata.ReadId(tr, ent);
                if (gid == alignmentId) return poly;
            }
            return null;
        }

        private static int EraseByKindAndId(Transaction tr, Database db, string xdataKind, Guid alignmentId)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            var toErase = new List<ObjectId>();
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                if (ent == null) continue;
                var k = HyRoadXdata.ReadKind(tr, ent);
                if (!string.Equals(k, xdataKind, StringComparison.Ordinal)) continue;
                var gid = HyRoadXdata.ReadId(tr, ent);
                if (gid != alignmentId) continue;
                toErase.Add(id);
            }
            foreach (var id in toErase)
            {
                var ent = tr.GetObject(id, OpenMode.ForWrite);
                if (ent != null && !ent.IsErased) ent.Erase();
            }
            return toErase.Count;
        }

        private static AlignmentSource BuildSource(IReadOnlyList<PiElement> elements)
        {
            var s = new AlignmentSource { Kind = AlignmentSourceKind.PiTable };
            foreach (var e in elements)
            {
                s.PiElements.Add(new AlignmentPiInput
                {
                    P = e.P,
                    Radius = e.Radius,
                    SpiralIn = e.SpiralIn,
                    SpiralOut = e.SpiralOut,
                    Tag = e.Tag,
                });
            }
            return s;
        }

        private static bool LayerExists(Transaction tr, Database db, string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return false;
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            return lt.Has(layerName);
        }
    }
}
