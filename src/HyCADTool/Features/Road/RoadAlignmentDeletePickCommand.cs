using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadAlnDeletePick</c> — 按 AutoCAD 多选方式（先选后执行 / 框选 / 窗口选）选择若干条
    /// HY_ROAD 平面线位相关实体（正式线 / 原线 / 预览），
    /// 解析 AlignmentId 后按 <b>路线去重</b> 删除（DWG 实体 + Domain + <c>.roaddesign.json</c>）。
    /// </summary>
    public sealed class RoadAlignmentDeletePickCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            var psr = TryGetSelection(ed);
            if (psr == null || psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
            {
                ed.WriteMessage("\n[道路] 已取消或未选择对象。");
                return;
            }

            var alignmentIds = new HashSet<Guid>();
            int skippedNonRoad = 0;
            int skippedBadId = 0;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject so in psr.Value)
                {
                    if (so == null) continue;
                    var ent = tr.GetObject(so.ObjectId, OpenMode.ForRead);
                    var kind = HyRoadXdata.ReadKind(tr, ent);
                    if (!HyRoadXdata.IsAlignmentKind(kind))
                    {
                        skippedNonRoad++;
                        continue;
                    }

                    var aid = HyRoadXdata.ReadId(tr, ent);
                    if (aid == Guid.Empty)
                    {
                        skippedBadId++;
                        continue;
                    }

                    alignmentIds.Add(aid);
                }

                tr.Commit();
            }

            if (alignmentIds.Count == 0)
            {
                ed.WriteMessage(
                    "\n[道路] 所选对象中没有可删除的 HY_ROAD 平面线位。"
                    + (skippedNonRoad > 0 ? $"（已跳过 {skippedNonRoad} 个非 HY_ROAD 对象）" : "")
                    + (skippedBadId > 0 ? $"（已跳过 {skippedBadId} 个缺少 ID 的对象）" : ""));
                return;
            }

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var deleteSvc = ServiceLocator.Resolve<RoadAlignmentDeleteService>();
            var alnSvc = ServiceLocator.Resolve<RoadAlignmentService>();

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                alnSvc.RebindForDocument(doc.Name, tr, db);
                tr.Commit();
            }

            if (!registry.TryGet(doc.Name, out var design) || design == null)
            {
                ed.WriteMessage("\n[道路] 当前 DWG 没有道路数据。");
                return;
            }

            int ok = 0;
            int fail = 0;
            string lastJson = null;

            foreach (var alignmentId in alignmentIds.OrderBy(g => g.ToString("N")))
            {
                if (!registry.TryGet(doc.Name, out design) || design == null)
                    break;

                var alignment = design.Alignments.FirstOrDefault(a => a.Id == alignmentId);
                if (alignment == null)
                {
                    ed.WriteMessage($"\n[道路] 跳过（已删除或不存在）: {alignmentId:N}");
                    continue;
                }

                var result = deleteSvc.Delete(doc, alignment);
                if (!result.Success)
                {
                    fail++;
                    ed.WriteMessage("\n[道路] 删除失败：" + (result.Message ?? ""));
                    continue;
                }

                ok++;
                if (!string.IsNullOrEmpty(result.JsonPath))
                    lastJson = result.JsonPath;
                ed.WriteMessage("\n[道路] " + result.Message);
            }

            if (skippedNonRoad > 0 || skippedBadId > 0)
                ed.WriteMessage(
                    $"\n[道路] 拾取统计：跳过非 HY_ROAD {skippedNonRoad} 个，缺少 ID {skippedBadId} 个；路线去重后删除 {ok} 条。");
            else if (ok > 1)
                ed.WriteMessage($"\n[道路] 共删除 {ok} 条路线。");

            if (!string.IsNullOrEmpty(lastJson))
                ed.WriteMessage($"\n[道路] JSON: {lastJson}");
        }

        /// <summary>
        /// 优先使用拾取优先（先选对象再执行命令，与 AutoCAD 一致）；否则提示多选框选。
        /// </summary>
        private static PromptSelectionResult TryGetSelection(Editor ed)
        {
            var implied = ed.SelectImplied();
            if (implied.Status == PromptStatus.OK && implied.Value != null && implied.Value.Count > 0)
                return implied;

            var pso = new PromptSelectionOptions
            {
                MessageForAdding = "\n[道路] 选择要删除的 HY_ROAD 平面线位（可多选、框选）：",
                AllowDuplicates = false,
            };

            // 不限制实体类型：与 AutoCAD 框选一致；仅含 HY_ROAD Alignment 类 KIND 的才计入删除。
            return ed.GetSelection(pso);
        }
    }
}
