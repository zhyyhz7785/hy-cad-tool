using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation.ViewModels.Road;
using HyCADTool.Refactored.Presentation.Views.Road;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// P1：按 PI 创建的 Alignment 的单点参数编辑命令。
    ///
    /// 两条交互分支（通过命令行关键字 [窗口(W)/命令行(C)] 切换，默认 W）：
    /// - <b>W 窗口</b>：打开 <see cref="PiThreeUnitWindow"/>，实时预览 + 6 项规范检查；
    /// - <b>C 命令行</b>：老的 Prompt 链路，便于脚本/批处理。
    ///
    /// 两路径共用「解析 PI 表 → Build → RebuildCenterline → 落 JSON + Validate」尾部，
    /// 只在参数收集阶段分叉。
    /// </summary>
    public sealed class RoadAlignmentEditPiCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            var peo = new PromptEntityOptions("\n[道路] 拾取要编辑的平面线位（HY_ROAD Alignment）：");
            peo.SetRejectMessage("\n[道路] 只能选择已挂 HY_ROAD 的 Polyline。");
            peo.AddAllowedClass(typeof(Polyline), exactMatch: false);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }

            var svc = ServiceLocator.Resolve<RoadAlignmentService>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();

            Alignment alignment;
            List<PiElement> elements;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                svc.RebindForDocument(doc.Name, tr, db);

                var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                var kind = HyRoadXdata.ReadKind(tr, ent);
                if (!string.Equals(kind, "Alignment", StringComparison.Ordinal))
                {
                    ed.WriteMessage("\n[道路] 拾取的图元没有 HY_ROAD Alignment 标识。请使用 hyRoadA 或 hyRoadAlnByPi 先挂标识。");
                    return;
                }
                var id = HyRoadXdata.ReadId(tr, ent);
                if (id == Guid.Empty)
                {
                    ed.WriteMessage("\n[道路] 拾取的图元缺少 HY_ROAD/ID。");
                    return;
                }
                if (!registry.TryGet(doc.Name, out var design))
                {
                    ed.WriteMessage("\n[道路] 当前 DWG 没有道路设计数据，请先跑 hyRoadAlnByPi。");
                    return;
                }
                alignment = design.Alignments.FirstOrDefault(a => a.Id == id);
                if (alignment == null)
                {
                    ed.WriteMessage($"\n[道路] Domain 里找不到 AlignmentId={id:N} 的数据，可能是 JSON 未加载。");
                    return;
                }
                if (alignment.Source == null || alignment.Source.PiElements == null || alignment.Source.PiElements.Count < 2)
                {
                    ed.WriteMessage(
                        "\n[道路] 该平面线位不是按 PI 创建（或老 JSON 无 PI 表快照）。"
                        + "\n[道路] 请先跑 hyRoadAlnByPi 重建，或使用未来的 hyRoadAlnRepi 从几何反解（P1.c）。");
                    return;
                }

                elements = alignment.Source.PiElements
                    .Select(e => new PiElement(e.P, e.Radius, e.SpiralIn, e.SpiralOut, e.Tag))
                    .ToList();

                tr.Commit();
            }

            if (elements.Count < 3)
            {
                ed.WriteMessage("\n[道路] 当前平面线位内部无可编辑 PI（至少需要 3 个 PI）。");
                return;
            }

            // ===== 1) 选择交互模式 =====
            var mode = PromptMode(ed);
            if (mode == InteractionMode.Cancelled) return;

            // ===== 2) 收集 PI 序号 =====
            PrintPiTable(ed, elements);
            int piIdx = PromptPiIndex(ed, elements.Count);
            if (piIdx < 0) return;

            // ===== 3) 根据模式收集参数 =====
            double newR;
            double newLsIn;
            double newLsOut;
            if (mode == InteractionMode.Window)
            {
                if (!CollectParamsViaWindow(elements, piIdx, out newR, out newLsIn, out newLsOut))
                {
                    ed.WriteMessage("\n[道路] 已取消。");
                    return;
                }
            }
            else
            {
                if (!CollectParamsViaPrompt(ed, elements[piIdx], out newR, out newLsIn, out newLsOut)) return;
            }

            var current = elements[piIdx];
            elements[piIdx] = new PiElement(current.P, newR, newLsIn, newLsOut, current.Tag);

            // ===== 4) 统一尾部：重建 + 落盘 =====
            PiDesignResult result;
            try
            {
                result = AlignmentPiDesigner.Build(elements, new PiDesignOptions());
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 几何重建失败：{ex.Message}");
                return;
            }

            bool rebuilt;
            using (doc.LockDocument())
            using (var tr2 = db.TransactionManager.StartTransaction())
            {
                rebuilt = svc.RebuildCenterline(doc.Name, tr2, db, alignment.Id, result.Polyline);

                if (rebuilt)
                {
                    alignment.Source = RebuildSource(elements);
                }
                tr2.Commit();
            }

            if (!rebuilt)
            {
                ed.WriteMessage("\n[道路] DWG 中找不到同 AlignmentId 的 Polyline，已取消（考虑先跑 hyRoadLoad 回写）。");
                return;
            }

            string savedTo = null;
            if (registry.TryGet(doc.Name, out var design2))
            {
                savedTo = exporter.SaveForDocument(design2, doc.Name);
            }

            ed.WriteMessage(
                $"\n[道路] PI[{piIdx}] 已更新：R={newR:F3}, Ls_in={newLsIn:F3}, Ls_out={newLsOut:F3}。"
                + $"\n[道路] 重建后 顶点={alignment.Centerline.VertexCount}, "
                + $"圆角={result.CurvedPiCount}, 缓和={result.SpiraledPiCount}, 跳过={result.SkippedCount}, "
                + $"平面长度={alignment.Centerline.GetPlanarLength():F3} m。");

            foreach (var w in result.Warnings) ed.WriteMessage("\n[道路] ⚠ " + w);

            var v = alignment.Validate();
            if (!v.Ok)
            {
                ed.WriteMessage("\n[道路] ⚠ 自检未通过：");
                foreach (var e in v.Errors) ed.WriteMessage("\n  · " + e);
            }

            if (savedTo != null) ed.WriteMessage($"\n[道路] JSON 已同步落盘：{savedTo}");
        }

        // ===================== 模式枚举与选择 =====================

        private enum InteractionMode { Window, Prompt, Cancelled }

        /// <summary>
        /// 通过关键字提示让用户选择交互方式。默认「窗口」。
        /// 借助 <see cref="PromptKeywordOptions"/> 的关键字系统支持中英文输入。
        /// </summary>
        private static InteractionMode PromptMode(Editor ed)
        {
            var pko = new PromptKeywordOptions("\n[道路] 选择交互方式 [窗口(W)/命令行(C)] <窗口>：")
            {
                AllowNone = true,
            };
            pko.Keywords.Add("Window", "W", "窗口(W)");
            pko.Keywords.Add("CommandLine", "C", "命令行(C)");
            pko.Keywords.Default = "Window";

            var res = ed.GetKeywords(pko);
            if (res.Status == PromptStatus.Cancel) return InteractionMode.Cancelled;
            if (res.Status == PromptStatus.None) return InteractionMode.Window;
            if (res.Status != PromptStatus.OK) return InteractionMode.Cancelled;

            return string.Equals(res.StringResult, "CommandLine", StringComparison.Ordinal)
                ? InteractionMode.Prompt
                : InteractionMode.Window;
        }

        // ===================== 窗口分支 =====================

        /// <summary>
        /// 打开 <see cref="PiThreeUnitWindow"/>，挂接 Transient 预览，并等待用户确定/取消。
        /// 返回值 = 用户是否确认；确认时三个 out 参数被填充。
        /// </summary>
        private static bool CollectParamsViaWindow(
            IReadOnlyList<PiElement> elements,
            int piIdx,
            out double r,
            out double lsIn,
            out double lsOut)
        {
            r = elements[piIdx].Radius;
            lsIn = elements[piIdx].SpiralIn;
            lsOut = elements[piIdx].SpiralOut;

            var vm = new PiThreeUnitViewModel(elements, piIdx);
            var window = new PiThreeUnitWindow(vm);

            using (var preview = new RoadAlignmentPreviewService())
            {
                // 构造时已跑过一次 Recalculate，立即把初始预览画出来
                if (vm.LastReport != null)
                {
                    try
                    {
                        var initial = AlignmentPiDesigner.Build(elements, new PiDesignOptions());
                        preview.Update(initial.Polyline);
                    }
                    catch
                    {
                        // 非致命，用户一改参数就会重算
                    }
                }

                vm.PreviewRequested += (_, res) => preview.Update(res.Polyline);

                // ShowModalWindow 会把 WPF 窗口的 Owner 设为 AutoCAD 主窗口
                AcApp.ShowModalWindow(window);
            }

            if (!vm.ConfirmedElement.HasValue) return false;

            r = vm.ConfirmedElement.Value.Radius;
            lsIn = vm.ConfirmedElement.Value.SpiralIn;
            lsOut = vm.ConfirmedElement.Value.SpiralOut;
            return true;
        }

        // ===================== 命令行分支 =====================

        private static bool CollectParamsViaPrompt(
            Editor ed,
            PiElement current,
            out double r,
            out double lsIn,
            out double lsOut)
        {
            r = current.Radius;
            lsIn = current.SpiralIn;
            lsOut = current.SpiralOut;

            r = PromptDistance(ed, $"\n[道路] 新半径 R（当前 {current.Radius:F3}，0=折线）", current.Radius, out bool cancelR);
            if (cancelR) return false;
            lsIn = PromptDistance(ed, $"\n[道路] 新入侧缓和曲线 Ls_in（当前 {current.SpiralIn:F3}）", current.SpiralIn, out bool cancelLi);
            if (cancelLi) return false;
            lsOut = PromptDistance(ed, $"\n[道路] 新出侧缓和曲线 Ls_out（当前 {current.SpiralOut:F3}）", current.SpiralOut, out bool cancelLo);
            if (cancelLo) return false;

            return true;
        }

        // ===================== 辅助 =====================

        private static void PrintPiTable(Editor ed, IReadOnlyList<PiElement> elements)
        {
            ed.WriteMessage($"\n[道路] 当前 PI 表（共 {elements.Count} 个，首尾不可编辑）：");
            ed.WriteMessage("\n  idx  X          Y          R       Ls_in   Ls_out  Tag");
            for (int i = 0; i < elements.Count; i++)
            {
                var e = elements[i];
                string tag = string.IsNullOrEmpty(e.Tag) ? "-" : e.Tag;
                string mark = (i == 0 || i == elements.Count - 1) ? "*" : " ";
                ed.WriteMessage(
                    $"\n  {mark}{i,-3} {e.P.X,-10:F3} {e.P.Y,-10:F3} {e.Radius,-7:F2} {e.SpiralIn,-7:F2} {e.SpiralOut,-7:F2} {tag}");
            }
            ed.WriteMessage("\n  (* = 端点 PI，不可编辑)");
        }

        private static int PromptPiIndex(Editor ed, int count)
        {
            // PI 只有一个内部点时直接返回，不再提示
            if (count == 3) return 1;

            var opt = new PromptIntegerOptions(
                $"\n[道路] 输入要编辑的 PI 序号 [1..{count - 2}]：")
            {
                LowerLimit = 1,
                UpperLimit = count - 2,
                AllowNone = false,
                AllowNegative = false,
                AllowZero = false,
            };
            var res = ed.GetInteger(opt);
            if (res.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return -1;
            }
            return res.Value;
        }

        private static double PromptDistance(Editor ed, string prompt, double defaultVal, out bool cancelled)
        {
            cancelled = false;
            var opt = new PromptDistanceOptions(prompt + "（回车保留）：")
            {
                DefaultValue = defaultVal,
                UseDefaultValue = true,
                AllowNegative = false,
                AllowZero = true,
                AllowNone = true,
            };
            var res = ed.GetDistance(opt);
            if (res.Status == PromptStatus.None) return defaultVal;
            if (res.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                cancelled = true;
                return 0;
            }
            return res.Value;
        }

        private static AlignmentSource RebuildSource(IReadOnlyList<PiElement> elements)
        {
            var s = new AlignmentSource { Kind = AlignmentSourceKind.PiTable };
            foreach (var e in elements)
            {
                s.PiElements.Add(new AlignmentPiInput
                {
                    P = e.P,
                    Radius = e.Radius,
                    SpiralIn = e.SpiralIn,
                    SpiralOut = e.SpiralOut,
                    Tag = e.Tag,
                });
            }
            return s;
        }
    }
}
