namespace HyCAD.Tables;

/// <summary>
/// 表格来源信息（文件路径、CAD 句柄、模板 ID 等）。
/// </summary>
public sealed record TableSourceInfo(
    TableSourceKind Kind,
    string? Location = null);

/// <summary>
/// 表格来源种类。
/// </summary>
public enum TableSourceKind
{
    /// <summary>未知或未指定。</summary>
    Unknown,

    /// <summary>外部文件（Word/Excel/JSON 等）。</summary>
    File,

    /// <summary>AutoCAD 图元（handle / XData）。</summary>
    CadEntity,

    /// <summary>内置或用户模板。</summary>
    Template
}
