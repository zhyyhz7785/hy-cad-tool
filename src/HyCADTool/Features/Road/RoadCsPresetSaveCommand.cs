using System;
using System.Linq;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadCsPresetSave</c>（M7.1）：从当前 <see cref="Domain.Models.Road.RoadDesign"/> 选择一个 Template 对应的
    /// <see cref="CrossSectionLayout"/>，另存为用户预设（写 <c>%AppData%/HyCAD/presets/crosssection/</c>）。
    ///
    /// <para>MVP：仅使用首个 Template 映射回 <see cref="CrossSectionLayout"/>；更精细的"按 Template 名选择"由 M7.3 UI 完成。</para>
    /// <para>若当前 RoadDesign 没有 Template 或 LayoutBuilder 不支持反向转换时直接返回。</para>
    /// </summary>
    public sealed class RoadCsPresetSaveCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var presetSvc = ServiceLocator.Resolve<CrossSectionPresetService>();

            if (!registry.TryGet(doc.Name, out var design) || design == null || design.Templates.Count == 0)
            {
                ed.WriteMessage("\n[道路] 当前 RoadDesign 无 Template，无法导出预设。请先 hyRoadCs 建模一次。");
                return;
            }

            // 从第一个 Template 回推一张 Layout —— 仅作 MVP；精细化用户选择由 M7 UI 补齐。
            var tpl = design.Templates[0];
            var layout = CrossSectionLayoutBuilder.FromTemplate(tpl, defaultSpeed: 50);
            if (layout == null)
            {
                ed.WriteMessage($"\n[道路] Template[{tpl.Name}] 无法反求 CrossSectionLayout，导出失败。");
                return;
            }

            var keyOpts = new PromptStringOptions("\n[道路] 输入预设 Key（用作文件名）<my-preset>: ")
            {
                AllowSpaces = false,
                DefaultValue = "my-preset",
                UseDefaultValue = true,
            };
            var keyPr = ed.GetString(keyOpts);
            if (keyPr.Status != PromptStatus.OK) return;

            var nameOpts = new PromptStringOptions("\n[道路] 输入显示名 <我的方案>: ")
            {
                AllowSpaces = true,
                DefaultValue = "我的方案",
                UseDefaultValue = true,
            };
            var namePr = ed.GetString(nameOpts);
            if (namePr.Status != PromptStatus.OK) return;

            try
            {
                string path = presetSvc.SaveUserPreset(keyPr.StringResult, namePr.StringResult, layout);
                ed.WriteMessage($"\n[道路] 已保存方案：{path}");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 保存方案失败：{ex.Message}");
            }
        }
    }
}
