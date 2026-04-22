using System;
using System.Collections.Generic;
using HyCADTool.Refactored.Domain.Models.Road.Civil;
using Newtonsoft.Json;

namespace HyCADTool.Refactored.Domain.Models.Road
{
    /// <summary>
    /// 道路项目根聚合（045 / M2）。
    ///
    /// 目的：以 <see cref="RoadProject"/> 包住一个或多个 <see cref="RoadDesign"/>（每个对应一份 DWG），
    /// 配合 <see cref="DataShortcut"/> 做跨 DWG 对象引用，对齐 Autodesk Civil 3D Toolspace 的"项目 +
    /// Data Shortcut"模型与纬地路立得"工程树"体验。
    /// <para>Schema：<see cref="SchemaVersion.CurrentProject"/>（v2.0.0）。v1.x 的纯 <see cref="RoadDesign"/>
    /// 文档可被 <c>RoadJsonExportService</c> 自动包装为单 <c>Designs[0]</c> 的 <see cref="RoadProject"/>。</para>
    /// <para>UI：<c>RoadProjectTreePanel</c>（M3 交付）。</para>
    /// </summary>
    public sealed class RoadProject
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>项目名（用户可编辑；默认取第一个 <see cref="RoadDesign.ProjectName"/>）。</summary>
        public string Name { get; set; }

        /// <summary>Schema 版本（项目级）。由 <see cref="SchemaVersion.CurrentProject"/> 写入。</summary>
        public string Schema { get; set; } = SchemaVersion.CurrentProject;

        /// <summary>项目根目录的绝对或相对路径；<see cref="DataShortcut.SourcePath"/> 相对此路径解析。</summary>
        public string RootDirectory { get; set; }

        /// <summary>创建时间（UTC）。</summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>最近变更时间（UTC）。</summary>
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 项目内的所有 <see cref="RoadDesign"/>（每个对应一份 DWG）。
        /// v2.0 初版通常只含 1 个，对应"单 DWG = 单项目"的最小工作流。
        /// </summary>
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<RoadDesign> Designs { get; set; } = new List<RoadDesign>();

        /// <summary>
        /// 跨 DWG 数据引用（Data Shortcut，045 §8）。M5 交付前仅作为持久化字段，UI 不渲染引用节点。
        /// </summary>
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<DataShortcut> Shortcuts { get; set; } = new List<DataShortcut>();

        /// <summary>
        /// 是否不含任何可持久化的子对象（所有 <see cref="Designs"/> 都 <see cref="RoadDesign.IsEmpty"/> 且无 Shortcut）。
        /// </summary>
        [JsonIgnore]
        public bool IsEmpty
        {
            get
            {
                if (Shortcuts != null && Shortcuts.Count > 0) return false;
                if (Designs == null || Designs.Count == 0) return true;
                for (int i = 0; i < Designs.Count; i++)
                {
                    var d = Designs[i];
                    if (d != null && !d.IsEmpty) return false;
                }
                return true;
            }
        }

        /// <summary>
        /// 便捷：获取主要 <see cref="RoadDesign"/>（<see cref="Designs"/> 首个非空项）；
        /// 向后兼容 v1.x「单 DWG 单项目」习惯。
        /// </summary>
        public RoadDesign GetPrimaryDesign()
        {
            if (Designs == null) return null;
            for (int i = 0; i < Designs.Count; i++)
            {
                if (Designs[i] != null) return Designs[i];
            }
            return null;
        }

        /// <summary>
        /// 静态工厂：用单个 <see cref="RoadDesign"/> 构造一个 <see cref="RoadProject"/>（v1.x 迁移默认路径）。
        /// </summary>
        public static RoadProject FromSingleDesign(RoadDesign design)
        {
            if (design == null) throw new ArgumentNullException(nameof(design));
            return new RoadProject
            {
                Name = design.ProjectName,
                Designs = new List<RoadDesign> { design }
            };
        }

        public override string ToString()
            => $"RoadProject[{Name}, Id={Id:N}, Schema={Schema}, Designs={Designs?.Count ?? 0}, Shortcuts={Shortcuts?.Count ?? 0}]";
    }
}
