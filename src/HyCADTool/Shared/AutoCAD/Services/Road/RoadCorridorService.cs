using System;
using HyCADTool.Features.Road.Events;
using HyCADTool.Domain.Models.Road;

namespace HyCADTool.Shared.AutoCAD.Services.Road
{
    /// <summary>
    /// 走廊服务（P0 占位 / P4 实现）�?    /// P4 阶段将整�?Alignment + DesignProfile + Template，按 SamplingStep 离散化；
    /// P7 阶段借助 <c>ICorridorMeshBuilder</c> 输出 <c>Mesh3D</c> �?Blender�?    /// </summary>
    public sealed class RoadCorridorService
    {
        private readonly RoadDesignRegistry _registry;
        private readonly IRoadEventBus _eventBus;

        public RoadCorridorService(RoadDesignRegistry registry, IRoadEventBus eventBus)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public Corridor Create(string documentName, Guid alignmentId, Guid templateId, string displayName = null)
        {
            var design = _registry.GetOrCreate(documentName);

            var corridor = new Corridor
            {
                Name = string.IsNullOrWhiteSpace(displayName)
                    ? $"Corridor {design.Corridors.Count + 1}"
                    : displayName,
                AlignmentId = alignmentId
            };
            corridor.Segments.Add(new CorridorSegment
            {
                StartStation = 0,
                EndStation = 0,
                TemplateId = templateId,
                SamplingStep = 5.0
            });

            design.Corridors.Add(corridor);
            design.LastModifiedUtc = DateTime.UtcNow;

            _eventBus.Publish(new CorridorChangedEvent(design.Id, corridor.Id, RoadChangeKind.Created));
            return corridor;
        }
    }
}
