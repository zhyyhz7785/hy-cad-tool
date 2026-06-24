namespace HyCAD.Tables.Formulas;

internal enum FormulaTokenKind
{
    Number,
    Operator,
    Percent,
    Bang,
    LParen,
    RParen,
    Comma,
    Colon,
    Identifier,
    CellRef,
    End
}

internal readonly struct FormulaToken
{
    public FormulaTokenKind Kind { get; }
    public string Text { get; }
    public double Number { get; }

    public FormulaToken(FormulaTokenKind kind, string text = "", double number = 0)
    {
        Kind = kind;
        Text = text;
        Number = number;
    }
}
