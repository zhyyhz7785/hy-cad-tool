using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.Configuration;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// P0 占位命令：创建一个空的横断面模板。
    /// P2 阶段扩展为"模板编辑器（可视化横截面 + MaterialKey / BlenderExtrudeHint 编辑）"。
    /// </summary>
    public sealed class RoadTemplateCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var svc = ServiceLocator.Resolve<RoadTemplateService>();
            var tpl = svc.CreateEmpty(doc.Name);

            // 命令收尾同步落盘（v1.1）
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
            string savedTo = null;
            if (registry.TryGet(doc.Name, out var design))
                savedTo = exporter.SaveForDocument(design, doc.Name);

            doc.Editor.WriteMessage(
                $"\n[道路] 已创建横断面模板 {tpl.Name}（Id={tpl.Id:N}）。P2 阶段上线模板编辑器。");
            if (savedTo != null)
                doc.Editor.WriteMessage($"\n[道路] JSON 已同步落盘：{savedTo}");
        }
    }
}
