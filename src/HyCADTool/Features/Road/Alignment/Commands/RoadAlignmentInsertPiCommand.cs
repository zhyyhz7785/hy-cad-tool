using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Services.Road;
using HyCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Presentation.ViewModels;
using HyCADTool.Features.Road.PlanAlignment.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using HyCADTool.Features.Road.PlanAlignment.Domain;
using HyCADTool.Features.Road.PlanAlignment.Views;

using HyCADTool.Features.Road.PlanAlignment.Services;
namespace HyCADTool.Features.Road.PlanAlignment.Commands
{
    /// <summary>
    /// <c>hyRoadAlnInsertPi</c> — 在已有平面线位的 PI 表中插入一个新 PI。
    ///
    /// 交互流程：
    /// 1. 拾取 Alignment Polyline；
    /// 2. 命令行输入"插到第 i 号 PI 之前"（i∈[1..count-1]）；
    /// 3. 图上点取新 PI 坐标（UseBasePoint = 前一 PI，便于推断方向）；
    /// 4. 选窗口/命令行 → 输入新 PI 的 R / Ls_in / Ls_out（默认从 hy-settings.json AlignmentDefaults 读）；
    /// 5. 共享尾部 <see cref="RoadAlignmentPiPipeline.RebuildAndPersist"/>。
    ///
    /// 窗口分支复用 <see cref="PiThreeUnitWindow"/>：先把占位 PI 插入到 elements 再传给 VM，
    /// VM 把它当作一个可编辑的内部 PI。确认后取出新参数写回 elements 相应位置。
    /// </summary>
    public sealed class RoadAlignmentInsertPiCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            if (!RoadAlignmentPiPipeline.PickAlignment(doc, out var alignment, out var elements, out _))
                return;

            if (elements.Count < 2)
            {
                ed.WriteMessage("\n[道路] 当前平面线位 PI 不足，无法插入。");
                return;
            }

            RoadAlignmentPiPipeline.PrintPiTable(ed, elements);

            // Step 1：选插入位置
            int insertBefore = PromptInsertIndex(ed, elements.Count);
            if (insertBefore < 0) return;

            // Step 2：点取新 PI 坐标（UseBasePoint = insertBefore-1 对应的 PI）
            var basePi = elements[insertBefore - 1].P;
            var ppo = new PromptPointOptions($"\n[道路] 点取新 PI 坐标（插到 PI[{insertBefore}] 之前）：")
            {
                AllowNone = false,
                UseBasePoint = true,
                BasePoint = new Point3d(basePi.X, basePi.Y, 0),
                UseDashedLine = true,
            };
            var pres = ed.GetPoint(ppo);
            if (pres.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消。"); return; }
            var newP = new Point2D(pres.Value.X, pres.Value.Y);

            // Step 3：R / Ls 默认值来自 hy-settings.json
            var defaults = SettingsPanelViewModel.Current?.CreateAlignmentDefaults()
                           ?? new global::HyCADTool.Domain.ValueObjects.Road.AlignmentDefaults();

            // 占位 PI 预填默认值，随后交互修正
            var placeholder = new PiElement(newP, defaults.DefaultRadius, defaults.DefaultSpiralIn, defaults.DefaultSpiralOut);
            elements.Insert(insertBefore, placeholder);

            // Step 4：W / C 交互模式
            var mode = PromptMode(ed);
            if (mode == InteractionMode.Cancelled)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }

            double r, lsIn, lsOut;
            if (mode == InteractionMode.Window)
            {
                if (!CollectParamsViaWindow(elements, insertBefore, out r, out lsIn, out lsOut))
                {
                    ed.WriteMessage("\n[道路] 已取消。");
                    return;
                }
            }
            else
            {
                if (!CollectParamsViaPrompt(ed, placeholder, out r, out lsIn, out lsOut))
                    return;
            }

            elements[insertBefore] = new PiElement(newP, r, lsIn, lsOut);

