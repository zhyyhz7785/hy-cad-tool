using HyCAD.Tables.Data;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Formulas;

/// <summary>
/// 全表公式依赖重算服务（Excel 风格）。
/// </summary>
public static class FormulaService
{
    /// <summary>
    /// 重算整张表中所有公式格，结果写入 <see cref="CellValue.Text"/>。
    /// </summary>
    public static TableGrid Recalculate(TableGrid grid)
    {
        var formulaCells = new List<(CellAddr Addr, CellValue Value, FormulaNode Node, List<CellAddr> References)>();
        foreach (var entry in grid.Data.Cells)
        {
            if (entry.Value.Kind != CellValueKind.Formula
                || string.IsNullOrEmpty(entry.Value.Formula))
            {
                continue;
            }

            var formulaText = entry.Value.Formula!;
            FormulaNode node;
            try
            {
                node = FormulaEvaluator.Parse(formulaText);
            }
            catch (FormulaException)
            {
                formulaCells.Add((
                    entry.Key,
                    entry.Value,
                    InvalidFormulaNode.Instance,
                    new List<CellAddr>()));
                continue;
            }

            var references = new List<CellAddr>();
            node.CollectReferences(references);
            formulaCells.Add((entry.Key, entry.Value, node, references));
        }

        if (formulaCells.Count == 0)
            return grid;

        var addrSet = new HashSet<CellAddr>();
        foreach (var item in formulaCells)
            addrSet.Add(item.Addr);

        var dependencies = new Dictionary<CellAddr, HashSet<CellAddr>>();
        foreach (var item in formulaCells)
        {
            var deps = new HashSet<CellAddr>();
            foreach (var reference in item.References)
            {
                if (addrSet.Contains(reference))
                    deps.Add(reference);
            }

            dependencies[item.Addr] = deps;
        }

        var order = TopologicalSort(formulaCells.Select(x => x.Addr).ToList(), dependencies, out var cyclic);
        var computed = new Dictionary<CellAddr, FormulaResult>();
        var resultGrid = grid;

        foreach (var addr in cyclic)
            computed[addr] = FormulaResult.FromError(FormulaErrorCode.Circular);

        foreach (var addr in order)
        {
            if (computed.ContainsKey(addr))
                continue;

            var item = formulaCells.First(x => x.Addr == addr);
            var context = new GridFormulaContext(resultGrid, computed);
            var evalResult = item.Node.Evaluate(context);
            computed[addr] = evalResult;
            resultGrid = WriteFormulaResult(resultGrid, item.Addr, item.Value, evalResult);
        }

        foreach (var addr in cyclic)
        {
            if (computed.TryGetValue(addr, out var evalResult))
            {
                var item = formulaCells.First(x => x.Addr == addr);
                resultGrid = WriteFormulaResult(resultGrid, item.Addr, item.Value, evalResult);
            }
        }

        return resultGrid;
    }

    private static TableGrid WriteFormulaResult(
        TableGrid grid,
        CellAddr addr,
        CellValue original,
        FormulaResult result)
    {
        var updated = original with { Text = result.ToDisplay() };
        return GridEditor.SetValue(grid, addr, updated);
    }

    private static List<CellAddr> TopologicalSort(
        IReadOnlyList<CellAddr> nodes,
        Dictionary<CellAddr, HashSet<CellAddr>> dependencies,
        out HashSet<CellAddr> cyclic)
    {
        cyclic = new HashSet<CellAddr>();
        var inDegree = nodes.ToDictionary(n => n, n => dependencies[n].Count);
        var reverse = new Dictionary<CellAddr, List<CellAddr>>();
        foreach (var node in nodes)
            reverse[node] = new List<CellAddr>();

        foreach (var entry in dependencies)
        {
            foreach (var dep in entry.Value)
            {
                if (!reverse.ContainsKey(dep))
                    reverse[dep] = new List<CellAddr>();
                reverse[dep].Add(entry.Key);
            }
        }

        var queue = new Queue<CellAddr>(inDegree.Where(x => x.Value == 0).Select(x => x.Key));
        var order = new List<CellAddr>();

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            order.Add(node);
            foreach (var dependent in reverse[node])
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                    queue.Enqueue(dependent);
            }
        }

        if (order.Count < nodes.Count)
        {
            foreach (var node in nodes)
            {
                if (!order.Contains(node))
                    cyclic.Add(node);
            }
        }

        return order;
    }

    private sealed class GridFormulaContext : IFormulaContext
    {
        private readonly TableGrid _grid;
        private readonly IReadOnlyDictionary<CellAddr, FormulaResult> _computed;

        public GridFormulaContext(TableGrid grid, IReadOnlyDictionary<CellAddr, FormulaResult> computed)
        {
            _grid = grid;
            _computed = computed;
        }

        public double GetNumber(CellAddr addr)
        {
            var anchor = _grid.Structure.GetAnchorOf(addr);
            if (_computed.TryGetValue(anchor, out var computed))
            {
                if (computed.IsError)
                    throw new FormulaException(computed.Error);
                return computed.Value;
            }

            if (!_grid.Data.Cells.TryGetValue(anchor, out var cell))
                return 0;

            if (cell.Kind == CellValueKind.Formula)
            {
                if (FormulaError.TryParseDisplay(cell.Text, out var code))
                    throw new FormulaException(code);
                return ParseNumber(cell.Text);
            }

            if (FormulaError.TryParseDisplay(cell.Text, out var errorCode))
                throw new FormulaException(errorCode);

            return ParseNumber(cell.Text);
        }

        public bool TryGetNumber(CellAddr addr, out double value)
        {
            value = 0;
            var anchor = _grid.Structure.GetAnchorOf(addr);

            if (_computed.TryGetValue(anchor, out var computed))
            {
                if (computed.IsError)
                    return false;
                value = computed.Value;
                return true;
            }

            if (!_grid.Data.Cells.TryGetValue(anchor, out var cell))
                return false;

            if (cell.Kind == CellValueKind.Formula)
            {
                if (FormulaError.TryParseDisplay(cell.Text, out _))
                    return false;
                return TryParseNumber(cell.Text, out value);
            }

            if (FormulaError.TryParseDisplay(cell.Text, out _))
                return false;

            return TryParseNumber(cell.Text, out value);
        }

        private static double ParseNumber(string text)
        {
            if (TryParseNumber(text, out var value))
                return value;
            throw new FormulaException(FormulaErrorCode.Value);
        }

        private static bool TryParseNumber(string text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            return double.TryParse(text.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value);
        }
    }
}
