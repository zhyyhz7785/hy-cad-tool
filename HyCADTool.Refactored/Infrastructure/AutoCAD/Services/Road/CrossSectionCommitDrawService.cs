using System;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Drawing;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using HyCADTool.Refactored.Presentation.ViewModels;
using HyCADTool.Refactored.Presentation.ViewModels.Road;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road
{
    /// <summary>
    /// 横断面「落盘 Registry + JSON + 模型空间出图」的共享收尾逻辑（原 <c>RoadCrossSectionDrawCommand.DrawAndSave</c>）。
    /// </summary>
    public sealed class CrossSectionCommitDrawService
    {
        private readonly RoadDesignRegistry _registry;
        private readonly RoadJsonExportService _exporter;
        private readonly RoadStandardSectionDrawService _drawService;

        public CrossSectionCommitDrawService(
            RoadDesignRegistry registry,
            RoadJsonExportService exporter,
            RoadStandardSectionDrawService drawService)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
            _drawService = drawService ?? throw new ArgumentNullException(nameof(drawService));
        }

        /// <summary>
        /// 将 <paramref name="result"/> 写入当前文档的 <see cref="RoadDesign"/>，清旧实体后重绘，并同步 JSON。
        /// </summary>
        public CommitDrawOutcome Commit(
            Document doc,
            CrossSectionDesignerResult result,
            Point2d origin,
            Guid? replaceTemplateId,
            CrossSectionDrawMode drawMode = CrossSectionDrawMode.WithStructureThickness,
            bool drawSectionStructureFills = true)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (result == null) throw new ArgumentNullException(nameof(result));

            DrawingSheetTitleSpec titleSpec = SettingsPanelViewModel.Current != null
                ? SettingsPanelViewModel.Current.CreateDrawingSheetTitleSpec()
                : DrawingSheetTitleSpec.RoadCrossSectionDefault;

            var design = _registry.GetOrCreate(doc.Name);
            var template = result.Template;

            if (replaceTemplateId.HasValue)
            {
                int idx = design.Templates.FindIndex(t => t.Id == replaceTemplateId.Value);
                if (idx >= 0) design.Templates[idx] = template;
                else design.Templates.Add(template);
            }
            else
            {
                var existing = design.Templates.FirstOrDefault(t => t.Id == template.Id);
                if (existing != null)
                    design.Templates.Remove(existing);
                design.Templates.Add(template);
            }

            design.LastModifiedUtc = DateTime.UtcNow;

            int erased = 0;
            int created = 0;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                erased = _drawService.Clear(tr, doc.Database, template.Id);
                double planOff = SettingsPanelViewModel.Current?.RoadCrossSectionPlanStripVerticalOffsetM ?? 5.0;
                created = _drawService.Draw(tr, doc.Database, result.Figure, template, origin,
                    modelUnitPerMeter: 1.0, mode: drawMode, layout: result.Layout, annotationStyle: null,
                    sheetTitleSpec: titleSpec, planStripVerticalOffsetMeters: planOff,
                    drawSectionStructureFills: drawSectionStructureFills);
                tr.Commit();
            }

            string savedTo = _exporter.SaveForDocument(design, doc.Name);

            return new CommitDrawOutcome(
                erased,
                created,
                savedTo,
                replaceTemplateId.HasValue);
        }
    }

    public sealed class CommitDrawOutcome
    {
        public CommitDrawOutcome(int erased, int created, string jsonPath, bool wasReplace)
        {
            Erased = erased;
            Created = created;
            JsonPath = jsonPath ?? string.Empty;
            WasReplace = wasReplace;
        }

        public int Erased { get; }
        public int Created { get; }
        public string JsonPath { get; }
        public bool WasReplace { get; }
    }
}
