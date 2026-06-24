namespace HyCAD.Tables.Structure;

/// <summary>
/// 单元格角色（Cell Role）。
/// </summary>
public enum CellRole
{
    /// <summary>标题行。</summary>
    Title,

    /// <summary>表头。</summary>
    Header,

    /// <summary>标签格（label）。</summary>
    Label,

    /// <summary>值格（value）。</summary>
    Value,

    /// <summary>照片占位格。</summary>
    PhotoSlot,

    /// <summary>布局占位，无业务语义。</summary>
    Spacer
}
