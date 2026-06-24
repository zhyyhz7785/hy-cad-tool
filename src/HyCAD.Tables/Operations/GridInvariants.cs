using HyCAD.Tables.Data;
using HyCAD.Tables.Diagnostics;
using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Operations;

/// <summary>
/// 表格不变量校验器（Grid Invariants）。
/// 只报告违例，不抛异常，不自动修复。
/// </summary>
public static class GridInvariants
{
    /// <summary>校验整张表。</summary>
    public static TableValidationReport Validate(TableGrid grid) =>
        Validate(grid.Structure, grid.Data);

    /// <summary>校验结构层与数据层。</summary>
    public static TableValidationReport Validate(GridStructure structure, GridData data)
    {
        var violations = new List<TableViolation>();
        var topology = structure.Topology;
        var rowCount = topology.RowCount;
        var colCount = topology.ColCount;

        ValidateBaseGrid(rowCount, colCount, violations);
        ValidateAddressCollections(structure, data, rowCount, colCount, violations);
        ValidateMerges(structure.Merges, rowCount, colCount, violations);
        ValidateDiagonals(structure, violations);
        ValidateFieldKeys(structure, violations);
        ValidateCellValues(structure, data, violations);

        return violations.Count == 0
            ? TableValidationReport.Valid
            : new TableValidationReport(false, violations);
    }

    private static void ValidateBaseGrid(int rowCount, int colCount, List<TableViolation> violations)
    {
        if (rowCount >= 1 && colCount >= 1)
            return;

        violations.Add(new TableViolation(
            TableInvariantCode.InvalidDimensions,
            $"网格尺寸无效：RowCount={rowCount}, ColCount={colCount}，均须 >= 1。"));
    }

    private static void ValidateAddressCollections(
        GridStructure structure,
        GridData data,
        int rowCount,
        int colCount,
        List<TableViolation> violations)
    {
        if (rowCount < 1 || colCount < 1)
            return;

        foreach (var addr in structure.Diagonals.Keys)
            CheckInBounds(addr, rowCount, colCount, "Diagonals", violations);

        foreach (var addr in structure.Styles.Keys)
            CheckInBounds(addr, rowCount, colCount, "Styles", violations);

        foreach (var addr in structure.Roles.Keys)
            CheckInBounds(addr, rowCount, colCount, "Roles", violations);

        foreach (var addr in structure.FieldIndex.Values)
            CheckInBounds(addr, rowCount, colCount, "FieldIndex", violations);

        foreach (var addr in data.Cells.Keys)
            CheckInBounds(addr, rowCount, colCount, "Data.Cells", violations);
    }

    private static void ValidateMerges(
        IReadOnlyList<MergeRegion> merges,
        int rowCount,
        int colCount,
        List<TableViolation> violations)
    {
        if (rowCount < 1 || colCount < 1)
            return;

        for (var i = 0; i < merges.Count; i++)
        {
            var merge = merges[i];

            if (merge.RowSpan < 1 || merge.ColSpan < 1)
            {
                violations.Add(new TableViolation(
                    TableInvariantCode.InvalidMergeSpan,
                    $"合并区 #{i} 的 RowSpan/ColSpan 无效：RowSpan={merge.RowSpan}, ColSpan={merge.ColSpan}。",
                    merge.TopLeft));
                continue;
            }

            var endRow = merge.TopLeft.Row + merge.RowSpan;
            var endCol = merge.TopLeft.Col + merge.ColSpan;

            if (merge.TopLeft.Row < 0
                || merge.TopLeft.Col < 0
                || endRow > rowCount
                || endCol > colCount)
            {
                violations.Add(new TableViolation(
                    TableInvariantCode.MergeOutOfBounds,
                    $"合并区 #{i} 超出网格：TopLeft={merge.TopLeft}, RowSpan={merge.RowSpan}, ColSpan={merge.ColSpan}。",
                    merge.TopLeft));
            }
        }

        for (var i = 0; i < merges.Count; i++)
        {
            for (var j = i + 1; j < merges.Count; j++)
            {
                if (merges[i].Intersects(merges[j]))
                {
                    violations.Add(new TableViolation(
                        TableInvariantCode.MergeOverlap,
                        $"合并区 #{i} 与 #{j} 相交重叠。",
                        merges[i].TopLeft));
                }
            }
        }
    }

