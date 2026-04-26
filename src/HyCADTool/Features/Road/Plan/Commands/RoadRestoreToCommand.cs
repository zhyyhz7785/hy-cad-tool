using System;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Features.Road.Events;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

using HyCADTool.Features.Road.PlanAlignment.Services;
using HyCADTool.Features.Road.CrossSection.Domain;
using HyCADTool.Features.Road.PlanAlignment.Commands;
using HyCADTool.Features.Road.CrossSection.Commands;
namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadRestoreTo</c>（M6.3）：命令行按序号一步还原（脚本化 / 快捷）。
    ///
    /// <para><b>工作流</b></para>
    /// <list type="number">
    ///   <item>命令行提示输入序号（默认最新一条）。</item>
    ///   <item><see cref="HistoryService.RestoreBySeqNo"/> → <see cref="RoadDesignRegistry.Replace"/> →
    ///         发布 <see cref="RoadDesignReloadedEvent"/>。</item>
    /// </list>
    /// </summary>
    public sealed class RoadRestoreToCommand
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
            var index = svc.LoadIndex(historyDir);
            if (index == null || index.Entries == null || index.Entries.Count == 0)
            {
                ed.WriteMessage($"\n[道路] 尚无还原点（{historyDir}）。");
                return;
            }

            int maxSeq = 0;
            foreach (var e in index.Entries)
                if (e.SeqNo > maxSeq) maxSeq = e.SeqNo;

            var opts = new PromptIntegerOptions($"\n[道路] 输入要还原的序号 (1-{maxSeq}) <{maxSeq}>: ")
            {
                AllowNone = true,
                DefaultValue = maxSeq,
                UseDefaultValue = true,
                LowerLimit = 1,
                UpperLimit = maxSeq,
                AllowNegative = false,
                AllowZero = false,
            };
            var pr = ed.GetInteger(opts);
            if (pr.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }

            int seqNo = pr.Value;
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var bus = ServiceLocator.Resolve<IRoadEventBus>();

            try
            {
                var restored = svc.RestoreBySeqNo(historyDir, seqNo);
                if (restored == null)
                {
                    ed.WriteMessage($"\n[道路] 未找到序号 #{seqNo} 或快照文件丢失。");
                    return;
                }
                registry.Replace(dwgPath, restored);
                bus.Publish(new RoadDesignReloadedEvent(restored.Id));
                ed.WriteMessage(
                    $"\n[道路] 已还原到 #{seqNo}。"
                    + "\n[道路] 提示：运行 hyRoadRepaintAll 以刷新 AutoCAD 图形。");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 还原失败：{ex.Message}");
            }
        }
    }
}
