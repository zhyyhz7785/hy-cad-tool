using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;
using HyCADTool.Refactored.Infrastructure.Configuration;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// <c>hyRoadAlnPlan</c>：按路线 <see cref="Alignment.CrossSectionAssignments"/> 分段扫掠出平面图；
    /// 无区段时回退为首个 <see cref="Template"/>（与旧 hyRoadPlan 一致）。
    /// </summary>
    public sealed class RoadAlignmentPlanCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var alnSvc = ServiceLocator.Resolve<RoadAlignmentService>();
            var drawSvc = ServiceLocator.Resolve<RoadPlanDrawService>();

            var peo = new PromptEntityOptions("\n[道路] 拾取 Alignment Polyline：");
            peo.SetRejectMessage("\n[道路] 只允许 Polyline。");
            peo.AddAllowedClass(typeof(Polyline), false);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            Alignment alignment = null;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                alnSvc.RebindForDocument(doc.Name, tr, db);
                var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                var kind = HyRoadXdata.ReadKind(tr, ent);
                if (!string.Equals(kind, "Alignment", StringComparison.Ordinal))
                {
                    ed.WriteMessage("\n[道路] 拾取的图元不是 Alignment。");
                    return;
                }
                var id = HyRoadXdata.ReadId(tr, ent);
                if (registry.TryGet(doc.Name, out var d))
                    alignment = d.Alignments.FirstOrDefault(a => a.Id == id);
                tr.Commit();
            }

            if (alignment == null)
            {
                ed.WriteMessage("\n[道路] 未在 Domain 找到对应 Alignment。");
                return;
            }

            if (!registry.TryGet(doc.Name, out var design) || design.Templates.Count == 0)
            {
                ed.WriteMessage("\n[道路] 当前 Design 无 Template，请先 hyRoadCs 建立横断面模板。");
                return;
            }

            var mode = RoadPlanCommand.PromptMode(ed);

            CorridorPlanPolylines plan;
            var sweep = new CorridorSweepService();
            try
            {
                if (alignment.CrossSectionAssignments != null
                    && alignment.CrossSectionAssignments.Count > 0)
                {
                    var sw = sweep.SweepWithAssignments(
                        alignment,
                        design.Templates,
                        alignment.CrossSectionAssignments,
                        5.0);
                    plan = sw.Plan;
                    if (!sw.UsedBandDividers)
                    {
                        ed.WriteMessage(
                            "\n[道路] 提示：各桩号区段条带数不一致，本次仅出左右红线+中心线（无板块分界）。");
                    }
                }
                else
                {
                    ed.WriteMessage("\n[道路] 未配置桩号区段，使用首个模板扫掠（可 hyRoadAlnAssign 分段挂模板）。");
                    var tpl = design.Templates[0];
                    var layout = CrossSectionLayoutBuilder.FromTemplate(tpl);
                    if (layout == null)
                    {
                        ed.WriteMessage($"\n[道路] Template[{tpl.Name}] 无法反求 CrossSectionLayout，无法扫掠。");
                        return;
                    }
                    plan = sweep.Sweep(alignment, layout, 5.0);
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 扫掠失败：{ex.Message}");
                return;
            }

            int erased, created;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                erased = drawSvc.Clear(tr, db, alignment.Id);
                created = drawSvc.Draw(tr, db, plan, mode);
                tr.Commit();
            }

            ed.WriteMessage(
                $"\n[道路] 平面扫掠完成：擦除 {erased} / 生成 {created}   模式: {mode}   "
                + $"左红线顶点 {plan.LeftRedLine?.VertexCount ?? 0}   右红线顶点 {plan.RightRedLine?.VertexCount ?? 0}   "
                + $"左分界 {plan.LeftBandDividers.Count} / 右分界 {plan.RightBandDividers.Count}");
        }
    }
}
