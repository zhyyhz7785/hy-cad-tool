using System;
using HyCADTool.Refactored.Domain.Events.Road;
using HyCADTool.Refactored.Domain.Models.Road;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road
{
    /// <summary>
    /// 纵断面服务（P0 占位 / P3 实现）。P3 阶段将读取 AutoCAD 多段线 / 标高表生成 PVI。
    /// </summary>
    public sealed class RoadProfileService
    {
        private readonly RoadDesignRegistry _registry;
        private readonly IRoadEventBus _eventBus;

        public RoadProfileService(RoadDesignRegistry registry, IRoadEventBus eventBus)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public Profile CreateDesignProfile(string documentName, Guid alignmentId)
        {
            if (!_registry.TryGet(documentName, out var design))
                throw new InvalidOperationException("当前文档未初始化 RoadDesign。");

            var alignment = design.Alignments.Find(a => a.Id == alignmentId)
                ?? throw new ArgumentException($"Alignment {alignmentId} 不存在", nameof(alignmentId));

            var profile = new Profile
            {
                Name = $"{alignment.Name} - 设计纵断面",
                IsDesignProfile = true
            };
            alignment.Profiles.Add(profile);
            design.LastModifiedUtc = DateTime.UtcNow;

            _eventBus.Publish(new ProfileChangedEvent(design.Id, alignmentId, profile.Id, RoadChangeKind.Created));
            return profile;
        }
    }
}
