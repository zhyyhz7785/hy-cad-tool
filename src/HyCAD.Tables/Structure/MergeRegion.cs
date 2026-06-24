namespace HyCAD.Tables.Structure;

/// <summary>
/// 矩形合并区域（Merge Region），不删除底格。
/// </summary>
/// <param name="TopLeft">左上角 Anchor 格。</param>
/// <param name="RowSpan">跨行数。</param>
/// <param name="ColSpan">跨列数。</param>
/// <param name="ValuePolicy">合并区值策略。</param>
public sealed record MergeRegion(
    CellAddr TopLeft,
    int RowSpan,
    int ColSpan,
    MergeValuePolicy ValuePolicy = MergeValuePolicy.AnchorOnly)
{
    /// <summary>主格（= TopLeft）。</summary>
    public CellAddr Anchor => TopLeft;

    /// <summary>
    /// 判断地址是否落在本合并矩形内（半开区间 [TopLeft, TopLeft+Span)）。
    /// </summary>
    public bool Covers(CellAddr addr) =>
        addr.Row >= TopLeft.Row
        && addr.Row < TopLeft.Row + RowSpan
        && addr.Col >= TopLeft.Col
        && addr.Col < TopLeft.Col + ColSpan;

    /// <summary>
    /// 判断两合并矩形是否相交（相邻不算相交，半开区间）。
    /// </summary>
    public bool Intersects(MergeRegion other)
    {
        var aEndRow = TopLeft.Row + RowSpan;
        var aEndCol = TopLeft.Col + ColSpan;
        var bEndRow = other.TopLeft.Row + other.RowSpan;
        var bEndCol = other.TopLeft.Col + other.ColSpan;

        return TopLeft.Row < bEndRow
            && other.TopLeft.Row < aEndRow
            && TopLeft.Col < bEndCol
            && other.TopLeft.Col < aEndCol;
    }
}
