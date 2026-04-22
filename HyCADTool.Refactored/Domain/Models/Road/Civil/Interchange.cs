using System;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Domain.Models.Road.Civil
{
    /// <summary>
    /// 立交（045 / M2 占位）。
    ///
    /// 对应项目树中「立交 Interchanges」一级节点；与 `Intersection`（平交）分开 —— 
    /// 平交是 <see cref="Intersection"/>（2+ 条 Alignment 端点相交），
    /// 立交涉及 **主线 + 多匝道 + 接线** 的组合，v3+ 才会展开为完整几何模型。
    /// v2.0 仅持久化最小字段与匝道 Alignment 引用。
    /// </summary>
    public sealed class Interchange
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; }

        /// <summary>立交类型。</summary>
        public InterchangeKind Kind { get; set; } = InterchangeKind.Custom;

        /// <summary>主线 Alignment；null 表示未指定。</summary>
        public Guid? MainAlignmentId { get; set; }

        /// <summary>
        /// 参与此立交的 Alignment 集合（含主线、匝道、接线）。
        /// <para>v2.0 仅记录 Id 引用；不负责拓扑计算；实际几何仍在各自 Alignment 上。</para>
        /// </summary>
        public List<Guid> AlignmentIds { get; } = new List<Guid>();

        public override string ToString()
            => $"Interchange[{Name}, Id={Id:N}, Kind={Kind}, Ramps={AlignmentIds.Count}]";
    }

    /// <summary>立交类型（v2.0 仅做类别标签，不驱动几何）。</summary>
    public enum InterchangeKind
    {
        Custom = 0,
        /// <summary>出入口。</summary>
        RampInOut = 1,
        /// <summary>下穿式立交。</summary>
        UnderPass = 2,
        /// <summary>上跨式立交。</summary>
        OverPass = 3
    }
}
