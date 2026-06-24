using System.Globalization;
using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Formulas;

/// <summary>
/// Excel A1 单元格引用格式转换。
/// </summary>
public static class CellReference
{
    public static string FormatA1(CellAddr addr) =>
        FormatColumn(addr.Col) + (addr.Row + 1).ToString(CultureInfo.InvariantCulture);

    public static bool TryParseA1(string text, out CellAddr addr)
    {
        addr = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        var index = 0;
        while (index < text.Length && char.IsLetter(text[index]))
            index++;

        if (index == 0 || index >= text.Length)
            return false;

        var colText = text.Substring(0, index);
        var rowText = text.Substring(index);
        if (!int.TryParse(rowText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rowOneBased))
            return false;
        if (rowOneBased < 1)
            return false;

        var col = ParseColumn(colText);
        if (col < 0)
            return false;

        addr = new CellAddr(rowOneBased - 1, col);
        return true;
    }

    public static string FormatColumn(int col)
    {
        if (col < 0)
            return string.Empty;

        var letters = new char[8];
        var length = 0;
        var value = col + 1;
        while (value > 0)
        {
            value--;
            letters[length++] = (char)('A' + value % 26);
            value /= 26;
        }

        Array.Reverse(letters, 0, length);
        return new string(letters, 0, length);
    }

    public static int ParseColumn(string letters)
    {
        if (string.IsNullOrEmpty(letters))
            return -1;

        var value = 0;
        foreach (var ch in letters)
        {
            if (!char.IsLetter(ch))
                return -1;

            value = value * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        }

        return value - 1;
    }
}
