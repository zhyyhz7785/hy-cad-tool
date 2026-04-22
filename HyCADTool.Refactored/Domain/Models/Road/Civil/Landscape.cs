using System;

namespace HyCADTool.Refactored.Domain.Models.Road.Civil
{
    /// <summary>
    /// 配景（045 / M2 占位）。
    ///
    /// 对应用户草案图中「配景 → 过街设施 / 铁路 / 水域」—— 参照物而非设计对象，
    /// 让项目树保留"周边环境"上下文。v2.0 仅最小字段。
    /// </summary>
    public sealed class Landscape
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; }

        /// <summary>配景种类。</summary>
        public LandscapeKind Kind { get; set; } = LandscapeKind.Custom;

        public override string ToString()
            => $"Landscape[{Name}, Id={Id:N}, Kind={Kind}]";
    }

    /// <summary>配景种类。</summary>
    public enum LandscapeKind
    {
        Custom = 0,
        /// <summary>过街设施（人行天桥 / 地道）。</summary>
        Crossing = 1,
        /// <summary>铁路。</summary>
        Railway = 2,
        /// <summary>水域（河道 / 湖泊 / 池塘）。</summary>
        Water = 3
    }
}
