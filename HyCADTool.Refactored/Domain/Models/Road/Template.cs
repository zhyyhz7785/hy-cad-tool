using System;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Domain.Models.Road
{
    /// <summary>
    /// 横断面模板（道路部件 / Assembly）。
    ///
    /// v1 职责：作为 Corridor 的几何生成依据，仅保存"相对中心线的横截点"；
    /// v2 职责：额外承载 Blender 材质键（<see cref="TemplatePoint.MaterialKey"/>）和挤出提示（<see cref="TemplatePoint.BlenderExtrudeHint"/>），
    ///          Blender 插件以此生成 Corridor Mesh。
    ///
    /// 数据结构：按"左 - 中心 - 右"顺序记录一系列 <see cref="TemplatePoint"/>；
    /// 每对相邻点构成一段 <see cref="TemplateComponent"/>（车道 / 路缘 / 边坡等）。
    /// </summary>
    public sealed class Template
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; }

        /// <summary>
        /// 模板级扩展元数据（向后兼容）。
        /// 用于承载不适合拆成强类型属性、但需要随 Template 一起持久化的附加参数。
        /// </summary>
        public Dictionary<string, string> ExtendedData { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 横截点序列（按横向偏移从左到右）。中心点 Offset=0 一般位于集合中部。
        /// </summary>
        public List<TemplatePoint> Points { get; } = new List<TemplatePoint>();

        /// <summary>
        /// 构造的部件段（Point[i] 到 Point[i+1] 之间的功能段）。
        /// </summary>
        public List<TemplateComponent> Components { get; } = new List<TemplateComponent>();

        public override string ToString() => $"Template[{Name}, Id={Id:N}, Points={Points.Count}, Components={Components.Count}]";
    }

    /// <summary>
    /// 横断面上的一个控制点。
    /// </summary>
    public sealed class TemplatePoint
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>用户可见名称，例如 "左路肩外缘"、"中心线"。</summary>
        public string Name { get; set; }

        /// <summary>相对中心线的横向偏移（m，向右为正，向左为负）。</summary>
        public double HorizontalOffset { get; set; }

        /// <summary>相对路面中心线设计标高的纵向偏移（m，向上为正）。</summary>
        public double VerticalOffset { get; set; }

        /// <summary>
        /// v2 预留：Blender 材质键（映射到 <c>_libraries/blender-material-mapping.json</c>）。
        /// 例如 "asphalt_base"、"kerb_stone"、"sidewalk_tile"。
        /// v1 允许为 null。
        /// </summary>
        public string MaterialKey { get; set; }

        /// <summary>
        /// v2 预留：Blender 挤出提示。
        /// 允许 "extrude_down_0.3" 或 "solid_0.15" 等标记，由 Blender 插件解析。
        /// v1 允许为 null。
        /// </summary>
        public string BlenderExtrudeHint { get; set; }

        /// <summary>
        /// 扩展元数据（向后兼容）。
        /// 用于存放不改变主 JSON 结构的附加点信息，例如 rCs v3 的 ElevationDiff。
        /// </summary>
        public Dictionary<string, string> ExtendedData { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 模板段功能类型。
    ///
    /// 数值兼容性：历史值 0-6 固定不动，新增值顺延，既有 JSON 文件按原数值解析。
    /// </summary>
    public enum TemplateComponentKind
    {
        Unknown = 0,
        Pavement = 1,
        Sidewalk = 2,
        Kerb = 3,
        MedianStrip = 4,
        Shoulder = 5,
        Slope = 6,

        /// <summary>非机动车道（自行车 / 电动车）。CJJ 37 §6.5。</summary>
        NonMotorized = 7,

        /// <summary>绿化带（分车绿带 / 行道树带），与 <see cref="MedianStrip"/> 区分。</summary>
        GreenStrip = 8
    }

    /// <summary>
    /// 模板段（Point[i] 到 Point[i+1] 的功能段）。
    /// </summary>
    public sealed class TemplateComponent
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>段两端点 ID（引用 <see cref="TemplatePoint.Id"/>）。</summary>
        public Guid StartPointId { get; set; }
        public Guid EndPointId { get; set; }

        public TemplateComponentKind Kind { get; set; } = TemplateComponentKind.Unknown;

        /// <summary>
        /// v2 预留：该段主材质键（映射到 <c>_libraries/blender-material-mapping.json</c>）。
        /// v1 允许为 null。
        /// </summary>
        public string MaterialKey { get; set; }
    }
}
