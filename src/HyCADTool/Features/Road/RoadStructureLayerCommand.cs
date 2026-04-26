using System;
using System.Linq;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.App.Bootstrap;
using HyCADTool.Presentation.ViewModels.Road;
using HyCADTool.Presentation.Views.Road;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadStructureLayer</c>（M7.3）：打开结构层定义窗口。
    ///
    /// <para><b>工作流</b></para>
    /// <list type="number">
    ///   <item>从 <see cref="RoadDesignRegistry"/> 获取当前文档的 <see cref="RoadDesign"/>；无则新建空聚合根。</item>
    ///   <item>把 <see cref="RoadDesign.StructureLayerSchemes"/> 绑定给 VM；用户编辑后点确定 → 替换聚合。</item>
    ///   <item>事件总线发布 <c>RoadDesignReloadedEvent</c> 以触发重绘（结构层变化会影响横断面出图）。</item>
    ///   <item>同步落盘 <c>.roaddesign.json</c>。</item>
    /// </list>
    /// </summary>
    public sealed class RoadStructureLayerCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();

            var design = registry.GetOrCreate(doc.Name);

            var vm = new StructureLayerViewModel(design.StructureLayerSchemes);
            bool confirmed = false;
            vm.Confirmed += (_, list) =>
            {
                design.StructureLayerSchemes.Clear();
                foreach (var s in list) design.StructureLayerSchemes.Add(s);
                design.LastModifiedUtc = DateTime.UtcNow;
                confirmed = true;
            };

            var window = new StructureLayerWindow(vm);
            AcApp.ShowModalWindow(window);

            if (!confirmed)
            {
                ed.WriteMessage("\n[道路] 结构层编辑已取消。");
                return;
            }

            // 同步落盘（忽略返回 null 的情况：DWG 尚未保存时 SaveForDocument 会静默跳过）
            string path = exporter.SaveForDocument(design, doc.Name);
            if (path != null)
                ed.WriteMessage($"\n[道路] 结构层方案已保存到 {path}（共 {design.StructureLayerSchemes.Count} 条方案）。");
            else
                ed.WriteMessage("\n[道路] 结构层方案已更新到内存（DWG 尚未保存，未落盘）。");
        }
    }
}
