using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

using HyCADTool.Features.Road.PlanAlignment.Services;
using HyCADTool.Features.Road.CrossSection.Domain;
using HyCADTool.Features.Road.PlanAlignment.Commands;
using HyCADTool.Features.Road.CrossSection.Commands;
namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadRepaintAll</c>（M6.5）：按当前 Registry 中的 <see cref="RoadDesign"/> 聚合根，
    /// 幂等重绘 AutoCAD 图形（还原点恢复后触发；也可任意时刻手工执行以修复 DWG 与 Domain 的偏差）。
    ///
    /// <para><b>现阶段覆盖范围</b></para>
    /// <list type="bullet">
    ///   <item>✅ 平面线位 Polyline 中心线（走 <see cref="RoadAlignmentService.RebuildCenterline"/>）</item>
    ///   <item>⏸ Profile / Template / Intersection / CornerArc / Crosswalk / CurbRamp / TactilePaving /
    ///     StopLine / GeometryPointLabel：各自已有幂等重绘命令，本命令仅做中心线刷新；其余请继续使用
    ///     <c>hyRoadAlnStation</c> / <c>hyRoadAlnGeomPt</c> / <c>hyRoadIntersection</c> 等命令按需刷新。</item>
    /// </list>
    ///
    /// <para>完整覆盖将随 M7 / M9 / M10 逐步扩展（每个里程碑新增的实体都挂到本命令的"待重绘 KIND"列表）。</para>
    /// </summary>
    public sealed class RoadRepaintAllCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var alnSvc = ServiceLocator.Resolve<RoadAlignmentService>();

            if (!registry.TryGet(doc.Name, out var design) || design == null)
            {
                ed.WriteMessage("\n[道路] 当前文档没有道路数据，无需刷新。");
                return;
            }

            int alnTouched = 0;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                alnSvc.RebindForDocument(doc.Name, tr, db);

                foreach (var a in design.Alignments.ToArray())
                {
                    if (a == null || a.Centerline == null) continue;
                    // 按 DWG Xdata 定位同一 AlignmentId 的 Polyline，并用 Domain 里的 Centerline 覆盖。
                    if (alnSvc.RebuildCenterline(doc.Name, tr, db, a.Id, a.Centerline))
                        alnTouched++;
                }

                tr.Commit();
            }

            ed.WriteMessage(
                $"\n[道路] 刷新完成：平面线位 {alnTouched} 条已按 Domain 数据重绘。"
                + "\n[道路] 其余对象请按需运行："
                + "\n        - hyRoadAlnStation（桩号）、hyRoadAlnGeomPt（几何点）、hyRoadAlnOffset（偏移线）"
                + "\n        - hyRoadIntersection（交叉口几何） / hyRoadCurbRamp / hyRoadTactilePaving"
                + "\n        - hyRoadCs（横断面出图）");
            LogXdataScan(ed, db);
        }

        /// <summary>
        /// 简要统计当前 DWG 中各 KIND 的 Xdata 实体数量，帮助用户对照 Domain 数据差异。
        /// </summary>
        private static void LogXdataScan(Autodesk.AutoCAD.EditorInput.Editor ed, Database db)
        {
            var stats = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);
            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    foreach (ObjectId oid in ms)
                    {
                        var ent = tr.GetObject(oid, OpenMode.ForRead);
                        var kind = HyRoadXdata.ReadKind(tr, ent);
                        if (string.IsNullOrEmpty(kind)) continue;
                        if (!stats.ContainsKey(kind)) stats[kind] = 0;
                        stats[kind]++;
                    }
                    tr.Commit();
                }
            }
            catch { /* 统计失败不致命 */ }

            if (stats.Count == 0)
            {
                ed.WriteMessage("\n[道路] DWG 中未检测到 HY_ROAD 实体。");
                return;
            }
            ed.WriteMessage("\n[道路] DWG Xdata 统计（KIND × 数量）：");
            foreach (var kv in stats.OrderBy(k => k.Key, StringComparer.Ordinal))
                ed.WriteMessage($"\n  · {kv.Key}: {kv.Value}");
        }
    }
}
