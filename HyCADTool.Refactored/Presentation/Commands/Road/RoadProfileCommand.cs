using System.Linq;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.Configuration;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// P0 占位命令：为当前文档的首条 Alignment 追加一个设计纵断面（空白）。
    /// P3 阶段扩展为"读取标高表 / 拾取纵断面线 → PVI 序列"。
    /// </summary>
    public sealed class RoadProfileCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            // 只查不建：P0 阶段该命令依赖已有 Alignment，为防止 SAVEAS 下 GetOrCreate 新建空壳污染 Registry，改用 TryGet。
            if (!registry.TryGet(doc.Name, out var design))
            {
                doc.Editor.WriteMessage("\n[道路] 当前文档没有道路数据，请先执行 hyRoadA。");
                return;
            }

            var firstAlignment = design.Alignments.FirstOrDefault();
            if (firstAlignment == null)
            {
                doc.Editor.WriteMessage("\n[道路] 当前文档没有平面线位，请先执行 hyRoadA。");
                return;
            }

            var svc = ServiceLocator.Resolve<RoadProfileService>();
            var profile = svc.CreateDesignProfile(doc.Name, firstAlignment.Id);

            // 命令收尾同步落盘（v1.1）
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
            var savedTo = exporter.SaveForDocument(design, doc.Name);

            doc.Editor.WriteMessage(
                $"\n[道路] 已为 {firstAlignment.Name} 创建设计纵断面 {profile.Name}（Id={profile.Id:N}）。P3 阶段支持拾取标高。");
            if (savedTo != null)
                doc.Editor.WriteMessage($"\n[道路] JSON 已同步落盘：{savedTo}");
        }
    }
}
