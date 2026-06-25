using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Layout;

/// <summary>
/// 可见单元格布局信息（行优先枚举项）。
/// </summary>
/// <param name="Addr">单元格地址。</param>
/// <param name="Bounds">外接矩形（mm）。</param>
/// <param name="IsMergeAnchor">是否为合并区 Anchor。</param>
/// <param name="RowSpan">跨行数。</param>
/// <param name="ColSpan">跨列数。</param>
/// <param name="Merge">所属合并区；未合并时为 null。</param>
public readonly record struct VisibleCellLayout(
    CellAddr Addr,
    LayoutRect Bounds,
    bool IsMergeAnchor,
    int RowSpan,
    int ColSpan,
    MergeRegion? Merge);
