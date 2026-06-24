using System.Globalization;

namespace HyCAD.Tables.Formulas;

/// <summary>
/// 公式求值结果（数值或 Excel 错误）。
/// </summary>
public readonly struct FormulaResult
{
    public bool IsError { get; }
    public double Value { get; }
    public FormulaErrorCode Error { get; }

    private FormulaResult(double value)
    {
        IsError = false;
        Value = value;
        Error = default;
    }

    private FormulaResult(FormulaErrorCode error)
    {
        IsError = true;
        Value = 0;
        Error = error;
    }

    public static FormulaResult FromValue(double value) => new(value);

    public static FormulaResult FromError(FormulaErrorCode code) => new(code);

    public string ToDisplay()
    {
        if (IsError)
            return FormulaError.ToDisplay(Error);

        if (double.IsNaN(Value) || double.IsInfinity(Value))
            return FormulaError.ToDisplay(FormulaErrorCode.Num);

        if (Math.Abs(Value - Math.Round(Value)) < 1e-12)
            return Math.Round(Value).ToString(CultureInfo.InvariantCulture);

        return Value.ToString("G15", CultureInfo.InvariantCulture);
    }

    public double GetValueOrThrow()
    {
        if (IsError)
            throw new FormulaException(Error);
        return Value;
    }
}