    private static void ValidateDiagonals(GridStructure structure, List<TableViolation> violations)
    {
        foreach (var entry in structure.Diagonals)
        {
            var addr = entry.Key;
            foreach (var merge in structure.Merges)
            {
                if (merge.Covers(addr) && addr != merge.Anchor)
                {
                    violations.Add(new TableViolation(
                        TableInvariantCode.DiagonalOnCoveredCell,
                        $"斜线格 {addr} 位于合并区 Anchor={merge.Anchor} 的被覆盖格上。",
                        addr));
                }
            }
        }
    }

    private static void ValidateFieldKeys(GridStructure structure, List<TableViolation> violations)
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var key in structure.FieldIndex.Keys)
            RegisterFieldKey(seen, key, "FieldIndex", violations);

        foreach (var entry in structure.Diagonals)
        {
            var addr = entry.Key;
            var split = entry.Value;
            foreach (var part in split.Parts)
            {
                if (string.IsNullOrEmpty(part.FieldKey))
                    continue;

                RegisterFieldKey(seen, part.FieldKey!, $"Diagonal({addr})", violations);
            }
        }
    }

    private static void ValidateCellValues(
        GridStructure structure,
        GridData data,
        List<TableViolation> violations)
    {
        foreach (var entry in data.Cells)
            CheckCellValue(entry.Value, $"Data[{entry.Key}]", entry.Key, violations);

        foreach (var entry in structure.Diagonals)
        {
            var addr = entry.Key;
            var split = entry.Value;
            for (var i = 0; i < split.Parts.Count; i++)
            {
                var part = split.Parts[i];
                CheckCellValue(part.Value, $"Diagonal({addr}).Parts[{i}]", addr, violations);
            }
        }
    }

    private static void CheckCellValue(
        CellValue value,
        string context,
        CellAddr? cell,
        List<TableViolation> violations)
    {
        if (value.Kind == CellValueKind.Formula && string.IsNullOrEmpty(value.Formula))
        {
            violations.Add(new TableViolation(
                TableInvariantCode.FormulaMissing,
                $"{context}：Kind=Formula 但 Formula 为空。",
                cell));
        }

        if (value.Kind == CellValueKind.Bound && string.IsNullOrEmpty(value.BindingExpr))
        {
            violations.Add(new TableViolation(
                TableInvariantCode.BindingMissing,
                $"{context}：Kind=Bound 但 BindingExpr 为空。",
                cell));
        }
    }

    private static void RegisterFieldKey(
        Dictionary<string, string> seen,
        string key,
        string context,
        List<TableViolation> violations)
    {
        if (seen.TryGetValue(key, out var firstContext))
        {
            violations.Add(new TableViolation(
                TableInvariantCode.DuplicateFieldKey,
                $"FieldKey \"{key}\" 重复：首次出现在 {firstContext}，再次出现在 {context}。"));
            return;
        }

        seen[key] = context;
    }

    private static void CheckInBounds(
        CellAddr addr,
        int rowCount,
        int colCount,
        string context,
        List<TableViolation> violations)
    {
        if (InBounds(addr, rowCount, colCount))
            return;

        violations.Add(new TableViolation(
            TableInvariantCode.AddressOutOfBounds,
            $"{context} 的地址 {addr} 超出网格 [0,{rowCount})×[0,{colCount})。",
            addr));
    }

    private static bool InBounds(CellAddr addr, int rowCount, int colCount) =>
        addr.Row >= 0
        && addr.Row < rowCount
        && addr.Col >= 0
        && addr.Col < colCount;
}
