using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Features.DesignSpec.Services;
using Newtonsoft.Json;
using System;
using System.IO;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Export
{
    /// <summary>
    /// HYMDPLANCHECK：同一 dump 用 InsertFromHyMdDump 公式编 plan，对照 HyMd TS plan JSON。不取点、不写图。
    /// </summary>
    public class HymdCadPlanCheckCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;
            if (ed == null) return;

            string dumpPath = PromptPath(ed, "\nHyMd dump JSON", FindDefault(
                "hymd-layout-dump.json", "hymd-dump.json", "cad-stamp-golden.dump.json"));
            if (string.IsNullOrWhiteSpace(dumpPath) || !File.Exists(dumpPath))
            {
                ed.WriteMessage("\n[HYMDPLANCHECK] dump 文件不存在。");
                return;
            }
            string planDefault = dumpPath.EndsWith(".dump.json", StringComparison.OrdinalIgnoreCase)
                ? dumpPath.Substring(0, dumpPath.Length - ".dump.json".Length) + ".plan.json"
                : Path.Combine(Path.GetDirectoryName(dumpPath) ?? ".", "hymd-plan.json");
            if (!File.Exists(planDefault))
            {
                string beside = Path.Combine(Path.GetDirectoryName(dumpPath) ?? ".", "hymd-plan.json");
                if (File.Exists(beside))
                    planDefault = beside;
            }
            string planPath = PromptPath(ed, "\nHyMd plan JSON（空=只写出基准 plan）", planDefault);

            CadLayoutDump dump;
            try
            {
                dump = JsonConvert.DeserializeObject<CadLayoutDump>(File.ReadAllText(dumpPath));
            }
            catch (Exception ex)
            {
                ed.WriteMessage("\n[HYMDPLANCHECK] dump 解析失败: " + ex.Message);
                return;
            }
            if (dump == null)
            {
                ed.WriteMessage("\n[HYMDPLANCHECK] dump 为空。");
                return;
            }

            double scale = 1.0;
            CadStampPlan hymdPlan = null;
            if (!string.IsNullOrWhiteSpace(planPath) && File.Exists(planPath))
            {
                try
                {
                    hymdPlan = JsonConvert.DeserializeObject<CadStampPlan>(File.ReadAllText(planPath));
                    if (hymdPlan != null && hymdPlan.Scale > 0)
                        scale = hymdPlan.Scale;
                }
                catch (Exception ex)
                {
                    ed.WriteMessage("\n[HYMDPLANCHECK] plan 解析失败: " + ex.Message);
                    return;
                }
            }

            CadStampPlan baseline;
            try
            {
                baseline = DesignSpecService.BuildStampPlanFromDump(dump, scale);
            }
            catch (Exception ex)
            {
                ed.WriteMessage("\n[HYMDPLANCHECK] 编基准 plan 失败: " + ex.Message);
                return;
            }

            string outPath = Path.Combine(Path.GetDirectoryName(dumpPath) ?? ".", "hymd-plan-from-dump.json");
            try
            {
                File.WriteAllText(outPath, JsonConvert.SerializeObject(baseline, Formatting.Indented));
                ed.WriteMessage("\n[HYMDPLANCHECK] 已写基准 " + outPath);
            }
            catch (Exception ex)
            {
                ed.WriteMessage("\n[HYMDPLANCHECK] 写基准失败: " + ex.Message);
            }

            if (hymdPlan == null)
            {
                ed.WriteMessage("\n[HYMDPLANCHECK] 未提供对照 plan，只写出基准。");
                return;
            }

            var diffs = CadStampPlanGolden.Diff(baseline, hymdPlan);
            ed.WriteMessage("\n[HYMDPLANCHECK] " + CadStampPlanGolden.FormatDiffs(diffs));
        }

        private static string PromptPath(Editor ed, string msg, string def)
        {
            var opt = new PromptStringOptions(msg)
            {
                AllowSpaces = true,
                UseDefaultValue = true,
                DefaultValue = def ?? ""
            };
            var res = ed.GetString(opt);
            if (res.Status != PromptStatus.OK)
                return null;
            string p = (res.StringResult ?? "").Trim().Trim('"');
            return string.IsNullOrWhiteSpace(p) ? def : p;
        }

        private static string FindDefault(params string[] fileNames)
        {
            string[] roots =
            {
                HymdCadCompareService.DefaultFixtureDir,
                Path.Combine(HymdCadCompareService.DefaultFixtureDir, "fixtures"),
                @"E:\Code\HyMdTauri2\packages\hymd-layout\test\fixtures"
            };
            foreach (var name in fileNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                foreach (var dir in roots)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(dir)) continue;
                        string p = Path.Combine(dir, name);
                        if (File.Exists(p)) return p;
                    }
                    catch { }
                }
            }
            return fileNames.Length > 0 ? fileNames[0] : "";
        }
    }
}
