using System;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadPlan</c>（M10.3）：兼容入口，与 <c>hyRoadAlnPlan</c> 相同实现（分段模板见 <c>hyRoadAlnAssign</c>）。
    /// </summary>
    public sealed class RoadPlanCommand
    {
        public void Execute() => new RoadAlignmentPlanCommand().Execute();

        /// <summary>出图模式（与 <see cref="RoadAlignmentPlanCommand"/> 共享）。</summary>
        internal static CrossSectionDrawMode PromptMode(Editor ed)
        {
            var opts = new PromptKeywordOptions("\n[道路] 出图模式 [Single(S) / Full(F)]: ")
            {
                AllowNone = true,
            };
            opts.Keywords.Add("Single");
            opts.Keywords.Add("Full");
            opts.Keywords.Default = "Full";
            var r = ed.GetKeywords(opts);
            if (r.Status != PromptStatus.OK || r.StringResult == "Full")
                return CrossSectionDrawMode.WithStructureThickness;
            return CrossSectionDrawMode.SingleLine;
        }
    }

    /// <summary>
    /// <c>hyRoadPlanRebuild</c>：按当前 Domain 数据幂等重建所有平面扫掠图形。
    /// </summary>
    public sealed class RoadPlanRebuildCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var drawSvc = ServiceLocator.Resolve<RoadPlanDrawService>();

            if (!registry.TryGet(doc.Name, out var design) || design == null || design.Alignments.Count == 0)
            {
                ed.WriteMessage("\n[道路] 无可重建的 Alignment。");
                return;
            }

            if (design.Templates.Count == 0)
            {
                ed.WriteMessage("\n[道路] 无可用 Template，请先 hyRoadCs 建立模板。");
                return;
            }

            var tpl0 = design.Templates[0];
            var layout0 = CrossSectionLayoutBuilder.FromTemplate(tpl0);
            if (layout0 == null)
            {
                ed.WriteMessage("\n[道路] Template 反解失败。");
                return;
            }

            var sweep = new CorridorSweepService();
            int totalErased = 0, totalCreated = 0;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var a in design.Alignments)
                {
                    if (a?.Centerline == null || a.Centerline.VertexCount < 2) continue;
                    try
                    {
                        CorridorPlanPolylines plan;
                        if (a.CrossSectionAssignments != null && a.CrossSectionAssignments.Count > 0)
                        {
                            var sw = sweep.SweepWithAssignments(
                                a, design.Templates, a.CrossSectionAssignments, 5.0);
                            plan = sw.Plan;
                        }
                        else
                        {
                            plan = sweep.Sweep(a, layout0);
                        }
                        totalErased += drawSvc.Clear(tr, db, a.Id);
                        totalCreated += drawSvc.Draw(tr, db, plan, CrossSectionDrawMode.WithStructureThickness);
                    }
                    catch (Exception ex)
                    {
                        ed.WriteMessage($"\n[道路] Alignment[{a.Name}] 扫掠失败：{ex.Message}");
                    }
                }
                tr.Commit();
            }

            ed.WriteMessage($"\n[道路] 全部刷新完成：擦除 {totalErased} / 生成 {totalCreated}");
        }
    }
}
