namespace HyCAD.Tables.Formulas;

/// <summary>
/// 公式求值器（Tokenizer → Parser → AST → Evaluator）。
/// </summary>
public static class FormulaEvaluator
{
    /// <summary>
    /// 求值公式文本（可含前导 =）。
    /// </summary>
    public static FormulaResult Evaluate(string formula, IFormulaContext? context = null)
    {
        if (string.IsNullOrWhiteSpace(formula))
            return FormulaResult.FromError(FormulaErrorCode.Value);

        var text = formula.Trim();
        if (text.StartsWith("=", StringComparison.Ordinal))
            text = text.Substring(1);

        try
        {
            var node = FormulaParser.Parse(text);
            return node.Evaluate(context);
        }
        catch (FormulaException ex)
        {
            return FormulaResult.FromError(ex.Code);
        }
    }

    internal static FormulaNode Parse(string formula)
    {
        var text = formula.Trim();
        if (text.StartsWith("=", StringComparison.Ordinal))
            text = text.Substring(1);
        return FormulaParser.Parse(text);
    }

    internal static IReadOnlyList<HyCAD.Tables.Structure.CellAddr> CollectReferences(FormulaNode node)
    {
        var references = new List<HyCAD.Tables.Structure.CellAddr>();
        node.CollectReferences(references);
        return references;
    }
}
