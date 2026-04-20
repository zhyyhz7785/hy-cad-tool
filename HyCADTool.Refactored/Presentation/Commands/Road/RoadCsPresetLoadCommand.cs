using System.Linq;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Infrastructure.Configuration;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// <c>hyRoadCsPresetLoad</c>（M7.1）：列出全部可用预设（内置 + 用户）供用户选择。
    /// MVP：只在命令行列出预设名；用户可直接把返回的 Layout 走 hyRoadCs 进一步编辑。
    /// </summary>
    public sealed class RoadCsPresetLoadCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            var svc = ServiceLocator.Resolve<CrossSectionPresetService>();
            var all = svc.LoadAll();
            if (all.Count == 0)
            {
                ed.WriteMessage("\n[道路] 尚无可用预设。");
                return;
            }

            ed.WriteMessage($"\n[道路] 共 {all.Count} 条预设：");
            for (int i = 0; i < all.Count; i++)
            {
                ed.WriteMessage($"\n  [{i + 1}] {all[i].Key}  →  {all[i].DisplayName}");
            }

            var keyOpts = new PromptStringOptions("\n[道路] 输入要查看的预设 Key（或 Esc 取消）: ")
            {
                AllowSpaces = false,
            };
            var pr = ed.GetString(keyOpts);
            if (pr.Status != PromptStatus.OK) return;

            var layout = svc.LoadByName(pr.StringResult);
            if (layout == null)
            {
                ed.WriteMessage($"\n[道路] 未找到预设 Key = {pr.StringResult}。");
                return;
            }

            ed.WriteMessage(
                $"\n[道路] 已加载：{layout.Title}"
                + $"\n        总宽 {layout.TotalWidth:F2} m，设计速度 {layout.DesignSpeed} km/h，比例 1:{layout.ScaleDenominator}"
                + $"\n        左半 {layout.LeftBands.Count} 条带 / 右半 {layout.RightBands.Count} 条带 / 中分带 {layout.CenterMedianWidth:F2} m"
                + "\n[道路] 提示：跑 hyRoadCs 并在顶部下拉框选择此预设即可用于出图（M7 UI 接入后）。");
        }
    }
}
