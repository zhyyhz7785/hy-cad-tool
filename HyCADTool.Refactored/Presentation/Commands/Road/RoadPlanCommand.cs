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
    /// <c>hyRoadPlan</c>（M10.3）：平面分段扫掠出图。
    ///
    /// <para><b>工作流</b></para>
    /// <list type="number">
    ///   <item>拾取一条已挂 HY_ROAD 的 Alignment Polyline。</item>
    ///   <item>选择 Template（当 RoadDesign 有多个 Template 时）。</item>
    ///   <item>命令行询问绘图模式（单线 / 带结构厚度）。</item>
    ///   <item>按 <c>SamplingStep</c>（默认 5m）扫掠 Alignment，展开出 CorridorPlanPolylines。</item>
    ///   <item>幂等重建：Clear 旧 + Draw 新 → 挂 HY_ROAD Xdata（KIND=CorridorPlan，ID=Alignment.Id）。</item>
    /// </list>
    /// </summary>
    public sealed class RoadPlanCommand
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

            // 1. 拾取 Alignment
            var peo = new PromptEntityOptions("\n[道路] 拾取 Alignment Polyline：");
            peo.SetRejectMessage("\n[道路] 只允许 Polyline。");
            peo.AddAllowedClass(typeof(Polyline), exactMatch: false);
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
                {
                    alignment = d.Alignments.FirstOrDefault(a => a.Id == id);
                }
                tr.Commit();
            }

            if (alignment == null)
            {
                ed.WriteMessage("\n[道路] 未在 Domain 找到对应 Alignment。");
                return;
            }

            // 2. 拿 Template（MVP：使用第一个）
            if (!registry.TryGet(doc.Name, out var design) || design.Templates.Count == 0)
            {
                ed.WriteMessage("\n[道路] 当前 Design 无 Template，请先 hyRoadCs 建立横断面模板。");
                return;
            }
            var tpl = design.Templates[0];
            var layout = CrossSectionLayoutBuilder.FromTemplate(tpl);
            if (layout == null)
            {
                ed.WriteMessage($"\n[道路] Template[{tpl.Name}] 无法反求 CrossSectionLayout，无法扫掠。");
                return;
            }

            // 3. 选模式
            var mode = PromptMode(ed);

            // 4. 扫掠
            CorridorPlanPolylines plan;
            try
            {
                var sweep = new CorridorSweepService();
                plan = sweep.Sweep(alignment, layout, samplingStepM: 5.0);
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 扫掠失败：{ex.Message}");
                return;
            }

            // 5. Clear 旧 + Draw 新
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
                + $"左分界线 {plan.LeftBandDividers.Count} / 右分界线 {plan.RightBandDividers.Count}");
        }

        private static CrossSectionDrawMode PromptMode(Editor ed)
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

            var tpl = design.Templates[0];
            var layout = CrossSectionLayoutBuilder.FromTemplate(tpl);
            if (layout == null)
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
                        var plan = sweep.Sweep(a, layout);
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
