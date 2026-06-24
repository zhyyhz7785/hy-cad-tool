using HyCAD.Tables.Data;
using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Operations;

/// <summary>
/// 表格编辑器（Grid Editor）：结构操作、填值、FieldKey 维护。
/// 不可变函数式：每次操作返回新 <see cref="TableGrid"/>。
/// </summary>
public static class GridEditor
{
    /// <summary>
    /// 合并矩形区域。AnchorOnly 仅改覆盖层；SameValue 合并时镜像 Anchor 现值到全体 Member。
    /// </summary>
    public static TableGrid Merge(
        TableGrid grid,
        CellAddr topLeft,
        int rowSpan,
        int colSpan,
        MergeValuePolicy policy = MergeValuePolicy.AnchorOnly)
    {
        var structure = grid.Structure;
        var topology = structure.Topology;

        if (rowSpan < 1 || colSpan < 1)
            throw new ArgumentOutOfRangeException(
                rowSpan < 1 ? nameof(rowSpan) : nameof(colSpan),
                "RowSpan 与 ColSpan 均须 >= 1。");

        if (rowSpan == 1 && colSpan == 1)
            throw new InvalidOperationException("1×1 合并无意义。");

        if (policy is MergeValuePolicy.PreserveEach or MergeValuePolicy.Composite)
            throw new NotSupportedException($"合并策略 {policy} 尚未支持（MVP 仅 AnchorOnly / SameValue）。");

        if (!InBounds(topLeft, topology.RowCount, topology.ColCount))
            throw new ArgumentOutOfRangeException(nameof(topLeft), $"合并 Anchor {topLeft} 超出网格。");

        var endRow = topLeft.Row + rowSpan;
        var endCol = topLeft.Col + colSpan;
        if (endRow > topology.RowCount || endCol > topology.ColCount)
            throw new InvalidOperationException(
                $"合并区超出网格：TopLeft={topLeft}, RowSpan={rowSpan}, ColSpan={colSpan}。");

        var newRegion = new MergeRegion(topLeft, rowSpan, colSpan, policy);
        foreach (var existing in structure.Merges)
        {
            if (existing.Intersects(newRegion))
                throw new InvalidOperationException(
                    $"新合并区与现有合并区 Anchor={existing.Anchor} 相交重叠。");
        }

        var merges = new List<MergeRegion>(structure.Merges.Count + 1);
        merges.AddRange(structure.Merges);
        merges.Add(newRegion);

        var newStructure = WithMerges(structure, merges);

        if (policy == MergeValuePolicy.AnchorOnly)
            return grid with { Structure = newStructure };

        var cells = CloneCells(grid.Data.Cells);
        grid.Data.Cells.TryGetValue(topLeft, out var anchorValue);
        MirrorRegion(cells, newRegion, IsEmptyValue(anchorValue) ? null : anchorValue);

        return grid with
        {
            Structure = newStructure,
            Data = new GridData(cells)
        };
    }

    /// <summary>
    /// 拆分合并区（按 Anchor 移除 MergeRegion，底格数据原样保留）。
    /// </summary>
    public static TableGrid Unmerge(TableGrid grid, CellAddr anchor)
    {
        var structure = grid.Structure;
        var index = FindMergeIndexByAnchor(structure.Merges, anchor);
        if (index < 0)
            throw new InvalidOperationException($"未找到 Anchor={anchor} 的合并区。");

        var merges = new List<MergeRegion>(structure.Merges);
        merges.RemoveAt(index);

        return grid with { Structure = WithMerges(structure, merges) };
    }

    /// <summary>
    /// 在指定格添加斜线分割。
    /// </summary>
    public static TableGrid SplitDiagonal(
        TableGrid grid,
        CellAddr addr,
        DiagonalDirection direction,
        IReadOnlyList<SubCell>? parts = null)
    {
        var structure = grid.Structure;
        var topology = structure.Topology;

        if (!InBounds(addr, topology.RowCount, topology.ColCount))
            throw new ArgumentOutOfRangeException(nameof(addr), $"地址 {addr} 超出网格。");

        if (structure.IsHidden(addr))
            throw new InvalidOperationException($"地址 {addr} 为被合并覆盖的非 Anchor 格，不能添加斜线。");

        if (structure.Diagonals.ContainsKey(addr))
            throw new InvalidOperationException($"地址 {addr} 已有斜线，请先 ClearDiagonal。");

        var subCells = parts ?? CreateDefaultSubCells();
        if (subCells.Count == 0)
            throw new ArgumentException("斜线子格不能为空。", nameof(parts));

        var split = new DiagonalSplit(direction, subCells);
        var diagonals = CloneDiagonals(structure.Diagonals);
        diagonals[addr] = split;

        return grid with { Structure = WithDiagonals(structure, diagonals) };
    }

