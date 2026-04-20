using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HyCADTool.Refactored.Domain.Models.Road.ControlElements
{
    /// <summary>
    /// 选中集合（SelectionSet）—— 一种 <see cref="IHyControl"/>。
    ///
    /// <para><b>用途</b></para>
    /// 用户临时保存一组 <see cref="IHyEntity.Id"/>，用于"下次命令批量操作" /
    /// "另存为方案时保留集合信息" / "规范校核时指定作用范围"。
    /// 不参与绘图，没有几何，只是 ID 列表。
    /// </summary>
    public sealed class SelectionSet : IHyControl
    {
        /// <summary>Kind 常量。</summary>
        public const string KindConstant = "Control.SelectionSet";

        public Guid Id { get; set; } = Guid.NewGuid();

        public string Kind { get; set; } = KindConstant;

        public string Name { get; set; }

        /// <summary>引用的实体 ID 列表（<see cref="IHyEntity.Id"/>）。</summary>
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<Guid> EntityIds { get; set; } = new List<Guid>();

        /// <summary>选中集合永远是永久保存的（不走 TransientManager）。</summary>
        public bool IsTransient { get; set; } = false;

        /// <summary>可选的描述。</summary>
        public string Description { get; set; }

        public override string ToString()
            => $"SelectionSet[{Name}, Id={Id:N}, Count={EntityIds?.Count ?? 0}]";
    }
}
