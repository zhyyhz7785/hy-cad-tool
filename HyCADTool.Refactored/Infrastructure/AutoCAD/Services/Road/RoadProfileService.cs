using System;
using System.Collections.Generic;
using HyCADTool.Refactored.Domain.Events.Road;
using HyCADTool.Refactored.Domain.Models.Road;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road
{
    /// <summary>
    /// 纵断面服务。
    ///
    /// 职责（P1 收口范围）：
    /// - 维护 <see cref="Profile"/> 的生命周期：Create / Replace PVI / Delete；
    /// - 不写 DWG 实体（Profile v1 仅在 JSON 中存在，B4 标注命令实现时再加图层 / DBText）；
    /// - 不缓存 Document / Database 引用，所有调用以 <c>documentName</c> 为索引；
    /// - 所有写操作通过 <see cref="IRoadEventBus"/> 发出 <see cref="ProfileChangedEvent"/>，
    ///   命令层 / JSON 持久化 / Blender 桥接订阅同一总线。
    ///
    /// 与 SAVEAS 的关系：命令层在调用本服务前必须先 <c>RoadAlignmentService.RebindForDocument</c>；
    /// 本服务不直接处理 rebind，因为它不持有 DWG 句柄。
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

        /// <summary>
        /// （兼容旧调用）始终新建一个空的设计 Profile 并挂到 Alignment。
        ///
        /// 升级到 P1 后，建议命令层改用 <see cref="GetOrCreateDesignProfile"/>，避免重复创建空白 Profile。
        /// </summary>
        public Profile CreateDesignProfile(string documentName, Guid alignmentId)
        {
            var (design, alignment) = ResolveAlignment(documentName, alignmentId);

            var profile = new Profile
            {
                Name = $"{alignment.Name} - 设计纵断面",
                IsDesignProfile = true,
                LastModifiedUtc = DateTime.UtcNow,
            };
            alignment.Profiles.Add(profile);
            design.LastModifiedUtc = DateTime.UtcNow;

            _eventBus.Publish(new ProfileChangedEvent(design.Id, alignmentId, profile.Id, RoadChangeKind.Created));
            return profile;
        }

        /// <summary>
        /// 找出 Alignment 上唯一的设计 Profile；不存在则按名称约定新建一个并发 <see cref="ProfileChangedEvent"/>。
        ///
        /// 选择策略：
        /// - 优先返回 <c>IsDesignProfile = true</c> 的第一个；
        /// - 若一个都没有，新建 "[AlignmentName] - 设计纵断面" 并发 <see cref="RoadChangeKind.Created"/>。
        ///
        /// v1 不支持"一条 Alignment 多设计纵断面"。
        /// </summary>
        /// <returns>（profile, isNewlyCreated）。命令层据此决定 UI 的"新建"/"打开"措辞。</returns>
        public (Profile profile, bool created) GetOrCreateDesignProfile(string documentName, Guid alignmentId)
        {
            var (design, alignment) = ResolveAlignment(documentName, alignmentId);

            for (int i = 0; i < alignment.Profiles.Count; i++)
            {
                if (alignment.Profiles[i].IsDesignProfile)
                {
                    return (alignment.Profiles[i], false);
                }
            }

            var profile = new Profile
            {
                Name = $"{alignment.Name} - 设计纵断面",
                IsDesignProfile = true,
                DesignSpeed = 60,
                LastModifiedUtc = DateTime.UtcNow,
            };
            alignment.Profiles.Add(profile);
            design.LastModifiedUtc = DateTime.UtcNow;

            _eventBus.Publish(new ProfileChangedEvent(design.Id, alignmentId, profile.Id, RoadChangeKind.Created));
            return (profile, true);
        }

        /// <summary>
        /// 整批替换 Profile 的 PVI 列表（编辑窗口"确定"时调用）。
        ///
        /// 行为：
        /// - 校验 alignment / profile 存在，不存在返回 false；
        /// - 复制 newVertices 元素到 <c>Profile.Vertices</c>（不持有外部引用，避免 UI 后续修改影响内存模型）；
        /// - 刷新 <see cref="Profile.LastModifiedUtc"/> / <see cref="RoadDesign.LastModifiedUtc"/>；
        /// - 发出 <see cref="ProfileChangedEvent"/> with <see cref="RoadChangeKind.Updated"/>；
        /// - 不做几何或规范校核（由 <see cref="Domain.Services.Road.ProfileFgDesigner"/> /
        ///   <see cref="Domain.Services.Road.ProfileCodeChecker"/> 在 UI 层完成，本服务只负责状态写入）。
        /// </summary>
        public bool ReplaceVertices(string documentName, Guid alignmentId, Guid profileId, IList<ProfileVertex> newVertices)
        {
            if (newVertices == null) throw new ArgumentNullException(nameof(newVertices));

            if (!TryResolve(documentName, alignmentId, profileId, out var design, out var profile, out _))
                return false;

            profile.Vertices.Clear();
            for (int i = 0; i < newVertices.Count; i++)
            {
                var src = newVertices[i];
                if (src == null) continue;
                profile.Vertices.Add(new ProfileVertex
                {
                    Id = src.Id == Guid.Empty ? Guid.NewGuid() : src.Id,
                    Station = src.Station,
                    Elevation = src.Elevation,
                    CurveRadius = src.CurveRadius,
                });
            }
            profile.LastModifiedUtc = DateTime.UtcNow;
            design.LastModifiedUtc = DateTime.UtcNow;

            _eventBus.Publish(new ProfileChangedEvent(design.Id, alignmentId, profile.Id, RoadChangeKind.Updated));
            return true;
        }

        /// <summary>
        /// 创建或整批替换 Alignment 上的 EG 地面线 Profile（<see cref="Profile.IsDesignProfile"/> = <c>false</c>）。
        ///
        /// 行为：
        /// <list type="number">
        ///   <item>查找 alignment 上第一个 <c>IsDesignProfile == false</c> 的 Profile：
        ///     <list type="bullet">
        ///       <item>存在 → 清空 <c>Vertices</c> 并写入 <paramref name="sampled"/>，发 <see cref="RoadChangeKind.Updated"/>；</item>
        ///       <item>不存在 → 新建命名 "{AlignmentName} - 现状地面线"，写入 <paramref name="sampled"/>，发 <see cref="RoadChangeKind.Created"/>。</item>
        ///     </list>
        ///   </item>
        ///   <item>不修改 <see cref="Profile.DesignSpeed"/>（EG 与设计速度无关，新建时取 0 占位）；</item>
        ///   <item>不做几何校核（<see cref="Domain.Services.Road.EgProfileSampler"/> 已保证 Station 升序）；</item>
        ///   <item>刷新 <see cref="Profile.LastModifiedUtc"/> / <see cref="RoadDesign.LastModifiedUtc"/>，
        ///         发 <see cref="ProfileChangedEvent"/>；命令层负责 JSON 落盘。</item>
        /// </list>
        ///
        /// v1 不支持"一条 Alignment 多 EG"。后续若需要"同一 alignment 的多版本地面线"
        /// （例如设计前 / 设计后两期），需引入显式 Profile 标签字段，再升级本方法的查找策略。
        /// </summary>
        public Profile CreateOrReplaceEgProfile(string documentName, Guid alignmentId, IList<ProfileVertex> sampled)
        {
            if (sampled == null) throw new ArgumentNullException(nameof(sampled));

            var (design, alignment) = ResolveAlignment(documentName, alignmentId);

            Profile eg = null;
            for (int i = 0; i < alignment.Profiles.Count; i++)
            {
                if (!alignment.Profiles[i].IsDesignProfile)
                {
                    eg = alignment.Profiles[i];
                    break;
                }
            }

            bool created = false;
            if (eg == null)
            {
                eg = new Profile
                {
                    Name = $"{alignment.Name} - 现状地面线",
                    IsDesignProfile = false,
                    DesignSpeed = 0,
                };
                alignment.Profiles.Add(eg);
                created = true;
            }

            eg.Vertices.Clear();
            for (int i = 0; i < sampled.Count; i++)
            {
                var src = sampled[i];
                if (src == null) continue;
                eg.Vertices.Add(new ProfileVertex
                {
                    Id = src.Id == Guid.Empty ? Guid.NewGuid() : src.Id,
                    Station = src.Station,
                    Elevation = src.Elevation,
                    CurveRadius = src.CurveRadius,
                });
            }
            eg.LastModifiedUtc = DateTime.UtcNow;
            design.LastModifiedUtc = DateTime.UtcNow;

            var kind = created ? RoadChangeKind.Created : RoadChangeKind.Updated;
            _eventBus.Publish(new ProfileChangedEvent(design.Id, alignmentId, eg.Id, kind));
            return eg;
        }

        /// <summary>
        /// 删除 Alignment 下指定 Profile（v1 UI 不暴露入口，留作脚本 / 测试调用）。
        /// 找不到对象返回 false。
        /// </summary>
        public bool Delete(string documentName, Guid alignmentId, Guid profileId)
        {
            if (!TryResolve(documentName, alignmentId, profileId, out var design, out var profile, out var alignment))
                return false;

            alignment.Profiles.Remove(profile);
            design.LastModifiedUtc = DateTime.UtcNow;

            _eventBus.Publish(new ProfileChangedEvent(design.Id, alignmentId, profile.Id, RoadChangeKind.Deleted));
            return true;
        }

        // ===== 内部 =====

        private (RoadDesign design, Alignment alignment) ResolveAlignment(string documentName, Guid alignmentId)
        {
            if (!_registry.TryGet(documentName, out var design))
                throw new InvalidOperationException("当前文档未初始化 RoadDesign。请先创建或导入 Alignment。");

            var alignment = design.Alignments.Find(a => a.Id == alignmentId)
                ?? throw new ArgumentException($"Alignment {alignmentId:N} 不存在。", nameof(alignmentId));

            return (design, alignment);
        }

        private bool TryResolve(string documentName, Guid alignmentId, Guid profileId,
            out RoadDesign design, out Profile profile, out Alignment alignment)
        {
            design = null;
            profile = null;
            alignment = null;

            if (!_registry.TryGet(documentName, out design)) return false;
            alignment = design.Alignments.Find(a => a.Id == alignmentId);
            if (alignment == null) return false;
            profile = alignment.Profiles.Find(p => p.Id == profileId);
            return profile != null;
        }
    }
}