    /// <summary>
    /// 在指定索引插入一行，并重映射全部 CellAddr 键。
    /// </summary>
    public static TableGrid InsertRow(TableGrid grid, int rowIndex)
    {
        var topology = grid.Structure.Topology;
        if (rowIndex < 0 || rowIndex > topology.RowCount)
            throw new ArgumentOutOfRangeException(nameof(rowIndex));

        var newTopology = topology.WithRowInserted(rowIndex);

        return Remap(
            grid,
            newTopology,
            addr => addr.Row >= rowIndex
                ? new CellAddr(addr.Row + 1, addr.Col)
                : addr,
            merge => MapMergeInsertRow(merge, rowIndex));
    }

    /// <summary>
    /// 在指定索引插入一列，并重映射全部 CellAddr 键。
    /// </summary>
    public static TableGrid InsertColumn(TableGrid grid, int colIndex)
    {
        var topology = grid.Structure.Topology;
        if (colIndex < 0 || colIndex > topology.ColCount)
            throw new ArgumentOutOfRangeException(nameof(colIndex));

        var newTopology = topology.WithColumnInserted(colIndex);

        return Remap(
            grid,
            newTopology,
            addr => addr.Col >= colIndex
                ? new CellAddr(addr.Row, addr.Col + 1)
                : addr,
            merge => MapMergeInsertColumn(merge, colIndex));
    }

    /// <summary>
    /// 删除指定行，并重映射全部 CellAddr 键。
    /// </summary>
    public static TableGrid DeleteRow(TableGrid grid, int rowIndex)
    {
        var topology = grid.Structure.Topology;
        if (topology.RowCount <= 1)
            throw new InvalidOperationException("不能删除最后一行。");
        if (rowIndex < 0 || rowIndex >= topology.RowCount)
            throw new ArgumentOutOfRangeException(nameof(rowIndex));

        var newTopology = topology.WithRowRemoved(rowIndex);

        return Remap(
            grid,
            newTopology,
            addr =>
            {
                if (addr.Row == rowIndex)
                    return null;
                return addr.Row > rowIndex
                    ? new CellAddr(addr.Row - 1, addr.Col)
                    : addr;
            },
            merge => MapMergeDeleteRow(merge, rowIndex));
    }

    /// <summary>
    /// 删除指定列，并重映射全部 CellAddr 键。
    /// </summary>
    public static TableGrid DeleteColumn(TableGrid grid, int colIndex)
    {
        var topology = grid.Structure.Topology;
        if (topology.ColCount <= 1)
            throw new InvalidOperationException("不能删除最后一列。");
        if (colIndex < 0 || colIndex >= topology.ColCount)
            throw new ArgumentOutOfRangeException(nameof(colIndex));

        var newTopology = topology.WithColumnRemoved(colIndex);

        return Remap(
            grid,
            newTopology,
            addr =>
            {
                if (addr.Col == colIndex)
                    return null;
                return addr.Col > colIndex
                    ? new CellAddr(addr.Row, addr.Col - 1)
                    : addr;
            },
            merge => MapMergeDeleteColumn(merge, colIndex));
    }

    /// <summary>
    /// 移除指定格的斜线分割。
    /// </summary>
    public static TableGrid ClearDiagonal(TableGrid grid, CellAddr addr)
    {
        var structure = grid.Structure;
        if (!structure.Diagonals.ContainsKey(addr))
            throw new InvalidOperationException($"地址 {addr} 没有斜线。");

        var diagonals = CloneDiagonals(structure.Diagonals);
        diagonals.Remove(addr);

        return grid with { Structure = WithDiagonals(structure, diagonals) };
    }

