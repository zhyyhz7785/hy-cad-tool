using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Features.Road.Events;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Shared.AutoCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.Shared.AutoCAD.Services.Road;

namespace HyCADTool.Features.Road.PlanAlignment.Services
{
    /// <summary>
    /// 「提交 UserPicked 草稿为正式平面线位」服务。
    ///
    /// 职责：
    /// - 在固定图层 <see cref="HyRoadLayers.AlignmentLayer"/>（<c>05_hy_道路_平面线位</c>）新建一条 Polyline，
    ///   写 HY_ROAD Xdata（KIND=Alignment，ID=alignment.Id）；
    /// - 擦除 ModelSpace 中挂 KIND=<see cref="RoadAlignmentUserPickPreviewService.KindAlignmentPreview"/>、
    ///   ID=alignment.Id 的所有预览实体；
    /// - 把 <see cref="AlignmentSource.Kind"/> 升级为 <see cref="AlignmentSourceKind.PiTable"/>，
    ///   让后续 PI 编辑类命令（hyRoadAlnEditPi / hyRoadAlnInsertPi / hyRoadAlnReverse）直接可用；
    /// - 发布 <see cref="AlignmentChangedEvent"/>（<see cref="RoadChangeKind.Updated"/>）。
    ///
    /// 幂等策略：
    /// - 若 Alignment 已经有 HY_ROAD Polyline 在场（Source.Kind 早就是 PiTable），仅擦预览并重发事件；
    /// - 允许用户多次点「提交」：不重复新建 Polyline。
    /// </summary>
    public sealed class RoadAlignmentCommitService
    {
        private readonly RoadDesignRegistry _registry;
        private readonly IRoadEventBus _eventBus;

        public RoadAlignmentCommitService(RoadDesignRegistry registry, IRoadEventBus eventBus)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public sealed class CommitResult
        {
            public bool Success;
            public bool CreatedNewPolyline;
            public int ErasedPreviewCount;
            public string Message;
        }

        /// <summary>
        /// 在 <paramref name="doc"/> 的活动事务内提交 Alignment。本方法自行 <c>LockDocument</c> + 开事务 + Commit。
        /// </summary>
        public CommitResult Commit(Document doc, Guid alignmentId)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            var result = new CommitResult();

            if (alignmentId == Guid.Empty)
            {
                result.Message = "AlignmentId 为空。";
                return result;
            }

            if (!_registry.TryGet(doc.Name, out var design))
            {
                result.Message = "当前 DWG 无道路设计数据。";
                return result;
            }

            Alignment alignment = null;
            foreach (var a in design.Alignments)
            {
                if (a.Id == alignmentId) { alignment = a; break; }
            }
            if (alignment == null)
            {
                result.Message = $"Domain 里找不到 AlignmentId={alignmentId:N}。";
                return result;
            }

            if (alignment.Centerline == null || alignment.Centerline.VertexCount < 2)
            {
                result.Message = "Alignment 中心线为空（顶点 < 2）。";
                return result;
            }

            var db = doc.Database;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                // 1) 擦除 ModelSpace 里同 AlignmentId 的 AlignmentPreview 实体
                result.ErasedPreviewCount = RoadAlignmentUserPickPreviewService
                    .ErasePreviewsForAlignment(tr, db, alignment.Id);

                // 2) 若已存在 HY_ROAD Alignment Polyline（多次提交 / 历史遗留），跳过新建
                bool hasExistingFormal = HasFormalAlignmentPolyline(tr, db, alignment.Id);
                if (!hasExistingFormal)
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    var poly = RoadGeometryBridge.ToAutoCadPolyline(alignment.Centerline);
                    if (LayerExists(tr, db, HyRoadLayers.AlignmentLayer))
                        poly.Layer = HyRoadLayers.AlignmentLayer;

                    ms.AppendEntity(poly);
                    tr.AddNewlyCreatedDBObject(poly, true);
                    HyRoadXdata.Write(tr, db, poly, alignment.Id, HyRoadXdata.KindAlignment, SchemaVersion.Current);
                    result.CreatedNewPolyline = true;
                }

                tr.Commit();
            }

            // 3) 升级 Source.Kind：提交后视为 PiTable（即便 PI 表来自 UserPicked 折线反推）
            if (alignment.Source != null && alignment.Source.Kind == AlignmentSourceKind.UserPicked)
            {
                alignment.Source.Kind = AlignmentSourceKind.PiTable;
            }

            design.LastModifiedUtc = DateTime.UtcNow;
            _eventBus.Publish(new AlignmentChangedEvent(design.Id, alignment.Id, RoadChangeKind.Updated));

            result.Success = true;
            result.Message = result.CreatedNewPolyline
                ? $"已提交到图层「{HyRoadLayers.AlignmentLayer}」，擦除预览 {result.ErasedPreviewCount} 条。"
                : $"Alignment 已有正式 Polyline；擦除预览 {result.ErasedPreviewCount} 条。";
            return result;
        }

        private static bool HasFormalAlignmentPolyline(Transaction tr, Database db, Guid alignmentId)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                if (!(ent is Polyline)) continue;
                var k = HyRoadXdata.ReadKind(tr, ent);
                if (!string.Equals(k, HyRoadXdata.KindAlignment, StringComparison.Ordinal)) continue;
                var gid = HyRoadXdata.ReadId(tr, ent);
                if (gid == alignmentId) return true;
            }
            return false;
        }

        private static bool LayerExists(Transaction tr, Database db, string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return false;
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            return lt.Has(layerName);
        }
    }
}