            // Step 5：共享尾部
            string head = $"已在 PI[{insertBefore}] 位置插入新 PI ({newP.X:F3}, {newP.Y:F3})，"
                        + $"R={r:F3}, Ls_in={lsIn:F3}, Ls_out={lsOut:F3}。";
            RoadAlignmentPiPipeline.RebuildAndPersist(doc, alignment, elements, head);
        }

        private static int PromptInsertIndex(Editor ed, int count)
        {
            var opt = new PromptIntegerOptions(
                $"\n[道路] 在第几号 PI 之前插入 [1..{count - 1}]：")
            {
                LowerLimit = 1,
                UpperLimit = count - 1,
                AllowNone = false,
                AllowNegative = false,
                AllowZero = false,
            };
            var res = ed.GetInteger(opt);
            if (res.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消。"); return -1; }
            return res.Value;
        }

        private enum InteractionMode { Window, Prompt, Cancelled }

        private static InteractionMode PromptMode(Editor ed)
        {
            var pko = new PromptKeywordOptions("\n[道路] 选择交互方式 [窗口(W)/命令行(C)] <窗口>：") { AllowNone = true };
            pko.Keywords.Add("Window", "W", "窗口(W)");
            pko.Keywords.Add("CommandLine", "C", "命令行(C)");
            pko.Keywords.Default = "Window";
            var res = ed.GetKeywords(pko);
            if (res.Status == PromptStatus.Cancel) return InteractionMode.Cancelled;
            if (res.Status == PromptStatus.None) return InteractionMode.Window;
            if (res.Status != PromptStatus.OK) return InteractionMode.Cancelled;
            return string.Equals(res.StringResult, "CommandLine", StringComparison.Ordinal)
                ? InteractionMode.Prompt : InteractionMode.Window;
        }

        private static bool CollectParamsViaWindow(
            IReadOnlyList<PiElement> elements,
            int piIdx,
            out double r, out double lsIn, out double lsOut)
        {
            r = elements[piIdx].Radius;
            lsIn = elements[piIdx].SpiralIn;
            lsOut = elements[piIdx].SpiralOut;

            var vm = new PiThreeUnitViewModel(elements, piIdx);
            var window = new PiThreeUnitWindow(vm);
            using (var preview = new RoadAlignmentPreviewService())
            {
                try
                {
                    var initial = AlignmentPiDesigner.Build(elements, new PiDesignOptions());
                    preview.Update(initial.Polyline);
                }
                catch { /* 非致命 */ }
                vm.PreviewRequested += (_, res) => preview.Update(res.Polyline);
                AcApp.ShowModalWindow(window);
            }
            if (!vm.ConfirmedElement.HasValue) return false;
            r = vm.ConfirmedElement.Value.Radius;
            lsIn = vm.ConfirmedElement.Value.SpiralIn;
            lsOut = vm.ConfirmedElement.Value.SpiralOut;
            return true;
        }

        private static bool CollectParamsViaPrompt(
            Editor ed, PiElement current,
            out double r, out double lsIn, out double lsOut)
        {
            r = current.Radius; lsIn = current.SpiralIn; lsOut = current.SpiralOut;

            r = PromptDistance(ed, $"\n[道路] 新 PI 半径 R（默认 {current.Radius:F2}，0=折线）", current.Radius, out bool cR);
            if (cR) return false;
            lsIn = PromptDistance(ed, $"\n[道路] 新 PI 入侧 Ls_in（默认 {current.SpiralIn:F2}）", current.SpiralIn, out bool cI);
            if (cI) return false;
            lsOut = PromptDistance(ed, $"\n[道路] 新 PI 出侧 Ls_out（默认 {current.SpiralOut:F2}）", current.SpiralOut, out bool cO);
            if (cO) return false;
            return true;
        }

        private static double PromptDistance(Editor ed, string prompt, double defaultVal, out bool cancelled)
        {
            cancelled = false;
            var opt = new PromptDistanceOptions(prompt + "（回车保留）：")
            {
                DefaultValue = defaultVal, UseDefaultValue = true,
                AllowNegative = false, AllowZero = true, AllowNone = true,
            };
            var res = ed.GetDistance(opt);
            if (res.Status == PromptStatus.None) return defaultVal;
            if (res.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消。"); cancelled = true; return 0; }
            return res.Value;
        }
    }
}
