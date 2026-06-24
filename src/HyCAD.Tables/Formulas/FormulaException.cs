namespace HyCAD.Tables.Formulas;

/// <summary>
/// 公式求值内部异常（边界处转为 <see cref="FormulaResult"/>）。
/// </summary>
public sealed class FormulaException : Exception
{
    public FormulaException(FormulaErrorCode code)
        : base(FormulaError.ToDisplay(code))
    {
        Code = code;
    }

    public FormulaErrorCode Code { get; }
}
