using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadSave</c>：将当前文档的 <see cref="RoadDesign"/> 立即同步落盘到 <c>.roaddesign.json</c>。
    ///
    /// 设计（v1.1，完全同步，取消 500ms 防抖）：
    /// 1. <strong>只查不新建</strong>：<see cref="RoadDesignRegistry.TryGet"/>；SAVEAS 后 <c>doc.Name</c> 改变时，
    ///    避免 <c>GetOrCreate</c> 在新 key 下凭空建一个空壳 design，然后被 Save 写成空 JSON；
    /// 2. <strong>自动 Rebind</strong>：若当前 key 未命中或只有空壳，开事务扫 DWG Xdata，
    ///    通过 <see cref="RoadAlignmentService.RebindForDocument"/> 把老 key 下的 design 迁到当前 doc.Name；
    /// 3. <strong>空 design 明确拒写</strong>：无内容只打印 Registry 快照，不写文件；
    /// 4. <strong>诊断可见</strong>：失败路径必打 snapshot，帮助用户一眼看出 Registry 当前持有哪些 key；
    /// 5. <strong>同步落盘</strong>：直接调 <see cref="RoadJsonExportService.SaveForDocument"/>，
    ///    无异步 Timer，命令返回前 JSON 已经落盘；失败会抛出（由外层 <c>Run</c> 捕获）。
    /// </summary>
    public sealed class RoadOpenJsonCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
            var svc = ServiceLocator.Resolve<RoadAlignmentService>();

            registry.TryGet(doc.Name, out var design);

            // 当前 key 未命中或只是空壳 → 扫 DWG Xdata，尝试把老 key 下的 design 迁过来
            if (design == null || design.IsEmpty)
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var rebound = svc.RebindForDocument(doc.Name, tr, db);
                    tr.Commit();
                    if (rebound != null && !ReferenceEquals(rebound, design))
                    {
                        design = rebound;
                        ed.WriteMessage(
                            "\n[道路] 已把历史 key 下的 RoadDesign 迁到当前 DWG 名（SAVEAS / 改名场景自修复）。");
                    }
                }
            }

            if (design == null)
            {
                ed.WriteMessage($"\n[道路] 当前文档 {doc.Name} 没有道路数据，未写盘。");
                PrintRegistrySnapshot(ed, registry);
                return;
            }

            if (design.IsEmpty)
            {
                ed.WriteMessage(
                    "\n[道路] 当前 RoadDesign 为空（无 Alignment / Template / Corridor / Node），未写盘。");
                PrintRegistrySnapshot(ed, registry);
                return;
            }

            // v1.2「保存最后的文（件）」：写盘前先扫 DWG，把已无对应 HY_ROAD Polyline 的非 UserPicked Alignment
            // 从 design 里清掉，避免 JSON 留着幽灵线位下一次 Load 又写回去。UserPicked 草稿恒保留。
            int beforeCount = design.Alignments.Count;
            int purged = 0;
            try { purged = exporter.PurgeOrphans(doc, design); }
            catch { /* PurgeOrphans 任何失败都不阻断写盘 */ }
            if (purged > 0)
            {
                ed.WriteMessage(
                    $"\n[道路] 写盘前同步：清理 {purged} 条「DWG 已无对应 Polyline」的孤儿 Alignment "
                    + $"（剩余 {design.Alignments.Count}/{beforeCount}）。");
            }

            var savedTo = exporter.SaveForDocument(design, doc.Name);
            if (savedTo != null)
            {
                ed.WriteMessage($"\n[道路] 已同步落盘：{savedTo}");
            }
            else
            {
                ed.WriteMessage(
                    "\n[道路] 未落盘：DWG 尚未保存（路径无法解析到 .roaddesign.json）。"
                    + "\n       请先 QSAVE / SAVEAS，再次执行 hyRoadSave。");
            }
        }

        private static void PrintRegistrySnapshot(Editor ed, RoadDesignRegistry registry)
        {
            var snap = registry.Snapshot();
            if (snap.Count == 0)
            {
                ed.WriteMessage("\n[道路] Registry 当前为空（未曾拾取过任何多段线）。");
                return;
            }

            ed.WriteMessage($"\n[道路] Registry 快照（{snap.Count} 条，用于诊断 key 漂移）:");
            foreach (var kv in snap)
            {
                var d = kv.Value;
                ed.WriteMessage(
                    $"\n  · key={kv.Key}"
                    + $"  Id={d.Id:N}"
                    + $"  A={d.Alignments.Count}"
                    + $"  T={d.Templates.Count}"
                    + $"  C={d.Corridors.Count}"
                    + $"  N={d.Nodes.Count}"
                    + (d.IsEmpty ? "  (empty)" : string.Empty));
            }
        }
    }
}
