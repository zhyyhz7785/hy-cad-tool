using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Features.AcadDimension.Domain.Results;
using HyCADTool.Shared.AutoCAD.Xdata;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.AcadDimension.Services
{
    /// <summary>
    /// Phase 8：扫描 ModelSpace 中带 <see cref="NewDdsBindingKeys.ExtensionDictionaryKey"/> 扩展字典的
    /// <see cref="RotatedDimension"/>，按源 Polyline Handle 分组，比对 <see cref="NewDdsDimensionBinding.FeatureSignature"/>：
    ///
    /// - 源对象不存在 → 标 Orphan，不删（留作诊断）；
    /// - 旧 Sig == 当前 Sig → 跳过；
    /// - 否则 → 删除该组所有旧标注，调用 <see cref="NewDdsService.RunInTransaction"/> 在同一事务内重画。
    ///
    /// 单事务原子性：任一阶段抛异常 → 整体 Abort（沿用 06 §7 #9 修法）。
    /// </summary>
    public sealed class NewDdsRefreshService
    {
        private readonly NewDdsService _service;

        public NewDdsRefreshService(NewDdsService service = null)
        {
            _service = service ?? new NewDdsService();
        }

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;
            if (ed == null) return;

            try
            {
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var summary = ExecuteWithinTransaction(doc, tr);

                    ed.WriteMessage(
                        $"\n[nddsR] 扫描标注={summary.ScannedDimensions} 涉及源={summary.UniqueSources} " +
                        $"未变={summary.Unchanged} 已更新={summary.Refreshed} 孤儿={summary.Orphans} 错误={summary.Errors}");
                    foreach (var line in summary.Notes.Take(8))
                        ed.WriteMessage("\n[nddsR] " + line);
                    if (summary.Notes.Count > 8)
                        ed.WriteMessage($"\n[nddsR] ...（另有 {summary.Notes.Count - 8} 条诊断已省略）");

                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[nddsR] 链路异常：{ex.GetType().Name} - {ex.Message}");
            }
        }

        public RefreshSummary ExecuteWithinTransaction(Document doc, Transaction tr)
        {
            var summary = new RefreshSummary();
            var db = doc.Database;
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var grouped = new Dictionary<string, List<DimensionBindingPair>>(StringComparer.OrdinalIgnoreCase);

            foreach (ObjectId id in ms)
            {
                if (id.IsErased) continue;
                var dim = tr.GetObject(id, OpenMode.ForRead) as RotatedDimension;
                if (dim == null) continue;

                var binding = ExtensionDictionaryService.Read<NewDdsDimensionBinding>(
                    tr, dim, NewDdsBindingKeys.ExtensionDictionaryKey);
                if (binding == null || string.IsNullOrEmpty(binding.SourcePolylineHandle))
                    continue;

                summary.ScannedDimensions++;
                if (!grouped.TryGetValue(binding.SourcePolylineHandle, out var bucket))
                {
                    bucket = new List<DimensionBindingPair>();
                    grouped[binding.SourcePolylineHandle] = bucket;
                }
                bucket.Add(new DimensionBindingPair(dim.ObjectId, binding));
            }

            summary.UniqueSources = grouped.Count;

            foreach (var kv in grouped)
            {
                string handleStr = kv.Key;
                var bucket = kv.Value;
                ObjectId sourceId = TryResolveHandle(db, handleStr);

                if (sourceId.IsNull || sourceId.IsErased ||
                    !(tr.GetObject(sourceId, OpenMode.ForRead) is Polyline))
                {
                    summary.Orphans += bucket.Count;
                    summary.Notes.Add($"源 Handle={handleStr} 不存在或已删除 → {bucket.Count} 条标注为孤儿（保留未删）");
                    continue;
                }

                string oldSig = bucket[0].Binding.FeatureSignature;
                bool allSameOldSig = bucket.All(p => string.Equals(p.Binding.FeatureSignature, oldSig, StringComparison.Ordinal));

                string currentSig;
                try
                {
                    var probeRun = ProbeFeatureSignature(tr, sourceId);
                    currentSig = probeRun;
                }
                catch (Exception ex)
                {
                    summary.Errors++;
                    summary.Notes.Add($"源 Handle={handleStr} 计算 Sig 失败：{ex.GetType().Name} - {ex.Message}");
                    continue;
                }

                if (allSameOldSig && string.Equals(oldSig, currentSig, StringComparison.Ordinal))
                {
                    summary.Unchanged += bucket.Count;
                    continue;
                }

                foreach (var pair in bucket)
                {
                    var dimEntity = tr.GetObject(pair.DimensionId, OpenMode.ForWrite) as Entity;
                    dimEntity?.Erase();
                }

                NewDdsRunOutcome run;
                try
                {
                    run = _service.RunInTransaction(doc, tr, sourceId);
                }
                catch (Exception ex)
                {
                    summary.Errors++;
                    summary.Notes.Add($"源 Handle={handleStr} 重生成失败：{ex.GetType().Name} - {ex.Message}");
                    continue;
                }

                if (run == null)
                {
                    summary.Errors++;
                    summary.Notes.Add($"源 Handle={handleStr} 不再是 Polyline，无法重生成（已擦除 {bucket.Count} 条）");
                    continue;
                }

                summary.Refreshed++;
                summary.Notes.Add(
                    $"源 Handle={handleStr} 旧 Sig={Short(oldSig)} → 新 Sig={Short(currentSig)} " +
                    $"擦除 {bucket.Count} → 重写 {run.Written}");
            }

            return summary;
        }

        private static ObjectId TryResolveHandle(Database db, string handleStr)
        {
            if (string.IsNullOrEmpty(handleStr)) return ObjectId.Null;
            if (!long.TryParse(handleStr, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out long handleVal))
                return ObjectId.Null;
            try
            {
                return db.GetObjectId(false, new Handle(handleVal), 0);
            }
            catch
            {
                return ObjectId.Null;
            }
        }

        private string ProbeFeatureSignature(Transaction tr, ObjectId polylineId)
        {
            var pl = (Polyline)tr.GetObject(polylineId, OpenMode.ForRead);
            var config = NewDdsConfigAdapter.FromSettingsPanel();
            var poly2D = AcadPolylineConverter.ToPolyline2D(pl, config.BulgeTessellatePrecision);
            return NewDdsFeatureSignature.Compute(poly2D);
        }

        private static string Short(string sig)
            => string.IsNullOrEmpty(sig) ? "(空)" : (sig.Length <= 12 ? sig : sig.Substring(0, 12) + "...");

        private readonly struct DimensionBindingPair
        {
            public DimensionBindingPair(ObjectId id, NewDdsDimensionBinding binding)
            {
                DimensionId = id;
                Binding = binding;
            }
            public ObjectId DimensionId { get; }
            public NewDdsDimensionBinding Binding { get; }
        }

        public sealed class RefreshSummary
        {
            public int ScannedDimensions { get; set; }
            public int UniqueSources { get; set; }
            public int Unchanged { get; set; }
            public int Refreshed { get; set; }
            public int Orphans { get; set; }
            public int Errors { get; set; }
            public IList<string> Notes { get; } = new List<string>();
        }
    }
}
