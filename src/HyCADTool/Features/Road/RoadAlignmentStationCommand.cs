using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.App.Bootstrap;
using HyCADTool.Presentation.ViewModels;
using HyCADTool.Presentation.ViewModels.Road;
using HyCADTool.Presentation.Views.Road;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadAlnStation</c> — 为当前 DWG 已登记的 Alignment 生成桩号标注。
    ///
    /// T6 在原"一键标注"的基础上加了 3 个关键字分支：
    /// <list type="bullet">
    ///   <item><b>应用 (A)</b>（默认）：从 <see cref="SettingsPanelViewModel"/> 读桩号配置，
    ///     调用 <see cref="RoadAlignmentService.DrawStationLabels"/> 生成标注。</item>
    ///   <item><b>配置 (C)</b>：弹 <see cref="StationLabelConfigWindow"/> 让用户交互改参数，
    ///     确定后持久化到 hy-settings.json（与 T4 走同一套）。</item>
    ///   <item><b>默认 (D)</b>：把桩号配置重置为 <see cref="RoadStationLabelOptions.Default"/>，
    ///     并持久化；下一次回车立即生效。</item>
    /// </list>
    ///
    /// 生成过程保持幂等：内部按 AlignmentId 先清旧标注，重复跑不会累积图元。
    /// </summary>
    public sealed class RoadAlignmentStationCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;

            switch (PromptBranch(ed))
            {
                case Branch.Apply:
                    DoApply(doc);
                    break;
                case Branch.Configure:
                    DoConfigure(ed);
                    break;
                case Branch.Defaults:
                    DoRestoreDefaults(ed);
                    break;
                default:
                    ed.WriteMessage("\n[道路] 已取消。");
                    break;
            }
        }

        private enum Branch { Apply, Configure, Defaults, Cancel }

        private static Branch PromptBranch(Editor ed)
        {
            var pko = new PromptKeywordOptions(
                "\n[道路] 桩号标注 [应用(A)/配置(C)/默认(D)] <应用>：")
            {
                AllowNone = true,
            };
            pko.Keywords.Add("Apply",     "A", "应用(A)");
            pko.Keywords.Add("Configure", "C", "配置(C)");
            pko.Keywords.Add("Defaults",  "D", "默认(D)");
            pko.Keywords.Default = "Apply";

            var res = ed.GetKeywords(pko);
            if (res.Status == PromptStatus.None) return Branch.Apply;
            if (res.Status != PromptStatus.OK) return Branch.Cancel;

            switch (res.StringResult)
            {
                case "Apply":     return Branch.Apply;
                case "Configure": return Branch.Configure;
                case "Defaults":  return Branch.Defaults;
                default:          return Branch.Cancel;
            }
        }

        // =============================================================================
        // A 分支：应用（原始行为）
        // =============================================================================

        private static void DoApply(Autodesk.AutoCAD.ApplicationServices.Document doc)
        {
            var ed = doc.Editor;
            var db = doc.Database;

            var svc = ServiceLocator.Resolve<RoadAlignmentService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();

            // 读持久化配置；SettingsPanelViewModel 不可用时退回默认
            var options = SettingsPanelViewModel.Current?.CreateStationLabelOptions()
                          ?? RoadStationLabelOptions.Default;
            try { options.Validate(); }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 桩号配置不合法，已退回默认：{ex.Message}");
                options = RoadStationLabelOptions.Default;
            }

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                svc.RebindForDocument(doc.Name, tr, db);

                if (!registry.TryGet(doc.Name, out var design) || design.Alignments.Count == 0)
                {
                    ed.WriteMessage(
                        "\n[道路] 当前图纸未登记任何平面线位，无法生成桩号。"
                        + "\n       请先运行 hyRoadA 或 hyRoadAlnByPi 创建 Alignment。");
                    tr.Abort();
                    return;
                }

                int totalMain = 0, totalSub = 0, touched = 0;
                var snapshot = design.Alignments.ToArray();
                foreach (var a in snapshot)
                {
                    if (a.Centerline == null || a.Centerline.VertexCount < 2)
                    {
                        ed.WriteMessage($"\n[道路] 跳过 {a.Name}：中心线顶点不足 2 个。");
                        continue;
                    }
                    var (mainCount, subCount) = svc.DrawStationLabels(doc.Name, tr, db, a.Id, options);
                    totalMain += mainCount;
                    totalSub += subCount;
                    touched++;
                    ed.WriteMessage(
                        $"\n[道路] {a.Name}：主桩 {mainCount}（每 {options.MainInterval} m）、副桩 {subCount}（每 {options.SubInterval} m），"
                        + $"平面长度 {a.Centerline.GetPlanarLength():F3} m。");
                }

                tr.Commit();

                if (touched == 0)
                    ed.WriteMessage("\n[道路] 没有可标注的 Alignment。");
                else
                    ed.WriteMessage(
                        $"\n[道路] 桩号标注完成：共 {touched} 条 Alignment，主桩 {totalMain}、副桩 {totalSub}；"
                        + $"已写入图层 {HyCADTool.Shared.AutoCAD.Xdata.HyRoadLayers.StationLayer}。");
            }
        }

        // =============================================================================
        // C 分支：配置
        // =============================================================================

        private static void DoConfigure(Editor ed)
        {
            var settings = SettingsPanelViewModel.Current;
            if (settings == null)
            {
                ed.WriteMessage("\n[道路] SettingsPanelViewModel 不可用，无法配置桩号参数。");
                return;
            }

            var initial = settings.CreateStationLabelOptions();
            var vm = new StationLabelConfigViewModel(initial);
            var window = new StationLabelConfigWindow(vm);

            bool? result = null;
            vm.CloseRequested += (_, r) => result = r;
            AcApp.ShowModalWindow(window);

            if (result == true)
            {
                try
                {
                    settings.ApplyStationLabelOptions(vm.Snapshot());
                    ed.WriteMessage(
                        $"\n[道路] 桩号配置已更新：主 {vm.MainInterval} m / 副 {vm.SubInterval} m，"
                        + $"文字 {vm.TextHeight} m，侧别 {(vm.SideIsLeft ? "左" : "右")}，"
                        + $"沿切线 {(vm.RotateTextAlongTangent ? "开" : "关")}。（→ hy-settings.json）"
                        + "\n[道路] 提示：回车 hyRoadAlnStation 再跑一次应用到图形。");
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n[道路] 保存配置失败：{ex.Message}");
                }
            }
            else
            {
                ed.WriteMessage("\n[道路] 已取消。");
            }
        }

        // =============================================================================
        // D 分支：恢复默认
        // =============================================================================

        private static void DoRestoreDefaults(Editor ed)
        {
            var settings = SettingsPanelViewModel.Current;
            if (settings == null)
            {
                ed.WriteMessage("\n[道路] SettingsPanelViewModel 不可用，无法保存默认配置。");
                return;
            }
            settings.ApplyStationLabelOptions(RoadStationLabelOptions.Default);
            ed.WriteMessage("\n[道路] 桩号配置已恢复为出厂默认并持久化。回车 hyRoadAlnStation 再跑一次应用。");
        }
    }
}