    /// <summary>
    /// 写入单元格值（写覆盖格自动重定向到 Anchor；SameValue 合并区镜像到全体 Member）。
    /// </summary>
    public static TableGrid SetValue(TableGrid grid, CellAddr addr, CellValue value)
    {
        var structure = grid.Structure;
        var topology = structure.Topology;

        if (!InBounds(addr, topology.RowCount, topology.ColCount))
            throw new ArgumentOutOfRangeException(nameof(addr), $"地址 {addr} 超出网格。");

        var anchor = structure.GetAnchorOf(addr);
        var cells = CloneCells(grid.Data.Cells);
        ApplyValueToAnchor(cells, structure, anchor, value);

        return grid with { Data = new GridData(cells) };
    }

    /// <summary>
    /// 按 FieldKey 写入值（先解析 FieldIndex）。
    /// </summary>
    public static TableGrid SetValueByField(TableGrid grid, string fieldKey, CellValue value)
    {
        if (string.IsNullOrEmpty(fieldKey))
            throw new ArgumentException("FieldKey 不能为空。", nameof(fieldKey));

        if (!grid.Structure.FieldIndex.TryGetValue(fieldKey, out var addr))
            throw new InvalidOperationException($"未找到 FieldKey \"{fieldKey}\"。");

        return SetValue(grid, addr, value);
    }

    /// <summary>
    /// 维护 FieldIndex 绑定（一格至多一个 key；重复 key 指向不同格将被拒绝）。
    /// </summary>
    public static TableGrid SetFieldKey(TableGrid grid, CellAddr addr, string? fieldKey)
    {
        var structure = grid.Structure;
        var topology = structure.Topology;

        if (!InBounds(addr, topology.RowCount, topology.ColCount))
            throw new ArgumentOutOfRangeException(nameof(addr), $"地址 {addr} 超出网格。");

        var anchor = structure.GetAnchorOf(addr);
        var fieldIndex = CloneFieldIndex(structure.FieldIndex);

        string? existingKeyForAnchor = null;
        foreach (var entry in fieldIndex)
        {
            if (entry.Value == anchor)
            {
                existingKeyForAnchor = entry.Key;
                break;
            }
        }

        if (string.IsNullOrEmpty(fieldKey))
        {
            if (existingKeyForAnchor != null)
                fieldIndex.Remove(existingKeyForAnchor);

            return grid with { Structure = structure with { FieldIndex = fieldIndex } };
        }

        var key = fieldKey!; // 已通过 IsNullOrEmpty 校验
        if (fieldIndex.TryGetValue(key, out var existingAddr) && existingAddr != anchor)
            throw new InvalidOperationException(
                $"FieldKey \"{key}\" 已绑定到 {existingAddr}，不能重复绑定到 {anchor}。");

        if (existingKeyForAnchor != null && existingKeyForAnchor != key)
            fieldIndex.Remove(existingKeyForAnchor);

        fieldIndex[key] = anchor;

        return grid with { Structure = structure with { FieldIndex = fieldIndex } };
    }

    /// <summary>
    /// 读取单元格值（读合并区任意成员均返回 Anchor 值）。
    /// </summary>
    public static CellValue GetValue(TableGrid grid, CellAddr addr)
    {
        var structure = grid.Structure;
        var topology = structure.Topology;

        if (!InBounds(addr, topology.RowCount, topology.ColCount))
            throw new ArgumentOutOfRangeException(nameof(addr), $"地址 {addr} 超出网格。");

        var anchor = structure.GetAnchorOf(addr);
        return grid.Data.Cells.TryGetValue(anchor, out var value) ? value : CellValue.Empty;
    }

    /// <summary>
    /// 按 FieldKey 读取值。
    /// </summary>
    public static CellValue GetValueByField(TableGrid grid, string fieldKey)
    {
        if (string.IsNullOrEmpty(fieldKey))
            throw new ArgumentException("FieldKey 不能为空。", nameof(fieldKey));

        if (!grid.Structure.FieldIndex.TryGetValue(fieldKey, out var addr))
            throw new InvalidOperationException($"未找到 FieldKey \"{fieldKey}\"。");

        return GetValue(grid, addr);
    }

