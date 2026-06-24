namespace HyCAD.Tables.Formulas;

internal static class FormulaMath
{
    public static FormulaResult FromDouble(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return FormulaResult.FromError(FormulaErrorCode.Num);

        return FormulaResult.FromValue(value);
    }
}
