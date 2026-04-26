using System;
using HyCADTool.Features.Road.Events;
using HyCADTool.Domain.Models.Road;

namespace HyCADTool.Shared.AutoCAD.Services.Road
{
    /// <summary>
    /// 横断面模板服务（P0 占位 / P2 实现）�?    /// P2 阶段提供 TemplateEditor UI（可视化横截面），支�?MaterialKey / BlenderExtrudeHint 配置�?    /// </summary>
    public sealed class RoadTemplateService
    {
        private readonly RoadDesignRegistry _registry;
        private readonly IRoadEventBus _eventBus;

        public RoadTemplateService(RoadDesignRegistry registry, IRoadEventBus eventBus)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public Template CreateEmpty(string documentName, string displayName = null)
        {
            var design = _registry.GetOrCreate(documentName);
            var tpl = new Template
            {
                Name = string.IsNullOrWhiteSpace(displayName)
                    ? $"Template {design.Templates.Count + 1}"
                    : displayName
            };
            design.Templates.Add(tpl);
            design.LastModifiedUtc = DateTime.UtcNow;

            _eventBus.Publish(new TemplateChangedEvent(design.Id, tpl.Id, RoadChangeKind.Created));
            return tpl;
        }

        public bool Delete(string documentName, Guid templateId)
        {
            if (!_registry.TryGet(documentName, out var design)) return false;
            int removed = design.Templates.RemoveAll(t => t.Id == templateId);
            if (removed == 0) return false;

            design.LastModifiedUtc = DateTime.UtcNow;
            _eventBus.Publish(new TemplateChangedEvent(design.Id, templateId, RoadChangeKind.Deleted));
            return true;
        }
    }
}
