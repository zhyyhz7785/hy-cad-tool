namespace HyCAD.Tables.Structure;

/// <summary>
/// 网格轨道尺寸模式（Grid Track Size Mode）。
/// </summary>
public enum TrackSizeMode
{
    /// <summary>固定尺寸（mm）。</summary>
    Fixed,

    /// <summary>按内容自动。</summary>
    Auto,

    /// <summary>权重分配剩余空间。</summary>
    Weight
}