    private static int FindMergeIndexByAnchor(IReadOnlyList<MergeRegion> merges, CellAddr anchor)
    {
        for (var i = 0; i < merges.Count; i++)
        {
            if (merges[i].Anchor == anchor)
                return i;
        }

        return -1;
    }

    private static bool InBounds(CellAddr addr, int rowCount, int colCount) =>
        addr.Row >= 0
        && addr.Row < rowCount
        && addr.Col >= 0
        && addr.Col < colCount;

    private static IReadOnlyList<SubCell> CreateDefaultSubCells() =>
        new[]
        {
            new SubCell(CellValue.Empty),
            new SubCell(CellValue.Empty)
        };

    private static Dictionary<CellAddr, DiagonalSplit> CloneDiagonals(
        IReadOnlyDictionary<CellAddr, DiagonalSplit> source)
    {
        var clone = new Dictionary<CellAddr, DiagonalSplit>(source.Count);
        foreach (var entry in source)
            clone[entry.Key] = entry.Value;
        return clone;
    }

    private static GridStructure WithMerges(GridStructure structure, IReadOnlyList<MergeRegion> merges) =>
        structure with { Merges = merges };

    private static GridStructure WithDiagonals(
        GridStructure structure,
        IReadOnlyDictionary<CellAddr, DiagonalSplit> diagonals) =>
        structure with { Diagonals = diagonals };

    private static TableGrid Remap(
        TableGrid grid,
        GridTopology newTopology,
        Func<CellAddr, CellAddr?> mapAddr,
        Func<MergeRegion, MergeRegion?> mapMerge)
    {
        var structure = grid.Structure;

        var merges = new List<MergeRegion>(structure.Merges.Count);
        foreach (var merge in structure.Merges)
        {
            var mapped = mapMerge(merge);
            if (mapped != null)
                merges.Add(mapped);
        }

        var diagonals = RemapKeys(structure.Diagonals, mapAddr);
        var styles = RemapKeys(structure.Styles, mapAddr);
        var roles = RemapKeys(structure.Roles, mapAddr);
        var fieldIndex = RemapFieldIndex(structure.FieldIndex, mapAddr);
        var dataCells = RemapKeys(grid.Data.Cells, mapAddr);

        var newStructure = structure with
        {
            Topology = newTopology,
            Merges = merges,
            Diagonals = diagonals,
            Styles = styles,
            Roles = roles,
            FieldIndex = fieldIndex
        };

        return grid with
        {
            Structure = newStructure,
            Data = new GridData(dataCells)
        };
    }

    private static Dictionary<CellAddr, T> RemapKeys<T>(
        IReadOnlyDictionary<CellAddr, T> source,
        Func<CellAddr, CellAddr?> map)
    {
        var result = new Dictionary<CellAddr, T>(source.Count);
        foreach (var entry in source)
        {
            var newKey = map(entry.Key);
            if (newKey != null)
                result[newKey.Value] = entry.Value;
        }

        return result;
    }

    private static Dictionary<string, CellAddr> RemapFieldIndex(
        IReadOnlyDictionary<string, CellAddr> source,
        Func<CellAddr, CellAddr?> map)
    {
        var result = new Dictionary<string, CellAddr>(source.Count);
        foreach (var entry in source)
        {
            var newAddr = map(entry.Value);
            if (newAddr != null)
                result[entry.Key] = newAddr.Value;
        }

        return result;
    }

    private static MergeRegion MapMergeInsertRow(MergeRegion merge, int rowIndex)
    {
        var top = merge.TopLeft.Row;
        var end = top + merge.RowSpan;
        var newTop = top >= rowIndex ? top + 1 : top;
        var newSpan = top < rowIndex && rowIndex < end ? merge.RowSpan + 1 : merge.RowSpan;
        return merge with
        {
            TopLeft = new CellAddr(newTop, merge.TopLeft.Col),
            RowSpan = newSpan
        };
    }

    private static MergeRegion MapMergeInsertColumn(MergeRegion merge, int colIndex)
    {
        var left = merge.TopLeft.Col;
        var end = left + merge.ColSpan;
        var newLeft = left >= colIndex ? left + 1 : left;
        var newSpan = left < colIndex && colIndex < end ? merge.ColSpan + 1 : merge.ColSpan;
        return merge with
        {
            TopLeft = new CellAddr(merge.TopLeft.Row, newLeft),
            ColSpan = newSpan
        };
    }

