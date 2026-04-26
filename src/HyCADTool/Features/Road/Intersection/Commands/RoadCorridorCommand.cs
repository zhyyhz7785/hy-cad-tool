using System.Linq;
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
    /// P0 占位命令：用当前文档的首条 Alignment + 首个 Template 组合新建一个 Corridor。
    /// P4 阶段扩展为"走廊定义 UI（分段 / 目标映射 / 超高 / 加宽）"。
    /// </summary>
    public sealed class RoadCorridorCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            // 只查不建，避免新建空壳
            if (!registry.TryGet(doc.Name, out var design))
            {
                doc.Editor.WriteMessage(
                    "\n[道路] 走廊需要至少 1 条 Alignment 和 1 个 Template，请先执行 hyRoadA / hyRoadT。");
                return;
            }

            var alignment = design.Alignments.FirstOrDefault();
            var template = design.Templates.FirstOrDefault();
            if (alignment == null || template == null)
            {
                doc.Editor.WriteMessage(
                    "\n[道路] 走廊需要至少 1 条 Alignment 和 1 个 Template，请先执行 hyRoadA / hyRoadT。");
                return;
            }

            var svc = ServiceLocator.Resolve<RoadCorridorService>();
            var corridor = svc.Create(doc.Name, alignment.Id, template.Id);

            // 命令收尾同步落盘（v1.1）
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
            var savedTo = exporter.SaveForDocument(design, doc.Name);

            doc.Editor.WriteMessage(
                $"\n[道路] 已创建走廊 {corridor.Name}（Id={corridor.Id:N}）。P4 阶段上线分段编辑。");
            if (savedTo != null)
                doc.Editor.WriteMessage($"\n[道路] JSON 已同步落盘：{savedTo}");
        }
    }
}
