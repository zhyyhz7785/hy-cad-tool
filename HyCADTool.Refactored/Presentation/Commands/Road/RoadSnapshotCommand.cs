using System;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.Configuration;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// <c>hyRoadSnapshot</c>（M6.3）：手动创建一个道路设计还原点。
    ///
    /// <para><b>工作流</b></para>
    /// <list type="number">
    ///   <item>命令行提示用户输入中文描述（例："删除中华大街A上的绿化带"）；回车默认 "手动保存"。</item>
    ///   <item>从 <see cref="RoadDesignRegistry"/> 获取当前文档的 <see cref="RoadDesign"/>；空或 IsEmpty 时提示并退出。</item>
    ///   <item>解析 <c>&lt;Dwg&gt;.roaddesign.history</c> 目录 → <see cref="HistoryService.CreateSnapshot"/> 写快照。</item>
    ///   <item>命令行回显 "已保存还原点 #N：描述"。</item>
    /// </list>
    ///
    /// <para><b>DWG 未保存时</b> 无法解析历史目录（路径需要 dwg basename） → 提示用户先 QSAVE，再执行本命令。</para>
    /// </summary>
    public sealed class RoadSnapshotCommand
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
                    "\n[道路] DWG 尚未保存，无法推断历史目录。请先 QSAVE / SAVEAS，再执行 hyRoadSnapshot。");
                return;
            }

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            if (!registry.TryGet(dwgPath, out var design) || design == null)
            {
                ed.WriteMessage($"\n[道路] 当前文档 {dwgPath} 没有道路数据，不创建还原点。");
                return;
            }
            if (design.IsEmpty)
            {
                ed.WriteMessage("\n[道路] 当前 RoadDesign 为空，不创建还原点。");
                return;
            }

            var opts = new PromptStringOptions("\n[道路] 输入还原点描述 <手动保存>: ")
            {
                AllowSpaces = true,
                DefaultValue = "手动保存",
                UseDefaultValue = true,
            };
            var pr = ed.GetString(opts);
            if (pr.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }

            string description = string.IsNullOrWhiteSpace(pr.StringResult) ? "手动保存" : pr.StringResult.Trim();

            var svc = ServiceLocator.Resolve<HistoryService>();
            HistoryEntry entry;
            try
            {
                entry = svc.CreateSnapshot(historyDir, design, description);
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 创建还原点失败：{ex.Message}");
                return;
            }

            if (entry == null)
            {
                ed.WriteMessage("\n[道路] 创建还原点失败（未知原因）。");
                return;
            }

            ed.WriteMessage(
                $"\n[道路] 已保存还原点 #{entry.SeqNo}：{entry.Description}   "
                + $"（{entry.SnapshotFileName}，{entry.SnapshotBytes / 1024.0:F1} KB）");
        }
    }
}
