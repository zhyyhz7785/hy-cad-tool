using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// <c>hyRoadIntersectionCrosswalk</c> v1.1 —— 基于 <see cref="Intersection"/> 聚合的人行横道命令。
    ///
    /// <para><b>替代的旧命令</b></para>
    /// <c>hyRoad</c> / <c>DrawCrosswalkCommand</c> + <c>Infrastructure.AutoCAD.Services.CrosswalkService</c>：
    /// 旧命令需用户选若干 Line + Arc，反推 RoadArm 再逐方向画；本命令直接在已有 Intersection 聚合上工作。
    ///
    /// <para><b>交互流程</b></para>
    /// <list type="number">
    /// <item>拾取目标交叉口任一实体（转角弧 / 坡道 / 盲道 / 路缘线）；</item>
    /// <item>命令自动为每条 Leg 生成 <see cref="Crosswalk"/>（base = 相邻 CornerArc 切点）；</item>
    /// <item>参数默认值：
    ///   <list type="bullet">
    ///   <item>GapWidth（路缘 → L2）取 <see cref="SettingsPanelViewModel.RoadGapWidth"/> 或 <see cref="Crosswalk.DefaultGapWidth"/>；</item>
    ///   <item>Width（L2 → L3）取 <see cref="SettingsPanelViewModel.RoadCrosswalkWidth"/> 或 <see cref="Crosswalk.DefaultWidth"/>；</item>
    ///   <item>StopLineDistance（L3 → L4）取 <see cref="SettingsPanelViewModel.RoadStopLineDistance"/>；</item>
    ///   <item>StripeSpacing 取 <see cref="SettingsPanelViewModel.RoadStripeSpacing"/>。</item>
    ///   </list>
    /// </item>
    /// <item>交互再次允许用户覆盖上述 4 个值；</item>
    /// <item><see cref="RoadCrosswalkService.RebuildCrosswalks"/> 幂等清理 + 重绘；</item>
    /// <item>JSON 落盘。</item>
    /// </list>
    ///
    /// <para><b>Domain / Infrastructure 分层</b></para>
    /// Domain 的 <see cref="CrosswalkDesigner"/> 负责纯几何（可单测），本命令层只做交互 + 事务 + 持久化。
    /// </summary>
    public sealed class RoadIntersectionCrosswalkCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var peo = new PromptEntityOptions(
                "\n[道路] 拾取目标交叉口的任意实体（转角弧 / 坡道 / 盲道 / 路缘线）：")
            {
                AllowNone = false,
            };
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消。"); return; }

            var vm = SettingsPanelViewModel.Current;
            double gap = AskDouble(ed, "[道路] 路缘到横道的空挡 (m)",
                vm?.RoadGapWidth ?? Crosswalk.DefaultGapWidth);
            double width = AskDouble(ed, "[道路] 横道宽度 (m)",
                vm?.RoadCrosswalkWidth ?? Crosswalk.DefaultWidth);
            double stop = AskDouble(ed, "[道路] 横道到停止线的距离 (m)",
                vm?.RoadStopLineDistance ?? Crosswalk.DefaultStopLineDistance);
            double spacing = AskDouble(ed, "[道路] 条纹中心距 (m)",
                vm?.RoadStripeSpacing ?? Crosswalk.DefaultStripeSpacing);

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var alnSvc = ServiceLocator.Resolve<RoadAlignmentService>();
            var cwSvc = ServiceLocator.Resolve<RoadCrosswalkService>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                alnSvc.RebindForDocument(doc.Name, tr, db);
                if (!registry.TryGet(doc.Name, out var design))
                {
                    ed.WriteMessage("\n[道路] 当前 DWG 没有道路设计数据。");
                    return;
                }

                var target = RoadCurbRampCommand.ResolveIntersection(tr, per.ObjectId, design);
                if (target == null)
                {
                    ed.WriteMessage("\n[道路] 未找到所属交叉口。");
                    return;
                }
                if (target.Legs.Count == 0)
                {
                    ed.WriteMessage("\n[道路] 目标交叉口没有 Leg，无法布置人行横道。");
                    return;
                }

                target.Crosswalks.Clear();
                var list = CrosswalkDesigner.LayoutForAllLegs(target, gap, width, stop, spacing);
                foreach (var cw in list) target.Crosswalks.Add(cw);
                target.LastModifiedUtc = DateTime.UtcNow;

                var (stripeIds, stopIds) = cwSvc.RebuildCrosswalks(
                    tr, db, target, HyRoadLayers.CrosswalkLayer, HyRoadLayers.StopLineLayer);

                tr.Commit();

                try
                {
                    if (registry.TryGet(doc.Name, out var designForSave))
                        exporter.SaveForDocument(designForSave, doc.Name);
                }
                catch (Exception ex) { ed.WriteMessage($"\n[道路][警告] .roaddesign.json 写盘失败：{ex.Message}"); }

                ed.WriteMessage(
                    $"\n[道路] 已为 {target.Legs.Count} 条 Leg 布置人行横道：" +
                    $"条纹 {stripeIds.Count} 条 / 停止线 {stopIds.Count} 条（Gap={gap:F2} W={width:F2} Stop={stop:F2} Spacing={spacing:F2}）。");
            }
        }

        private static double AskDouble(Editor ed, string prompt, double defaultValue)
        {
            var opt = new PromptDoubleOptions($"\n{prompt} (默认 {defaultValue:F2})：")
            {
                DefaultValue = defaultValue,
                UseDefaultValue = true,
                AllowNone = true,
                AllowNegative = false,
                AllowZero = true,
            };
            var r = ed.GetDouble(opt);
            return r.Status == PromptStatus.OK ? r.Value : defaultValue;
        }
    }
}
