using System;
using System.Collections.Generic;
using HyCADTool.Refactored.Domain.Models.Road.ControlElements;
using HyCADTool.Refactored.Domain.Models.Road.Serialization;
using Newtonsoft.Json;

namespace HyCADTool.Refactored.Domain.Models.Road
{
    /// <summary>
    /// 道路设计聚合根（一个项目 = 一个 <see cref="RoadDesign"/>）。
    ///
    /// 决策 1（单文件 JSON）：整个聚合根由 <c>RoadJsonExportService</c> 序列化到 <c>.roaddesign.json</c>。
    /// 决策 2（不做纯 JSON）：Domain 模型不直接参与 UI 绑定，保持纯粹；Presentation 层通过 ViewModel 投影。
    /// 决策 3（真实事件总线）：任何子对象变更都通过 <c>IRoadEventBus</c> 广播。
    /// 决策 4（DWG Xdata）：<see cref="Alignment"/> / Template 等的 <c>Id</c> 会同步写入 DWG 对象的 Xdata。
    /// 决策 5（v2 Blender）：<see cref="ValueObjects.Geometry.Mesh3D"/> 由 v2 Corridor Mesh Builder 生成后写入 JSON。
    /// </summary>
    public sealed class RoadDesign
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>项目名称（通常与 DWG 文件名同名）。</summary>
        public string ProjectName { get; set; }

        /// <summary>Schema 版本，由 <see cref="SchemaVersion.Current"/> 写入。</summary>
        public string Schema { get; set; } = SchemaVersion.Current;

        /// <summary>坐标系 EPSG 编码（可选）。v1 默认 null（世界平面坐标）。</summary>
        public string CrsEpsg { get; set; }

        /// <summary>创建时间（UTC）。</summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>最近一次变更时间（UTC），事件总线触发时更新。</summary>
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;

        public List<Alignment> Alignments { get; } = new List<Alignment>();
        public List<Template> Templates { get; } = new List<Template>();
        public List<Corridor> Corridors { get; } = new List<Corridor>();

        /// <summary>
        /// 交叉口 / 路口节点（v1 仅作为已迁移的 <see cref="RoadArm"/> 聚合容器）。
        /// </summary>
        public List<RoadNode> Nodes { get; } = new List<RoadNode>();

        /// <summary>
        /// P3 引入的"<b>从 Alignment 驱动</b>的平面交叉口"集合（与旧 <see cref="Nodes"/> 并存）。
        /// <para>每个 <see cref="Intersection"/> 由 <c>IntersectionDesigner.ComputeFromAlignments</c> 基于 2+ 条
        /// <see cref="Alignment"/> 的端点生成四臂 / 三臂 / Y 臂 + 转角 CornerArc；通过 <see cref="Intersection.Id"/>
        /// 与 DWG Xdata（KIND="Intersection"）挂钩。</para>
        /// <para>本容器会被 <c>RoadJsonExportService</c> 序列化到 <c>.roaddesign.json</c> 里；JSON schema 向后兼容
        /// （Newtonsoft 对缺失字段默认空列表）。</para>
        /// </summary>
        public List<Intersection> Intersections { get; } = new List<Intersection>();

        /// <summary>
        /// M6 引入的「控制体」集合：参考点 / 参考线 / 参考面 / 选中集合。
        /// <para>与 <see cref="Alignments"/> / <see cref="Intersections"/> 等实体容器并存；不参与 3D 导出。</para>
        /// <para>JSON 多态读写通过 <see cref="HyControlJsonConverter"/> 按 <c>Kind</c> 字段派发。</para>
        /// </summary>
        [JsonProperty(ItemConverterType = typeof(HyControlJsonConverter),
                      ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<IHyControl> Controls { get; set; } = new List<IHyControl>();

        /// <summary>
        /// M7 引入的「结构层方案库」：每个方案 = 一组具名结构层（面层 / 基层 / 垫层 / 自定义），
        /// 由 <c>hyRoadStructureLayer</c> 命令维护，供横断面板块的 <c>路面结构</c> 引用。
        /// </summary>
        public List<StructureLayerScheme> StructureLayerSchemes { get; } = new List<StructureLayerScheme>();

        /// <summary>
        /// M9 引入的「交叉口条带合并规则」覆盖条目。为空时 <c>IntersectionDesigner</c> 使用内置默认规则。
        /// </summary>
        public List<IntersectionBandMergeRule> IntersectionBandMergeRules { get; } = new List<IntersectionBandMergeRule>();

        /// <summary>
        /// 是否不含任何可持久化的子对象。
        ///
        /// 用途：<c>RoadJsonExportService.SaveForDocument</c> 在写盘前校验；
        /// 避免 SAVEAS / 切文档瞬间 <c>GetOrCreate</c> 出的占位空壳被写成误导性 <c>.roaddesign.json</c>。
        ///
        /// 标 <see cref="JsonIgnoreAttribute"/>：派生状态，不参与 JSON 序列化。
        /// </summary>
        [JsonIgnore]
        public bool IsEmpty
            => Alignments.Count == 0
            && Templates.Count == 0
            && Corridors.Count == 0
            && Nodes.Count == 0
            && Intersections.Count == 0
            && (Controls == null || Controls.Count == 0)
            && StructureLayerSchemes.Count == 0
            && IntersectionBandMergeRules.Count == 0;

        public override string ToString()
            => $"RoadDesign[{ProjectName}, Id={Id:N}, Schema={Schema}, A={Alignments.Count}, T={Templates.Count}, C={Corridors.Count}, I={Intersections.Count}]";
    }
}
