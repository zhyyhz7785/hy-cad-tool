namespace HyCAD.Tables.Structure;

/// <summary>
/// 合并区域值策略（Merge Value Policy）。
/// </summary>
public enum MergeValuePolicy
{
    /// <summary>仅 Anchor 有值，其余为空（Excel 默认行为）。</summary>
    AnchorOnly,

    /// <summary>全体镜像 Anchor 值（拆分后内容不丢）。</summary>
    SameValue,

    /// <summary>各格保留原值，仅显示 Anchor（复杂数据保真）。</summary>
    PreserveEach,

    /// <summary>显示时组合多格值（人事/说明表）。</summary>
    Composite
}
