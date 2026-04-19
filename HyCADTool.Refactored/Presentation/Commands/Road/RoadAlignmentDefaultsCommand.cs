using System;
using HyCADTool.Refactored.Presentation.ViewModels;
using HyCADTool.Refactored.Presentation.ViewModels.Road;
using HyCADTool.Refactored.Presentation.Views.Road;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// <c>hyRoadAlnDefaults</c> — 维护"平面线位默认值"。
    ///
    /// 作用：在 <c>hyRoadAlnByPi</c>、<c>hyRoadAlnEditPi</c> 等创建命令里回车默认值
    /// （默认 R / Ls_in / Ls_out / 起桩号）需要落在工程实际数量级上，
    /// 这些默认值持久化在 <see cref="SettingsPanelViewModel"/>，
    /// 本命令提供一个独立的 BlenderWindow 配置入口，改一次就全局生效，避免每条命令重复敲键。
    ///
    /// 执行流：
    /// 1. 读 <see cref="SettingsPanelViewModel.Current"/>.CreateAlignmentDefaults() 作为窗口初值；
    /// 2. ShowModalWindow 打开 <see cref="AlignmentDefaultsWindow"/>；
    /// 3. 用户确定时通过 <see cref="SettingsPanelViewModel.ApplyAlignmentDefaults"/> 写回并即时落盘。
    /// </summary>
    public sealed class RoadAlignmentDefaultsCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            var settings = SettingsPanelViewModel.Current;
            if (settings == null)
            {
                ed.WriteMessage("\n[道路] SettingsPanelViewModel 不可用（hy-settings.json 未加载），无法配置默认值。");
                return;
            }

            var initial = settings.CreateAlignmentDefaults();
            var vm = new AlignmentDefaultsViewModel(initial);
            var window = new AlignmentDefaultsWindow(vm);

            bool? result = null;
            vm.CloseRequested += (_, dialogResult) =>
            {
                result = dialogResult;
            };

            AcApp.ShowModalWindow(window);

            if (result == true)
            {
                try
                {
                    settings.ApplyAlignmentDefaults(vm.Snapshot());
                    // SettingsPanelViewModel 的 SetProperty 会自动触发 SaveSettings
                    ed.WriteMessage(
                        $"\n[道路] Alignment 默认值已更新：R={vm.DefaultRadius:F2} m, "
                        + $"Ls_in={vm.DefaultSpiralIn:F2} m, Ls_out={vm.DefaultSpiralOut:F2} m, "
                        + $"起桩号={vm.DefaultStartStation:F3} m。（→ hy-settings.json）");
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n[道路] 保存默认值失败：{ex.Message}");
                }
            }
            else
            {
                ed.WriteMessage("\n[道路] 已取消。");
            }
        }
    }
}
