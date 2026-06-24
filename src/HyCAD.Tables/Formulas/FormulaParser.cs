using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Formulas;

internal sealed class FormulaParser
{
    private readonly FormulaTokenizer _tokenizer;
    private FormulaToken _current;

    public FormulaParser(FormulaTokenizer tokenizer)
    {
        _tokenizer = tokenizer;
        _current = _tokenizer.Next();
    }

    public FormulaNode ParseExpression() => ParseAdditive();

    public static FormulaNode Parse(string formula)
    {
        var parser = new FormulaParser(new FormulaTokenizer(formula));
        var node = parser.ParseExpression();
        if (parser._current.Kind != FormulaTokenKind.End)
            throw new FormulaException(FormulaErrorCode.Value);
        return node;
    }

    private FormulaNode ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (_current.Kind == FormulaTokenKind.Operator
               && (_current.Text == "+" || _current.Text == "-"))
        {
            var op = _current.Text;
            Advance();
            left = new BinaryNode(op, left, ParseMultiplicative());
        }

        return left;
    }

    private FormulaNode ParseMultiplicative()
    {
        var left = ParseUnary();
        while (_current.Kind == FormulaTokenKind.Operator
               && (_current.Text == "*" || _current.Text == "/"))
        {
            var op = _current.Text;
            Advance();
            left = new BinaryNode(op, left, ParseUnary());
        }

        return left;
    }

    private FormulaNode ParseUnary()
    {
        if (_current.Kind == FormulaTokenKind.Operator && _current.Text == "+")
        {
            Advance();
            return ParseUnary();
        }

        if (_current.Kind == FormulaTokenKind.Operator && _current.Text == "-")
        {
            Advance();
            return new UnaryNode("-", ParseUnary());
        }

        return ParsePower();
    }

    private FormulaNode ParsePower()
    {
        var left = ParsePostfix();
        if (_current.Kind == FormulaTokenKind.Operator && _current.Text == "^")
        {
            Advance();
            return new BinaryNode("^", left, ParseUnary());
        }

        return left;
    }

    private FormulaNode ParsePostfix()
    {
        var node = ParsePrimary();
        while (true)
        {
            if (_current.Kind == FormulaTokenKind.Bang)
            {
                Advance();
                node = new FactorialNode(node);
                continue;
            }

            if (_current.Kind == FormulaTokenKind.Percent)
            {
                Advance();
                node = new PercentNode(node);
                continue;
            }

            break;
        }

        return node;
    }

    private FormulaNode ParsePrimary()
    {
        switch (_current.Kind)
        {
            case FormulaTokenKind.Number:
            {
                var value = _current.Number;
                Advance();
                return new NumberNode(value);
            }
            case FormulaTokenKind.Identifier:
                return ParseIdentifierOrFunction();
            case FormulaTokenKind.CellRef:
                return ParseCellOrRange();
            case FormulaTokenKind.LParen:
            {
                Advance();
                var inner = ParseExpression();
                Expect(FormulaTokenKind.RParen);
                return inner;
            }
            default:
                throw new FormulaException(FormulaErrorCode.Value);
        }
    }

    private FormulaNode ParseIdentifierOrFunction()
    {
        var name = _current.Text;
        Advance();

        if (_current.Kind == FormulaTokenKind.LParen)
        {
            Advance();
            var args = ParseFunctionArgs();
            Expect(FormulaTokenKind.RParen);
            return new FunctionNode(name, args);
        }

        if (FormulaFunctions.TryGetConstant(name, out var constant))
            return new NumberNode(constant);

        throw new FormulaException(FormulaErrorCode.Name);
    }

    private FormulaNode ParseCellOrRange()
    {
        if (!CellReference.TryParseA1(_current.Text, out var from))
            throw new FormulaException(FormulaErrorCode.Ref);

        Advance();
        if (_current.Kind == FormulaTokenKind.Colon)
        {
            Advance();
            if (_current.Kind != FormulaTokenKind.CellRef
                || !CellReference.TryParseA1(_current.Text, out var to))
            {
                throw new FormulaException(FormulaErrorCode.Ref);
            }

            Advance();
            return new RangeNode(from, to);
        }

        return new CellRefNode(from);
    }

    private List<FormulaArgNode> ParseFunctionArgs()
    {
        var args = new List<FormulaArgNode>();
        if (_current.Kind == FormulaTokenKind.RParen)
            return args;

        args.Add(ParseFunctionArg());
        while (_current.Kind == FormulaTokenKind.Comma)
        {
            Advance();
            args.Add(ParseFunctionArg());
        }

        return args;
    }

    private FormulaArgNode ParseFunctionArg()
    {
        if (_current.Kind == FormulaTokenKind.CellRef)
        {
            if (!CellReference.TryParseA1(_current.Text, out var from))
                throw new FormulaException(FormulaErrorCode.Ref);

            Advance();
            if (_current.Kind == FormulaTokenKind.Colon)
            {
                Advance();
                if (_current.Kind != FormulaTokenKind.CellRef
                    || !CellReference.TryParseA1(_current.Text, out var to))
                {
                    throw new FormulaException(FormulaErrorCode.Ref);
                }

                Advance();
                return new RangeArgNode(from, to);
            }

            return new ScalarArgNode(new CellRefNode(from));
        }

        return new ScalarArgNode(ParseExpression());
    }

    private void Expect(FormulaTokenKind kind)
    {
        if (_current.Kind != kind)
            throw new FormulaException(FormulaErrorCode.Value);
        Advance();
    }

    private void Advance() => _current = _tokenizer.Next();
}
