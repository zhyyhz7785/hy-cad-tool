namespace HyCAD.Tables.Structure;

/// <summary>
/// 表格结构层（模板：拓扑、合并、斜线、样式、角色、FieldIndex）。
/// </summary>
public sealed record GridStructure(
    GridTopology Topology,
    IReadOnlyList<MergeRegion> Merges,
    IReadOnlyDictionary<CellAddr, DiagonalSplit> Diagonals,
    IReadOnlyDictionary<CellAddr, CellStyle> Styles,
    IReadOnlyDictionary<CellAddr, CellRole> Roles,
    IReadOnlyDictionary<string, CellAddr> FieldIndex)
{
    /// <summary>
    /// 创建空覆盖层的结构（仅拓扑）。
    /// </summary>
    public static GridStructure CreateEmpty(GridTopology topology) =>
        new(
            topology,
            Array.Empty<MergeRegion>(),
            new Dictionary<CellAddr, DiagonalSplit>(),
            new Dictionary<CellAddr, CellStyle>(),
            new Dictionary<CellAddr, CellRole>(),
            new Dictionary<string, CellAddr>());

    /// <summary>
    /// 查找覆盖指定地址的合并区。
    /// </summary>
    public bool TryGetMergeAt(CellAddr addr, out MergeRegion region)
    {
        foreach (var merge in Merges)
        {
            if (merge.Covers(addr))
            {
                region = merge;
                return true;
            }
        }

        region = default!;
        return false;
    }

    /// <summary>
    /// 地址是否落在某合并区内（含 Anchor）。
    /// </summary>
    public bool IsCovered(CellAddr addr) => TryGetMergeAt(addr, out _);

    /// <summary>
    /// 地址是否被合并隐藏（被覆盖且非 Anchor）。
    /// </summary>
    public bool IsHidden(CellAddr addr) =>
        TryGetMergeAt(addr, out var merge) && addr != merge.Anchor;

    /// <summary>
    /// 获取地址对应的 Anchor；未覆盖时返回自身。
    /// </summary>
    public CellAddr GetAnchorOf(CellAddr addr) =>
        TryGetMergeAt(addr, out var merge) ? merge.Anchor : addr;
}
