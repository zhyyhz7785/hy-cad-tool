namespace HyCAD.Tables.Diagnostics;

/// <summary>
/// 表格校验报告（Table Validation Report）。
/// </summary>
/// <param name="IsValid">是否通过全部不变量。</param>
/// <param name="Violations">违例列表（可为空）。</param>
public sealed record TableValidationReport(
    bool IsValid,
    IReadOnlyList<TableViolation> Violations)
{
    /// <summary>无违例的有效报告。</summary>
    public static TableValidationReport Valid { get; } =
        new(true, Array.Empty<TableViolation>());
}
