using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Shared.AutoCAD.Xdata;

namespace HyCADTool.Shared.AutoCAD.Services.Road
{
    /// <summary>
    /// 「删除线位」服务：删除当前 Alignment 的正式线位 / 原线档案 / 用户快照 / 标注，并同步从 RoadDesign + JSON 移除。
    /// 供路线工作台顶栏「删除」按钮使用。
    /// </summary>
    public sealed class RoadAlignmentDeleteService
    {
        private readonly RoadDesignRegistry _registry;
        private readonly RoadJsonExportService _jsonExport;
        private readonly RoadAlignmentService _alignmentSvc;

        public RoadAlignmentDeleteService(
            RoadDesignRegistry registry,
            RoadJsonExportService jsonExport,
            RoadAlignmentService alignmentSvc)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _jsonExport = jsonExport ?? throw new ArgumentNullException(nameof(jsonExport));
            _alignmentSvc = alignmentSvc ?? throw new ArgumentNullException(nameof(alignmentSvc));
        }

        public sealed class DeleteResult
        {
            public bool Success;
            public int ErasedFormalCount;
            public int ErasedRawCount;
            public int ErasedDesignPreviewCount;
            public int ErasedLivePreviewCount;
            public int ErasedStationLabelCount;
            public int ErasedGeometryPointLabelCount;
            public string JsonPath;
            public string Message;
        }

        public DeleteResult Delete(Document doc, Alignment alignment)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            var r = new DeleteResult();
            if (alignment == null)
            {
                r.Message = "Alignment 为空。";
                return r;
            }

            if (!_registry.TryGet(doc.Name, out var design)
                || design == null
                || design.Alignments.Find(a => a.Id == alignment.Id) == null)
            {
                r.Message = "当前线位不在 RoadDesign 中，无法删除。";
                return r;
            }

            var db = doc.Database;
            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    _alignmentSvc.RebindForDocument(doc.Name, tr, db);

                    r.ErasedFormalCount = EraseByKindAndId(tr, db, HyRoadXdata.KindAlignment, alignment.Id);

                    using (LayerLockScope.Unlock(tr, db, HyRoadLayers.RawPolylineLayer))
                    {
                        r.ErasedRawCount = EraseByKindAndId(tr, db, HyRoadXdata.KindAlignmentRawPick, alignment.Id);
                        r.ErasedDesignPreviewCount = EraseByKindAndId(tr, db, HyRoadXdata.KindAlignmentDesignPreview, alignment.Id);
                    }

                    r.ErasedLivePreviewCount = EraseByKindAndId(tr, db, HyRoadXdata.KindAlignmentLivePreview, alignment.Id);
                    r.ErasedStationLabelCount = _alignmentSvc.ClearStationLabels(tr, db, alignment.Id);
                    r.ErasedGeometryPointLabelCount = _alignmentSvc.ClearGeometryPointLabels(tr, db, alignment.Id);

                    tr.Commit();
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                r.Message = $"DWG 删除失败：{ex.Message}（ErrorStatus={ex.ErrorStatus}）。";
                return r;
            }
            catch (Exception ex)
            {
                r.Message = "DWG 删除失败：" + ex.Message;
                return r;
            }

            if (!_alignmentSvc.Delete(doc.Name, alignment.Id))
            {
                r.Message = "DWG 已删，但从 RoadDesign 移除失败。";
                return r;
            }

            if (_registry.TryGet(doc.Name, out design) && design != null)
            {
                try
                {
                    if (design.IsEmpty)
                    {
                        var jsonPath = RoadJsonExportService.GetDefaultJsonPath(doc.Name);
                        if (!string.IsNullOrWhiteSpace(jsonPath) && File.Exists(jsonPath))
                        {
                            File.Delete(jsonPath);
                            r.JsonPath = jsonPath;
                        }
                    }
                    else
                    {
                        r.JsonPath = _jsonExport.SaveForDocument(design, doc.Name);
                    }
                }
                catch (Exception ex)
                {
                    r.Message = "JSON 同步失败：" + ex.Message;
                    return r;
                }
            }

            r.Success = true;
            r.Message =
                $"已删除 {alignment.Name}：正式线 {r.ErasedFormalCount} 条，原线 {r.ErasedRawCount} 条，预览残留 {r.ErasedDesignPreviewCount + r.ErasedLivePreviewCount} 条，标注 {r.ErasedStationLabelCount + r.ErasedGeometryPointLabelCount} 条。";
            return r;
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
    }
}
