using System;
using System.Linq;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadIntersectionMergeRule</c>（M9.2）：交叉口条带合并规则管理。
    ///
    /// <para>MVP：命令行交互 — 列出 5×5 默认规则 / 当前 overrides，支持添加 / 移除 / 重置。</para>
    /// </summary>
    public sealed class RoadIntersectionMergeRuleCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
            var design = registry.GetOrCreate(doc.Name);

            // 列出当前规则
            ed.WriteMessage("\n[道路] === 默认 5x5 规则（仅展示）===");
            foreach (var r in IntersectionBandMergeRuleTable.DefaultFiveByFive())
            {
                ed.WriteMessage(
                    $"\n  {(TemplateComponentKind)r.SelfKind,-15} x {(TemplateComponentKind)r.OppositeKind,-15} = {r.Strategy}");
            }
            ed.WriteMessage($"\n[道路] === 已有 Overrides ({design.IntersectionBandMergeRules.Count})===");
            foreach (var r in design.IntersectionBandMergeRules)
            {
                ed.WriteMessage(
                    $"\n  {(TemplateComponentKind)r.SelfKind,-15} x {(TemplateComponentKind)r.OppositeKind,-15} = {r.Strategy}  (p={r.SelfPriority})");
            }

            var opts = new PromptKeywordOptions("\n[道路] 操作 [Add / Remove / Reset / Exit]: ")
            {
                AllowNone = true,
            };
            opts.Keywords.Add("Add");
            opts.Keywords.Add("Remove");
            opts.Keywords.Add("Reset");
            opts.Keywords.Add("Exit");
            opts.Keywords.Default = "Exit";
            var r0 = ed.GetKeywords(opts);
            if (r0.Status != PromptStatus.OK) return;

            switch (r0.StringResult)
            {
                case "Add": AddOverride(ed, design); break;
                case "Remove": RemoveOverride(ed, design); break;
                case "Reset":
                    design.IntersectionBandMergeRules.Clear();
                    ed.WriteMessage("\n[道路] 已清空 overrides，恢复默认规则。");
                    break;
                default: return;
            }

            design.LastModifiedUtc = DateTime.UtcNow;
            string saved = exporter.SaveForDocument(design, doc.Name);
            if (saved != null) ed.WriteMessage($"\n[道路] 规则已保存：{saved}");
        }

        private static void AddOverride(Editor ed, RoadDesign design)
        {
            var self = PromptKind(ed, "Self 条带类型");
            if (self == null) return;
            var opp = PromptKind(ed, "Opposite 条带类型");
            if (opp == null) return;
            var strategy = PromptStrategy(ed);
            if (strategy == null) return;

            int selfP = IntersectionBandMergeRuleTable.DefaultPriority.TryGetValue(self.Value, out var p1) ? p1 : 0;

            // 移除已有 override（若有）
            design.IntersectionBandMergeRules.RemoveAll(r =>
                r.SelfKind == (int)self.Value && r.OppositeKind == (int)opp.Value);

            design.IntersectionBandMergeRules.Add(new IntersectionBandMergeRule
            {
                SelfKind = (int)self.Value,
                OppositeKind = (int)opp.Value,
                SelfPriority = selfP,
                Strategy = strategy.Value,
            });
            ed.WriteMessage("\n[道路] Override 已添加。");
        }

        private static void RemoveOverride(Editor ed, RoadDesign design)
        {
            var self = PromptKind(ed, "Self 条带类型");
            if (self == null) return;
            var opp = PromptKind(ed, "Opposite 条带类型");
            if (opp == null) return;

            int removed = design.IntersectionBandMergeRules.RemoveAll(r =>
                r.SelfKind == (int)self.Value && r.OppositeKind == (int)opp.Value);
            ed.WriteMessage($"\n[道路] 已移除 {removed} 条 override。");
        }

        private static TemplateComponentKind? PromptKind(Editor ed, string role)
        {
            var opts = new PromptKeywordOptions(
                $"\n[道路] {role} [Pavement/NonMotor/Sidewalk/Green/Median]: ")
            {
                AllowNone = true,
            };
            opts.Keywords.Add("Pavement");
            opts.Keywords.Add("NonMotor");
            opts.Keywords.Add("Sidewalk");
            opts.Keywords.Add("Green");
            opts.Keywords.Add("Median");
            opts.Keywords.Default = "Pavement";
            var r = ed.GetKeywords(opts);
            if (r.Status != PromptStatus.OK) return null;
            switch (r.StringResult)
            {
                case "NonMotor": return TemplateComponentKind.NonMotorized;
                case "Sidewalk": return TemplateComponentKind.Sidewalk;
                case "Green": return TemplateComponentKind.GreenStrip;
                case "Median": return TemplateComponentKind.MedianStrip;
                default: return TemplateComponentKind.Pavement;
            }
        }

        private static BandMergeStrategy? PromptStrategy(Editor ed)
        {
            var opts = new PromptKeywordOptions("\n[道路] 合并策略 [KeepSelf/KeepOpp/Blend/Arc]: ")
            {
                AllowNone = true,
            };
            opts.Keywords.Add("KeepSelf");
            opts.Keywords.Add("KeepOpp");
            opts.Keywords.Add("Blend");
            opts.Keywords.Add("Arc");
            opts.Keywords.Default = "Arc";
            var r = ed.GetKeywords(opts);
            if (r.Status != PromptStatus.OK) return null;
            switch (r.StringResult)
            {
                case "KeepSelf": return BandMergeStrategy.KeepSelf;
                case "KeepOpp": return BandMergeStrategy.KeepOpposite;
                case "Blend": return BandMergeStrategy.BlendByLength;
                default: return BandMergeStrategy.ConnectWithArc;
            }
        }
    }
}