    private static MergeRegion? MapMergeDeleteRow(MergeRegion merge, int rowIndex)
    {
        var top = merge.TopLeft.Row;
        var end = top + merge.RowSpan;
        int newTop;
        int newSpan;
        if (rowIndex < top)
        {
            newTop = top - 1;
            newSpan = merge.RowSpan;
        }
        else if (rowIndex < end)
        {
            newTop = top;
            newSpan = merge.RowSpan - 1;
        }
        else
        {
            newTop = top;
            newSpan = merge.RowSpan;
        }

        if (newSpan < 1 || (newSpan == 1 && merge.ColSpan == 1))
            return null;

        return merge with
        {
            TopLeft = new CellAddr(newTop, merge.TopLeft.Col),
            RowSpan = newSpan
        };
    }

    private static MergeRegion? MapMergeDeleteColumn(MergeRegion merge, int colIndex)
    {
        var left = merge.TopLeft.Col;
        var end = left + merge.ColSpan;
        int newLeft;
        int newSpan;
        if (colIndex < left)
        {
            newLeft = left - 1;
            newSpan = merge.ColSpan;
        }
        else if (colIndex < end)
        {
            newLeft = left;
            newSpan = merge.ColSpan - 1;
        }
        else
        {
            newLeft = left;
            newSpan = merge.ColSpan;
        }

        if (newSpan < 1 || (merge.RowSpan == 1 && newSpan == 1))
            return null;

        return merge with
        {
            TopLeft = new CellAddr(merge.TopLeft.Row, newLeft),
            ColSpan = newSpan
        };
    }

    private static void ApplyValueToAnchor(
        Dictionary<CellAddr, CellValue> cells,
        GridStructure structure,
        CellAddr anchor,
        CellValue value)
    {
        if (IsEmptyValue(value))
        {
            cells.Remove(anchor);
            if (structure.TryGetMergeAt(anchor, out var merge)
                && merge.ValuePolicy == MergeValuePolicy.SameValue)
            {
                MirrorRegion(cells, merge, null);
            }

            return;
        }

        cells[anchor] = value;
        if (structure.TryGetMergeAt(anchor, out var sameValueMerge)
            && sameValueMerge.ValuePolicy == MergeValuePolicy.SameValue)
        {
            MirrorRegion(cells, sameValueMerge, value);
        }
    }

    private static void MirrorRegion(
        Dictionary<CellAddr, CellValue> cells,
        MergeRegion region,
        CellValue? value)
    {
        var endRow = region.TopLeft.Row + region.RowSpan;
        var endCol = region.TopLeft.Col + region.ColSpan;

        for (var row = region.TopLeft.Row; row < endRow; row++)
        {
            for (var col = region.TopLeft.Col; col < endCol; col++)
            {
                var addr = new CellAddr(row, col);
                if (value == null || IsEmptyValue(value))
                    cells.Remove(addr);
                else
                    cells[addr] = value;
            }
        }
    }

    private static bool IsEmptyValue(CellValue? value)
    {
        if (value == null)
            return true;

        if (value.Kind != CellValueKind.Static)
            return false;

        return string.IsNullOrEmpty(value.Text)
            && string.IsNullOrEmpty(value.Formula)
            && string.IsNullOrEmpty(value.BindingExpr)
            && (value.Runs == null || value.Runs.Count == 0);
    }

    private static Dictionary<CellAddr, CellValue> CloneCells(
        IReadOnlyDictionary<CellAddr, CellValue> source)
    {
        var clone = new Dictionary<CellAddr, CellValue>(source.Count);
        foreach (var entry in source)
            clone[entry.Key] = entry.Value;
        return clone;
    }

    private static Dictionary<string, CellAddr> CloneFieldIndex(
        IReadOnlyDictionary<string, CellAddr> source)
    {
        var clone = new Dictionary<string, CellAddr>(source.Count, StringComparer.Ordinal);
        foreach (var entry in source)
            clone[entry.Key] = entry.Value;
        return clone;
    }
}
