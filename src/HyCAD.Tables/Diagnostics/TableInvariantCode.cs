namespace HyCAD.Tables.Diagnostics;

/// <summary>
/// 表格不变量违例代码（Table Invariant Violation Code）。
/// </summary>
public enum TableInvariantCode
{
    /// <summary>行列数无效（RowCount/ColCount &lt; 1）。</summary>
    InvalidDimensions,

    /// <summary>CellAddr 超出网格范围。</summary>
    AddressOutOfBounds,

    /// <summary>合并区 RowSpan/ColSpan 无效（&lt; 1）。</summary>
    InvalidMergeSpan,

    /// <summary>合并区超出网格边界。</summary>
    MergeOutOfBounds,

    /// <summary>两个合并区相交重叠。</summary>
    MergeOverlap,

    /// <summary>斜线落在被合并覆盖的非 Anchor 格。</summary>
    DiagonalOnCoveredCell,

    /// <summary>FieldKey 重复。</summary>
    DuplicateFieldKey,

    /// <summary>Kind=Formula 但 Formula 为空。</summary>
    FormulaMissing,

    /// <summary>Kind=Bound 但 BindingExpr 为空。</summary>
    BindingMissing
}
