using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HyCADTool.Domain.Models.Road
{
    /// <summary>
    /// 结构层方案（Structure Layer Scheme）—— 一组有序结构层 + 方案元数据（M6/M7）。
    ///
    /// <para><b>对应图 7</b></para>
    /// 左侧 TreeView 一级节点（"机动车道(重)" / "机动车道(轻)" / "非机动车道" / "人行道铺装" /
    /// "硬路肩结构层1" / "沥青路面1" / "沥青路面2" / "硬路肩结构层2" / "高新区" 等）。
    /// 每个方案 = 方案名 + 多条 <see cref="StructureLayer"/>。
    ///
    /// <para><b>M6 阶段</b></para>
    /// 本类仅作 JSON Schema v1.2.0 字段预留；M7 由 <c>hyRoadStructureLayer</c> 命令 + WPF 窗口驱动。
    /// </summary>
    public sealed class StructureLayerScheme : IHyEntity
    {
        public const string KindConstant = "StructureLayerScheme";

        public Guid Id { get; set; } = Guid.NewGuid();

        string IHyEntity.Kind => KindConstant;

        /// <summary>方案名称（例："机动车道(重)" / "人行道铺装" / "高新区"）。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>可选的方案描述。</summary>
        public string Description { get; set; }

        /// <summary>
        /// 分层列表，顺序 = 从上到下（上表面在前）。
        /// </summary>
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<StructureLayer> Layers { get; set; } = new List<StructureLayer>();

        /// <summary>
        /// 是否为"内置"方案（系统自带示例）。内置方案不允许删除，只允许"另存为"。
        /// M7 加载预设时置 true。
        /// </summary>
        public bool IsBuiltIn { get; set; }

        public override string ToString()
            => $"StructureLayerScheme[{Name}, Layers={Layers?.Count ?? 0}]";
    }
}
