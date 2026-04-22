using System;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Domain.Models.Road.Serialization
{
    /// <summary>
    /// <c>.roaddesign.json</c>（v1.x）→ <see cref="RoadProject"/>（v2.0）的迁移辅助（045 / M2）。
    ///
    /// 策略（非破坏）：
    /// <list type="bullet">
    ///   <item>永远不删 / 不改名 v1.x 的 <c>.roaddesign.json</c> 物理文件；</item>
    ///   <item>把 <see cref="RoadDesign"/> 包进单 <c>Designs[0]</c> 的 <see cref="RoadProject"/>；</item>
    ///   <item>占位集合（Surfaces / Bridges / ... / Geologies）保持空 List；</item>
    ///   <item>内层 <see cref="RoadDesign.Schema"/> 透明升到 v2.0（表示"已具备 v2.0 占位字段"）。</item>
    /// </list>
    /// </summary>
    public static class RoadProjectMigration
    {
        /// <summary>
        /// 把一份 v1.x 的 <see cref="RoadDesign"/> 包装为 v2.0 的 <see cref="RoadProject"/>。
        /// <para>参数 <paramref name="design"/> 为 null 时返回空 project；对 design 无副作用之外的额外写入仅有
        /// <see cref="RoadDesign.Schema"/> = <see cref="SchemaVersion.Current"/>。</para>
        /// </summary>
        public static RoadProject WrapSingleDesign(RoadDesign design)
        {
            if (design == null)
                return new RoadProject();

            if (SchemaVersion.IsLegacyV1(design.Schema))
                design.Schema = SchemaVersion.Current;

            return new RoadProject
            {
                Name = string.IsNullOrWhiteSpace(design.ProjectName) ? "UnnamedProject" : design.ProjectName,
                Designs = new List<RoadDesign> { design }
            };
        }

        /// <summary>
        /// 判断给定的 <paramref name="project"/> 是否需要 v2.0 字段补齐（幂等，可反复调用）。
        /// 目前只负责把 <see cref="RoadProject.Schema"/> 设到 <see cref="SchemaVersion.CurrentProject"/>；
        /// 未来新增字段时在此追加"填默认值"逻辑。
        /// </summary>
        public static RoadProject EnsureV2Fields(RoadProject project)
        {
            if (project == null) return null;
            if (SchemaVersion.IsLegacyV1(project.Schema))
                project.Schema = SchemaVersion.CurrentProject;
            if (project.Designs == null) project.Designs = new List<RoadDesign>();
            if (project.Shortcuts == null) project.Shortcuts = new List<Civil.DataShortcut>();
            return project;
        }
    }
}
