using System;

namespace HyCADTool.Domain.ValueObjects.Road
{
    /// <summary>
    /// 平面线位（Alignment）的"工程默认值"。
    /// 设计意图：让 hyRoadAlnByPi 等创建命令在交互最初就给出贴近本项目实际的回车默认值，
    /// 而不是写死的 R=30 / Ls=0。用户在 hyRoadAlnDefaults 调一次就持久化到 hy-settings.json。
    ///
    /// 字段语义与 <see cref="PiElement"/> 一致；StartStation 是 BP 的桩号偏移（米）。
    /// </summary>
    public sealed class AlignmentDefaults
    {
        /// <summary>内部 PI 默认半径 R（米，0 表示折线）。</summary>
        public double DefaultRadius { get; set; } = 30.0;

        /// <summary>默认入侧缓和曲线长 Ls_in（米，0=无）。</summary>
        public double DefaultSpiralIn { get; set; } = 0.0;

        /// <summary>默认出侧缓和曲线长 Ls_out（米，0=无）。</summary>
        public double DefaultSpiralOut { get; set; } = 0.0;

        /// <summary>默认起点桩号（米）；常用 0 / 1000 等。</summary>
        public double DefaultStartStation { get; set; } = 0.0;

        /// <summary>克隆出独立副本（避免共享引用）。</summary>
        public AlignmentDefaults Clone() => new AlignmentDefaults
        {
            DefaultRadius = DefaultRadius,
            DefaultSpiralIn = DefaultSpiralIn,
            DefaultSpiralOut = DefaultSpiralOut,
            DefaultStartStation = DefaultStartStation,
        };

        /// <summary>
        /// 校验：禁止负数；半径仅约束 ≥ 0；缓和长仅约束 ≥ 0；
        /// 起桩负值允许（理论上 BP 可置于负向链桩，例如旧线复测）。
        /// </summary>
        public void Validate()
        {
            if (DefaultRadius < 0) throw new ArgumentOutOfRangeException(nameof(DefaultRadius), "半径不能为负。");
            if (DefaultSpiralIn < 0) throw new ArgumentOutOfRangeException(nameof(DefaultSpiralIn), "入侧缓和长不能为负。");
            if (DefaultSpiralOut < 0) throw new ArgumentOutOfRangeException(nameof(DefaultSpiralOut), "出侧缓和长不能为负。");
        }
    }
}
