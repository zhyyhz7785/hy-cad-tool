namespace HyCAD.Tables.Formulas;

/// <summary>
/// 公式错误显示映射。
/// </summary>
public static class FormulaError
{
    public static string ToDisplay(FormulaErrorCode code) =>
        code switch
        {
            FormulaErrorCode.DivByZero => "#DIV/0!",
            FormulaErrorCode.Value => "#VALUE!",
            FormulaErrorCode.Name => "#NAME?",
            FormulaErrorCode.Ref => "#REF!",
            FormulaErrorCode.Num => "#NUM!",
            FormulaErrorCode.NA => "#N/A",
            FormulaErrorCode.Circular => "#CIRCULAR!",
            _ => "#VALUE!"
        };

    public static bool TryParseDisplay(string text, out FormulaErrorCode code)
    {
        code = FormulaErrorCode.Value;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        switch (text.Trim().ToUpperInvariant())
        {
            case "#DIV/0!":
                code = FormulaErrorCode.DivByZero;
                return true;
            case "#VALUE!":
                code = FormulaErrorCode.Value;
                return true;
            case "#NAME?":
                code = FormulaErrorCode.Name;
                return true;
            case "#REF!":
                code = FormulaErrorCode.Ref;
                return true;
            case "#NUM!":
                code = FormulaErrorCode.Num;
                return true;
            case "#N/A":
                code = FormulaErrorCode.NA;
                return true;
            case "#CIRCULAR!":
                code = FormulaErrorCode.Circular;
                return true;
            default:
                return false;
        }
    }
}
