using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.Shared.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadCurbRamp</c> — 自动在指定交叉口的所有 CornerArc 上布置缘石坡道（GB 50763 §3.2）。
    ///
    /// <para><b>交互</b></para>
    /// <list type="number">
    /// <item>拾取任意一个属于目标交叉口的图元（转角弧 / 已有 Ramp / 已有盲道 都可），经 HY_ROAD Xdata ID 定位 Intersection；</item>
    /// <item>选择坡道类型（单面 / 三面 / 扇形，默认单面）；</item>
    /// <item>输入坡道宽度 W / 深度 D / 坡度（默认 1.50 m / 1.80 m / 1:12）。</item>
    /// </list>
    ///
    /// <para><b>内部流程</b></para>
    /// <list type="number">
    /// <item>调 <see cref="CurbRampDesigner.LayoutRampsOnCornerArcs"/> 生成 <see cref="CurbRamp"/> 列表；</item>
    /// <item>调 <see cref="RoadAccessibilityService.RebuildAccessibility"/> 幂等重画（含盲道）；</item>
    /// <item>调 <see cref="AccessibilityCodeChecker"/> 校核宽度 / 坡度；</item>
    /// <item>调 <c>RoadJsonExportService.SaveForDocument</c> 落盘。</item>
    /// </list>
    /// </summary>
    public sealed class RoadCurbRampCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var peo = new PromptEntityOptions(
                "\n[道路] 拾取目标交叉口的任意图元（转角弧 / 已有坡道 / 已有盲道）：")
            {
                AllowNone = false,
            };
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消。"); return; }

            var kindOpt = new PromptKeywordOptions(
                "\n[道路] 坡道类型 [单面(S)/三面(T)/扇形(F)]：")
            {
                AllowNone = true,
            };
            kindOpt.Keywords.Add("S");
            kindOpt.Keywords.Add("T");
            kindOpt.Keywords.Add("F");
            kindOpt.Keywords.Default = "S";
            var kw = ed.GetKeywords(kindOpt);
            CurbRampKind kind = CurbRampKind.SingleFace;
            if (kw.Status == PromptStatus.OK)
            {
                switch (kw.StringResult)
                {
                    case "T": kind = CurbRampKind.ThreeFace; break;
                    case "F": kind = CurbRampKind.Fan; break;
                    default: kind = CurbRampKind.SingleFace; break;
                }
            }

            double width = AskDouble(ed, $"\n[道路] 坡道宽度 W (默认 {CurbRamp.DefaultWidth:F2} m)：", CurbRamp.DefaultWidth);
            double depth = AskDouble(ed, $"\n[道路] 坡道深度 D (默认 {CurbRamp.DefaultDepth:F2} m)：", CurbRamp.DefaultDepth);
            double slope = AskDouble(ed, $"\n[道路] 坡度（如 1:12 输入 0.0833，默认 {CurbRamp.DefaultSlope:F4}）：", CurbRamp.DefaultSlope);

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var alnSvc = ServiceLocator.Resolve<RoadAlignmentService>();
            var accSvc = ServiceLocator.Resolve<RoadAccessibilityService>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();

            Intersection target;
            int rampCount, pavingCount;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                alnSvc.RebindForDocument(doc.Name, tr, db);
                if (!registry.TryGet(doc.Name, out var design))
                {
                    ed.WriteMessage("\n[道路] 当前 DWG 没有道路设计数据。");
                    return;
                }

                target = ResolveIntersection(tr, per.ObjectId, design);
                if (target == null)
                {
                    ed.WriteMessage("\n[道路] 未找到所属交叉口（请拾取带 HY_ROAD KIND∈{Intersection,CurbRamp,TactilePaving} 的图元）。");
                    return;
                }

                CurbRampDesigner.LayoutRampsOnCornerArcs(target, kind, width, depth, slope);
                var (ramps, pavings) = accSvc.RebuildAccessibility(
                    tr, db, target, HyRoadLayers.CurbRampLayer, HyRoadLayers.TactilePavingLayer);
                rampCount = ramps.Count;
                pavingCount = pavings.Count;

                tr.Commit();
            }

            try
            {
                if (registry.TryGet(doc.Name, out var designForSave))
                    exporter.SaveForDocument(designForSave, doc.Name);
            }
            catch (Exception ex) { ed.WriteMessage($"\n[道路][警告] .roaddesign.json 写盘失败：{ex.Message}"); }

            int warn = 0;
            foreach (var r in target.CurbRamps)
            {
                var cw = AccessibilityCodeChecker.CheckCurbRampWidth(r);
                var cs = AccessibilityCodeChecker.CheckCurbRampSlope(r);
                if (!cw.Pass) { ed.WriteMessage($"\n[道路][警告] {cw.Message}"); warn++; }
                if (!cs.Pass) { ed.WriteMessage($"\n[道路][警告] {cs.Message}"); warn++; }
            }

            ed.WriteMessage(
                $"\n[道路] 缘石坡道 {rampCount} 条 + 关联盲道 {pavingCount} 条已布置。"
                + (warn > 0 ? $" 规范警告 {warn} 条。" : " GB 50763 校核全部通过。"));
        }

        private static double AskDouble(Editor ed, string prompt, double defaultValue)
        {
            var opt = new PromptDoubleOptions(prompt)
            {
                DefaultValue = defaultValue,
                UseDefaultValue = true,
                AllowNone = true,
                AllowNegative = false,
                AllowZero = false,
            };
            var r = ed.GetDouble(opt);
            return r.Status == PromptStatus.OK ? r.Value : defaultValue;
        }

        /// <summary>
        /// 给定拾取到的任意 HY_ROAD 图元，按其 Xdata ID 反查 <see cref="Intersection"/>。
        /// 匹配的 KIND ∈ {Intersection, CurbRamp, TactilePaving}；三者 ID 共用 <see cref="Intersection.Id"/>。
        /// </summary>
        internal static Intersection ResolveIntersection(
            Transaction tr, ObjectId id, RoadDesign design)
        {
            var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
            if (ent == null) return null;

            var kind = HyRoadXdata.ReadKind(tr, ent);
            if (!string.Equals(kind, RoadIntersectionService.IntersectionKind, StringComparison.Ordinal)
                && !string.Equals(kind, RoadAccessibilityService.CurbRampKind, StringComparison.Ordinal)
                && !string.Equals(kind, RoadAccessibilityService.TactilePavingKind, StringComparison.Ordinal))
                return null;

            var guid = HyRoadXdata.ReadId(tr, ent);
            if (guid == Guid.Empty) return null;

            return design.Intersections.FirstOrDefault(x => x.Id == guid);
        }
    }
}
