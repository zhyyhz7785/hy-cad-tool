using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadTactilePaving</c> — 在指定交叉口自动布置盲道（GB 50763 §3.3）。
    ///
    /// <para><b>交互</b></para>
    /// <list type="number">
    /// <item>拾取目标交叉口的任意图元（转角弧 / 已有坡道 / 已有盲道），经 HY_ROAD Xdata ID 定位 Intersection；</item>
    /// <item>输入提示盲道宽度（默认 <see cref="TactilePaving.DefaultStopWidth"/>）；</item>
    /// <item>输入行进盲道宽度（默认 <see cref="TactilePaving.DefaultAdvanceWidth"/>）；</item>
    /// <item>输入行进盲道延伸长度（默认 <see cref="TactilePavingDesigner.DefaultAdvanceLengthFromApproach"/>）。</item>
    /// </list>
    ///
    /// <para><b>前置条件</b></para>
    /// 若目标 <see cref="Intersection.CurbRamps"/> 为空，本命令会自动先用默认参数调
    /// <see cref="CurbRampDesigner.LayoutRampsOnCornerArcs"/> 布置一遍坡道 —— 保证提示盲道有落点。
    /// 不想要默认坡道时，请先在 <c>hyRoadCurbRamp</c> 中定制后再执行本命令。
    /// </summary>
    public sealed class RoadTactilePavingCommand
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

            double stopW = AskDouble(ed, $"\n[道路] 提示盲道宽度 (默认 {TactilePaving.DefaultStopWidth:F2} m)：", TactilePaving.DefaultStopWidth);
            double advW = AskDouble(ed, $"\n[道路] 行进盲道宽度 (默认 {TactilePaving.DefaultAdvanceWidth:F2} m)：", TactilePaving.DefaultAdvanceWidth);
            double advL = AskDouble(
                ed,
                $"\n[道路] 行进盲道沿 Leg 延伸长度 (默认 {TactilePavingDesigner.DefaultAdvanceLengthFromApproach:F1} m)：",
                TactilePavingDesigner.DefaultAdvanceLengthFromApproach);

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

                target = RoadCurbRampCommand.ResolveIntersection(tr, per.ObjectId, design);
                if (target == null)
                {
                    ed.WriteMessage("\n[道路] 未找到所属交叉口。");
                    return;
                }

                if (target.CurbRamps == null || target.CurbRamps.Count == 0)
                {
                    CurbRampDesigner.LayoutRampsOnCornerArcs(target);
                    ed.WriteMessage("\n[道路] 未检测到坡道，按默认参数自动布置一轮（见 hyRoadCurbRamp 可自定义）。");
                }

                TactilePavingDesigner.LayoutTactilePaving(target, stopW, advW, advL);

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
            foreach (var p in target.TactilePavings)
            {
                var r = AccessibilityCodeChecker.CheckTactilePavingWidth(p);
                if (!r.Pass) { ed.WriteMessage($"\n[道路][警告] {r.Message}"); warn++; }
            }

            ed.WriteMessage(
                $"\n[道路] 盲道 {pavingCount} 条（坡道 {rampCount} 条）已布置。"
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
    }
}
