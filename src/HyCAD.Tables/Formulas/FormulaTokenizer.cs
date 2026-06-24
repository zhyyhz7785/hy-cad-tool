using System.Globalization;

namespace HyCAD.Tables.Formulas;

internal sealed class FormulaTokenizer
{
    private readonly string _text;
    private int _index;

    public FormulaTokenizer(string text)
    {
        _text = text ?? string.Empty;
    }

    public FormulaToken Next()
    {
        SkipWhitespace();
        if (_index >= _text.Length)
            return new FormulaToken(FormulaTokenKind.End);

        var ch = _text[_index];
        switch (ch)
        {
            case '+':
            case '-':
            case '*':
            case '/':
            case '^':
                _index++;
                return new FormulaToken(FormulaTokenKind.Operator, ch.ToString());
            case '%':
                _index++;
                return new FormulaToken(FormulaTokenKind.Percent);
            case '!':
                _index++;
                return new FormulaToken(FormulaTokenKind.Bang);
            case '(':
                _index++;
                return new FormulaToken(FormulaTokenKind.LParen);
            case ')':
                _index++;
                return new FormulaToken(FormulaTokenKind.RParen);
            case ',':
                _index++;
                return new FormulaToken(FormulaTokenKind.Comma);
            case ':':
                _index++;
                return new FormulaToken(FormulaTokenKind.Colon);
        }

        if (char.IsDigit(ch) || ch == '.')
            return ReadNumber();

        if (char.IsLetter(ch))
            return ReadIdentifierOrCellRef();

        throw new FormulaException(FormulaErrorCode.Value);
    }

    public FormulaToken Peek()
    {
        var saved = _index;
        var token = Next();
        _index = saved;
        return token;
    }

    private void SkipWhitespace()
    {
        while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
            _index++;
    }

    private FormulaToken ReadNumber()
    {
        var start = _index;
        if (_index < _text.Length && _text[_index] == '.')
            _index++;

        while (_index < _text.Length && char.IsDigit(_text[_index]))
            _index++;

        if (_index < _text.Length && _text[_index] == '.')
        {
            _index++;
            while (_index < _text.Length && char.IsDigit(_text[_index]))
                _index++;
        }

        if (_index < _text.Length && (_text[_index] == 'e' || _text[_index] == 'E'))
        {
            _index++;
            if (_index < _text.Length && (_text[_index] == '+' || _text[_index] == '-'))
                _index++;
            while (_index < _text.Length && char.IsDigit(_text[_index]))
                _index++;
        }

        var numberText = _text.Substring(start, _index - start);
        if (!double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            throw new FormulaException(FormulaErrorCode.Value);

        return new FormulaToken(FormulaTokenKind.Number, numberText, value);
    }

    private FormulaToken ReadIdentifierOrCellRef()
    {
        var start = _index;
        while (_index < _text.Length && char.IsLetter(_text[_index]))
            _index++;

        var letters = _text.Substring(start, _index - start);
        var digitStart = _index;
        while (_index < _text.Length && char.IsDigit(_text[_index]))
            _index++;

        if (digitStart < _index)
            return new FormulaToken(FormulaTokenKind.CellRef, _text.Substring(start, _index - start));

        return new FormulaToken(FormulaTokenKind.Identifier, letters);
    }
}
