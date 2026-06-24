using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Formulas;

internal static class FormulaFunctions
{
    private static readonly Dictionary<string, double> Constants =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["PI"] = Math.PI,
            ["E"] = Math.E
        };

    public static bool TryGetConstant(string name, out double value) =>
        Constants.TryGetValue(name, out value);

    public static FormulaResult Invoke(
        string name,
        IReadOnlyList<FormulaArg> args,
        IFormulaContext? context)
    {
        switch (name.ToUpperInvariant())
        {
            case "SUM":
                return Aggregate(args, context, values =>
                {
                    var sum = 0.0;
                    foreach (var value in values)
                        sum += value;
                    return FormulaResult.FromValue(sum);
                });
            case "AVERAGE":
                return Aggregate(args, context, values =>
                {
                    var list = values.ToList();
                    if (list.Count == 0)
                        return FormulaResult.FromError(FormulaErrorCode.DivByZero);
                    return FormulaResult.FromValue(list.Average());
                });
            case "COUNT":
                return Aggregate(args, context, values =>
                    FormulaResult.FromValue(values.Count()));
            case "MIN":
                return Aggregate(args, context, values =>
                {
                    var list = values.ToList();
                    if (list.Count == 0)
                        return FormulaResult.FromValue(0);
                    return FormulaResult.FromValue(list.Min());
                });
            case "MAX":
                return Aggregate(args, context, values =>
                {
                    var list = values.ToList();
                    if (list.Count == 0)
                        return FormulaResult.FromValue(0);
                    return FormulaResult.FromValue(list.Max());
                });
            case "PRODUCT":
                return Aggregate(args, context, values =>
                {
                    var product = 1.0;
                    foreach (var value in values)
                        product *= value;
                    return FormulaResult.FromValue(product);
                });
        }

        var scalars = RequireScalars(args, context);
        return name.ToUpperInvariant() switch
        {
            "SIN" => FormulaMath.FromDouble(Math.Sin(scalars[0])),
            "COS" => FormulaMath.FromDouble(Math.Cos(scalars[0])),
            "TAN" => FormulaMath.FromDouble(Math.Tan(scalars[0])),
            "ASIN" => FormulaMath.FromDouble(Math.Asin(scalars[0])),
            "ACOS" => FormulaMath.FromDouble(Math.Acos(scalars[0])),
            "ATAN" => FormulaMath.FromDouble(Math.Atan(scalars[0])),
            "ATAN2" => FormulaMath.FromDouble(Math.Atan2(scalars[0], scalars[1])),
            "SINH" => FormulaMath.FromDouble(Math.Sinh(scalars[0])),
            "COSH" => FormulaMath.FromDouble(Math.Cosh(scalars[0])),
            "TANH" => FormulaMath.FromDouble(Math.Tanh(scalars[0])),
            "EXP" => FormulaMath.FromDouble(Math.Exp(scalars[0])),
            "LN" => LogNatural(scalars[0]),
            "LOG10" => LogBase(scalars[0], 10),
            "LOG" => scalars.Count >= 2
                ? LogBase(scalars[0], scalars[1])
                : LogBase(scalars[0], 10),
            "SQRT" => FormulaMath.FromDouble(Math.Sqrt(scalars[0])),
            "ABS" => FormulaResult.FromValue(Math.Abs(scalars[0])),
            "SIGN" => FormulaResult.FromValue(Math.Sign(scalars[0])),
            "ROUND" => FormulaResult.FromValue(
                Math.Round(scalars[0], scalars.Count >= 2 ? (int)scalars[1] : 0)),
            "FLOOR" => FormulaResult.FromValue(Math.Floor(scalars[0])),
            "CEILING" => FormulaResult.FromValue(Math.Ceiling(scalars[0])),
            "TRUNC" => FormulaResult.FromValue(Math.Truncate(scalars[0])),
            "MOD" => Mod(scalars[0], scalars[1]),
            "POWER" => FormulaMath.FromDouble(Math.Pow(scalars[0], scalars[1])),
            "FACT" => Fact(scalars[0]),
            "DEGREES" => FormulaResult.FromValue(scalars[0] * 180.0 / Math.PI),
            "RADIANS" => FormulaResult.FromValue(scalars[0] * Math.PI / 180.0),
            _ => FormulaResult.FromError(FormulaErrorCode.Name)
        };
    }

    private static FormulaResult LogNatural(double value)
    {
        if (value <= 0)
            return FormulaResult.FromError(FormulaErrorCode.Num);

        return FormulaMath.FromDouble(Math.Log(value));
    }

    private static FormulaResult LogBase(double value, double logBase)
    {
        if (value <= 0 || logBase <= 0 || Math.Abs(logBase - 1) < 1e-15)
            return FormulaResult.FromError(FormulaErrorCode.Num);

        return FormulaMath.FromDouble(Math.Log(value, logBase));
    }

    private static FormulaResult Mod(double dividend, double divisor)
    {
        if (Math.Abs(divisor) < 1e-15)
            return FormulaResult.FromError(FormulaErrorCode.DivByZero);

        return FormulaMath.FromDouble(dividend % divisor);
    }

    private static FormulaResult Aggregate(
        IReadOnlyList<FormulaArg> args,
        IFormulaContext? context,
        Func<IEnumerable<double>, FormulaResult> aggregate)
    {
        if (context == null)
            return FormulaResult.FromError(FormulaErrorCode.Ref);

        var values = new List<double>();
        foreach (var arg in args)
        {
            if (arg.IsError)
                return FormulaResult.FromError(arg.Error);

            if (arg.IsScalar)
            {
                if (arg.Scalar.IsError)
                    return arg.Scalar;
                values.Add(arg.Scalar.GetValueOrThrow());
                continue;
            }

            foreach (var addr in ExpandRange(arg.RangeFrom, arg.RangeTo))
            {
                if (context.TryGetNumber(addr, out var value))
                    values.Add(value);
            }
        }

        return aggregate(values);
    }

    private static List<double> RequireScalars(IReadOnlyList<FormulaArg> args, IFormulaContext? context)
    {
        if (args.Any(arg => arg.IsRange))
            throw new FormulaException(FormulaErrorCode.Value);

        var values = new List<double>(args.Count);
        foreach (var arg in args)
        {
            if (arg.IsError)
                throw new FormulaException(arg.Error);

            if (!arg.IsScalar)
                throw new FormulaException(FormulaErrorCode.Value);

            values.Add(arg.Scalar.GetValueOrThrow());
        }

        return values;
    }

    private static FormulaResult Fact(double value)
    {
        if (value < 0 || Math.Abs(value - Math.Round(value)) > 1e-12)
            return FormulaResult.FromError(FormulaErrorCode.Num);

        var n = (int)Math.Round(value);
        double result = 1;
        for (var i = 2; i <= n; i++)
            result *= i;
        return FormulaMath.FromDouble(result);
    }

    private static IEnumerable<CellAddr> ExpandRange(CellAddr from, CellAddr to)
    {
        var top = Math.Min(from.Row, to.Row);
        var bottom = Math.Max(from.Row, to.Row);
        var left = Math.Min(from.Col, to.Col);
        var right = Math.Max(from.Col, to.Col);
        for (var row = top; row <= bottom; row++)
        {
            for (var col = left; col <= right; col++)
                yield return new CellAddr(row, col);
        }
    }
}
