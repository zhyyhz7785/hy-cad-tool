using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Formulas;

internal abstract class FormulaNode
{
    public abstract FormulaResult Evaluate(IFormulaContext? context);
    public abstract void CollectReferences(List<CellAddr> references);
}

internal sealed class InvalidFormulaNode : FormulaNode
{
    public static InvalidFormulaNode Instance { get; } = new();

    public override FormulaResult Evaluate(IFormulaContext? context) =>
        FormulaResult.FromError(FormulaErrorCode.Value);

    public override void CollectReferences(List<CellAddr> references) { }
}

internal sealed class NumberNode : FormulaNode
{
    public NumberNode(double value) => Value = value;
    public double Value { get; }

    public override FormulaResult Evaluate(IFormulaContext? context) =>
        FormulaResult.FromValue(Value);

    public override void CollectReferences(List<CellAddr> references) { }
}

internal sealed class UnaryNode : FormulaNode
{
    public UnaryNode(string op, FormulaNode operand)
    {
        Operator = op;
        Operand = operand;
    }

    public string Operator { get; }
    public FormulaNode Operand { get; }

    public override FormulaResult Evaluate(IFormulaContext? context)
    {
        var operand = Operand.Evaluate(context);
        if (operand.IsError)
            return operand;

        return Operator switch
        {
            "+" => FormulaResult.FromValue(operand.Value),
            "-" => FormulaResult.FromValue(-operand.Value),
            _ => FormulaResult.FromError(FormulaErrorCode.Value)
        };
    }

    public override void CollectReferences(List<CellAddr> references) =>
        Operand.CollectReferences(references);
}

internal sealed class BinaryNode : FormulaNode
{
    public BinaryNode(string op, FormulaNode left, FormulaNode right)
    {
        Operator = op;
        Left = left;
        Right = right;
    }

    public string Operator { get; }
    public FormulaNode Left { get; }
    public FormulaNode Right { get; }

    public override FormulaResult Evaluate(IFormulaContext? context)
    {
        var left = Left.Evaluate(context);
        if (left.IsError)
            return left;

        var right = Right.Evaluate(context);
        if (right.IsError)
            return right;

        return Operator switch
        {
            "+" => FormulaMath.FromDouble(left.Value + right.Value),
            "-" => FormulaMath.FromDouble(left.Value - right.Value),
            "*" => FormulaMath.FromDouble(left.Value * right.Value),
            "/" => right.Value == 0
                ? FormulaResult.FromError(FormulaErrorCode.DivByZero)
                : FormulaMath.FromDouble(left.Value / right.Value),
            "^" => FormulaMath.FromDouble(Math.Pow(left.Value, right.Value)),
            _ => FormulaResult.FromError(FormulaErrorCode.Value)
        };
    }

    public override void CollectReferences(List<CellAddr> references)
    {
        Left.CollectReferences(references);
        Right.CollectReferences(references);
    }
}

internal sealed class PercentNode : FormulaNode
{
    public PercentNode(FormulaNode operand) => Operand = operand;
    public FormulaNode Operand { get; }

    public override FormulaResult Evaluate(IFormulaContext? context)
    {
        var operand = Operand.Evaluate(context);
        if (operand.IsError)
            return operand;

        return FormulaMath.FromDouble(operand.Value / 100.0);
    }

    public override void CollectReferences(List<CellAddr> references) =>
        Operand.CollectReferences(references);
}

internal sealed class FactorialNode : FormulaNode
{
    public FactorialNode(FormulaNode operand) => Operand = operand;
    public FormulaNode Operand { get; }

    public override FormulaResult Evaluate(IFormulaContext? context)
    {
        var operand = Operand.Evaluate(context);
        if (operand.IsError)
            return operand;

        var value = operand.Value;
        if (value < 0 || Math.Abs(value - Math.Round(value)) > 1e-12)
            return FormulaResult.FromError(FormulaErrorCode.Num);

        var n = (int)Math.Round(value);
        double result = 1;
        for (var i = 2; i <= n; i++)
            result *= i;
        return FormulaMath.FromDouble(result);
    }

    public override void CollectReferences(List<CellAddr> references) =>
        Operand.CollectReferences(references);
}

internal sealed class CellRefNode : FormulaNode
{
    public CellRefNode(CellAddr addr) => Address = addr;
    public CellAddr Address { get; }

