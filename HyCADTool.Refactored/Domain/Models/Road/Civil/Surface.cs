using System;

namespace HyCADTool.Refactored.Domain.Models.Road.Civil
{
    /// <summary>
    /// 地形曲面（045 / M2 占位）。
    ///
    /// 用于支撑项目树中「曲面 Surfaces」节点；v2.0 仅持久化最小字段，
    /// v2.1+ 才会接入 TIN / EG 采样 / 体积对比。
    /// 与 <see cref="RoadDesign.Surfaces"/> 搭配。
    /// </summary>
    public sealed class Surface
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>显示名，如 "初设参考地形"。</summary>
        public string Name { get; set; }

        /// <summary>曲面种类（EG 原地面 / FG 设计完工面 / TIN 三角网 / Custom 其它）。</summary>
        public SurfaceKind Kind { get; set; } = SurfaceKind.Custom;

        /// <summary>备注。</summary>
        public string Note { get; set; }

        public override string ToString() => $"Surface[{Name}, Id={Id:N}, Kind={Kind}]";
    }

    /// <summary>曲面种类。</summary>
    public enum SurfaceKind
    {
        Custom = 0,
        /// <summary>Existing Ground 原地面。</summary>
        ExistingGround = 1,
        /// <summary>Finished Ground 完工面（路面顶面等）。</summary>
        FinishedGround = 2,
        /// <summary>三角网。</summary>
        TIN = 3
    }
}
