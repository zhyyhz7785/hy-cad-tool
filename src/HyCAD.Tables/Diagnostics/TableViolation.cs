using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Diagnostics;

/// <summary>
/// 单条不变量违例（Table Invariant Violation）。
/// </summary>
/// <param name="Code">违例代码。</param>
/// <param name="Message">人类可读描述。</param>
/// <param name="Cell">相关单元格地址，可空。</param>
public sealed record TableViolation(
    TableInvariantCode Code,
    string Message,
    CellAddr? Cell = null);