    public override FormulaResult Evaluate(IFormulaContext? context)
    {
        if (context == null)
            return FormulaResult.FromError(FormulaErrorCode.Ref);

        try
        {
            return FormulaMath.FromDouble(context.GetNumber(Address));
        }
        catch (FormulaException ex)
        {
            return FormulaResult.FromError(ex.Code);
        }
    }

    public override void CollectReferences(List<CellAddr> references) =>
        references.Add(Address);
}

internal sealed class RangeNode : FormulaNode
{
    public RangeNode(CellAddr from, CellAddr to)
    {
        From = from;
        To = to;
    }

    public CellAddr From { get; }
    public CellAddr To { get; }

    public override FormulaResult Evaluate(IFormulaContext? context) =>
        throw new FormulaException(FormulaErrorCode.Value);

    public override void CollectReferences(List<CellAddr> references)
    {
        var top = Math.Min(From.Row, To.Row);
        var bottom = Math.Max(From.Row, To.Row);
        var left = Math.Min(From.Col, To.Col);
        var right = Math.Max(From.Col, To.Col);
        for (var row = top; row <= bottom; row++)
        {
            for (var col = left; col <= right; col++)
                references.Add(new CellAddr(row, col));
        }
    }
}

internal sealed class FunctionNode : FormulaNode
{
    public FunctionNode(string name, IReadOnlyList<FormulaArgNode> args)
    {
        Name = name;
        Args = args;
    }

    public string Name { get; }
    public IReadOnlyList<FormulaArgNode> Args { get; }

    public override FormulaResult Evaluate(IFormulaContext? context)
    {
        var args = new List<FormulaArg>(Args.Count);
        foreach (var arg in Args)
            args.Add(arg.EvaluateArg(context));

        return FormulaFunctions.Invoke(Name, args, context);
    }

    public override void CollectReferences(List<CellAddr> references)
    {
        foreach (var arg in Args)
            arg.CollectReferences(references);
    }
}

internal abstract class FormulaArgNode
{
    public abstract FormulaArg EvaluateArg(IFormulaContext? context);
    public abstract void CollectReferences(List<CellAddr> references);
}

internal sealed class ScalarArgNode : FormulaArgNode
{
    public ScalarArgNode(FormulaNode expression) => Expression = expression;
    public FormulaNode Expression { get; }

    public override FormulaArg EvaluateArg(IFormulaContext? context) =>
        FormulaArg.FromScalar(Expression.Evaluate(context));

    public override void CollectReferences(List<CellAddr> references) =>
        Expression.CollectReferences(references);
}

internal sealed class RangeArgNode : FormulaArgNode
{
    public RangeArgNode(CellAddr from, CellAddr to)
    {
        From = from;
        To = to;
    }

    public CellAddr From { get; }
    public CellAddr To { get; }

    public override FormulaArg EvaluateArg(IFormulaContext? context)
    {
        if (context == null)
            return FormulaArg.FromError(FormulaErrorCode.Ref);
        return FormulaArg.FromRange(From, To);
    }

    public override void CollectReferences(List<CellAddr> references)
    {
        var top = Math.Min(From.Row, To.Row);
        var bottom = Math.Max(From.Row, To.Row);
        var left = Math.Min(From.Col, To.Col);
        var right = Math.Max(From.Col, To.Col);
        for (var row = top; row <= bottom; row++)
        {
            for (var col = left; col <= right; col++)
                references.Add(new CellAddr(row, col));
        }
    }
}

internal readonly struct FormulaArg
{
    public bool IsScalar { get; }
    public bool IsRange { get; }
    public bool IsError { get; }
    public FormulaResult Scalar { get; }
    public CellAddr RangeFrom { get; }
    public CellAddr RangeTo { get; }
    public FormulaErrorCode Error { get; }

    private FormulaArg(FormulaResult scalar)
    {
        IsScalar = true;
        Scalar = scalar;
    }

    private FormulaArg(CellAddr from, CellAddr to)
    {
        IsRange = true;
        RangeFrom = from;
        RangeTo = to;
    }

    private FormulaArg(FormulaErrorCode error)
    {
        IsError = true;
        Error = error;
    }

    public static FormulaArg FromScalar(FormulaResult result) => new(result);
    public static FormulaArg FromRange(CellAddr from, CellAddr to) => new(from, to);
    public static FormulaArg FromError(FormulaErrorCode error) => new(error);
}
