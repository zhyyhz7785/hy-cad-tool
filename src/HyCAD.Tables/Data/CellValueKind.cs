namespace HyCAD.Tables.Data;

/// <summary>
/// 单元格值种类（Cell Value Kind）。
/// </summary>
public enum CellValueKind
{
    /// <summary>静态文本/数字。</summary>
    Static,

    /// <summary>公式（Formula）。</summary>
    Formula,

    /// <summary>外部绑定（Bound）。</summary>
    Bound
}
