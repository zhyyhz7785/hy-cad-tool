using System;

namespace HyCADTool.Domain.Models.Road.Civil
{
    /// <summary>
    /// 地质（045 / M2 占位）。
    ///
    /// 对应项目树「地质」一级节点。v2.0 仅最小字段，v3+ 扩展钻孔 / 地层模型。
    /// </summary>
    public sealed class Geology
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; }

        /// <summary>地质要素种类。</summary>
        public GeologyKind Kind { get; set; } = GeologyKind.Custom;

        public override string ToString()
            => $"Geology[{Name}, Id={Id:N}, Kind={Kind}]";
    }

    /// <summary>地质要素种类。</summary>
    public enum GeologyKind
    {
        Custom = 0,
        /// <summary>钻孔。</summary>
        BoreHole = 1,
        /// <summary>地层。</summary>
        Stratum = 2
    }
}
