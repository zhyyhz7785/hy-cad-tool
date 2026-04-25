using System;
using HyCADTool.Domain.Events.Road;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.Bootstrap;
using HyCADTool.Presentation.ViewModels.Road;
using HyCADTool.Presentation.Views.Road;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadHistory</c>（M6.3）：打开图 1 样式的数据还原窗口。
    ///
    /// <para><b>工作流</b></para>
    /// <list type="number">
    ///   <item>解析 DWG 同级的 <c>&lt;Dwg&gt;.roaddesign.history</c> 目录。</item>
    ///   <item>构造 <see cref="RoadHistoryViewModel"/>（读取索引），弹 <see cref="RoadHistoryWindow"/>。</item>
    ///   <item>用户点「还原」→ VM 触发 <see cref="RoadHistoryViewModel.RestoreRequested"/> →
    ///         本命令：<see cref="HistoryService.Restore"/> → <see cref="RoadDesignRegistry.Replace"/> →
    ///         发布 <see cref="RoadDesignReloadedEvent"/> →（建议用户随后运行 <c>hyRoadRepaintAll</c>）。</item>
    /// </list>
    /// </summary>
    public sealed class RoadHistoryCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            string dwgPath = doc.Name;

            string historyDir = HistoryService.GetHistoryDirectory(dwgPath);
            if (string.IsNullOrWhiteSpace(historyDir))
            {
                ed.WriteMessage(
                    "\n[道路] DWG 尚未保存，无法推断历史目录。请先 QSAVE / SAVEAS。");
                return;
            }

            var svc = ServiceLocator.Resolve<HistoryService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var bus = ServiceLocator.Resolve<IRoadEventBus>();

            var vm = new RoadHistoryViewModel(svc, historyDir);
            if (vm.Rows.Count == 0)
            {
                ed.WriteMessage(
                    $"\n[道路] 尚无还原点（{historyDir}）。先用 hyRoadSnapshot 创建第一条。");
                return;
            }

            vm.RestoreRequested += (_, entry) =>
            {
                if (entry == null) return;
                try
                {
                    var restored = svc.Restore(historyDir, entry);
                    if (restored == null)
                    {
                        ed.WriteMessage($"\n[道路] 还原点 #{entry.SeqNo} 加载失败（文件不存在）。");
                        return;
                    }
                    registry.Replace(dwgPath, restored);
                    bus.Publish(new RoadDesignReloadedEvent(restored.Id));
                    ed.WriteMessage(
                        $"\n[道路] 已还原到 #{entry.SeqNo}（{entry.Description}）。"
                        + "\n[道路] 提示：运行 hyRoadRepaintAll 以刷新 AutoCAD 图形到此状态。");
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n[道路] 还原失败：{ex.Message}");
                }
            };

            var window = new RoadHistoryWindow(vm);
            AcApp.ShowModalWindow(window);
        }
    }
}
