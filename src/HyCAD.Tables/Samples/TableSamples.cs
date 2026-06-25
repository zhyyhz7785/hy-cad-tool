using HyCAD.Tables.Data;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Samples;

/// <summary>
/// P8 黄金样表构造（家庭成员表 + 人员基本情况表），供单测与 AutoCAD 渲染联调共用。
/// </summary>
public static class TableSamples
{
    /// <summary>家庭成员表（7×6）：表头 + colspan + 6 行数据 + FieldKey。</summary>
    public static TableGrid BuildFamilyTable()
    {
        var grid = TableGrid.CreateEmpty(7, 6);

        grid = GridEditor.SetValue(grid, new CellAddr(0, 0), new CellValue("称谓"));
        grid = GridEditor.SetValue(grid, new CellAddr(0, 1), new CellValue("姓名"));
        grid = GridEditor.SetValue(grid, new CellAddr(0, 2), new CellValue("年龄"));
        grid = GridEditor.SetValue(grid, new CellAddr(0, 3), new CellValue("政治面貌"));
        grid = GridEditor.Merge(grid, new CellAddr(0, 4), rowSpan: 1, colSpan: 2);
        grid = GridEditor.SetValue(grid, new CellAddr(0, 4), new CellValue("工作单位及职务"));

        grid = WithRoles(
            grid,
            (new CellAddr(0, 0), CellRole.Header),
            (new CellAddr(0, 1), CellRole.Header),
            (new CellAddr(0, 2), CellRole.Header),
            (new CellAddr(0, 3), CellRole.Header),
            (new CellAddr(0, 4), CellRole.Header));

        for (var row = 1; row <= 6; row++)
        {
            grid = GridEditor.SetValue(grid, new CellAddr(row, 0), new CellValue("父亲"));
            grid = GridEditor.SetFieldKey(grid, new CellAddr(row, 1), $"member{row - 1}_name");
            grid = GridEditor.SetValueByField(grid, $"member{row - 1}_name", new CellValue($"成员{row}"));
            grid = GridEditor.SetValue(grid, new CellAddr(row, 2), new CellValue($"{40 + row}"));
            grid = GridEditor.SetValue(grid, new CellAddr(row, 3), new CellValue("群众"));
            grid = GridEditor.SetValue(grid, new CellAddr(row, 4), new CellValue($"单位{row}"));
        }

        return ApplyFamilyTableBorders(grid);
    }

    /// <summary>斜线验收微夹具（2×2，Dev 命令用）。</summary>
    public static TableGrid BuildDiagonalDemoTable()
    {
        var addr = new CellAddr(0, 0);
        return GridEditor.SplitDiagonal(
            TableGrid.CreateEmpty(2, 2),
            addr,
            DiagonalDirection.BackSlashTLBR,
            new[]
            {
                new SubCell(new CellValue("学历")),
                new SubCell(new CellValue("学位")),
            });
    }

    /// <summary>人员基本情况表（7×7）：Title/PhotoSlot/竖排/colspan/FieldKey name。</summary>
    public static TableGrid BuildPersonnelTable()
    {
        var grid = TableGrid.CreateEmpty(7, 7);

        grid = GridEditor.Merge(grid, new CellAddr(0, 0), rowSpan: 1, colSpan: 7);
        grid = GridEditor.SetValue(grid, new CellAddr(0, 0), new CellValue("人员基本情况表"));
        grid = WithRoles(grid, (new CellAddr(0, 0), CellRole.Title));

        grid = GridEditor.Merge(grid, new CellAddr(1, 6), rowSpan: 3, colSpan: 1);
        grid = WithRoles(grid, (new CellAddr(1, 6), CellRole.PhotoSlot));

        grid = GridEditor.SetValue(grid, new CellAddr(1, 0), new CellValue("姓名"));
        grid = GridEditor.SetFieldKey(grid, new CellAddr(1, 1), "name");
        grid = GridEditor.SetValueByField(grid, "name", new CellValue("张三"));
        grid = GridEditor.SetValue(grid, new CellAddr(1, 2), new CellValue("性别"));
        grid = GridEditor.SetValue(grid, new CellAddr(1, 3), new CellValue("男"));
        grid = GridEditor.SetValue(grid, new CellAddr(1, 4), new CellValue("出生年月"));
        grid = GridEditor.SetValue(grid, new CellAddr(1, 5), new CellValue("1990-01"));

        grid = WithRoles(
            grid,
            (new CellAddr(1, 0), CellRole.Label),
            (new CellAddr(1, 1), CellRole.Value),
            (new CellAddr(1, 2), CellRole.Label),
            (new CellAddr(1, 3), CellRole.Value),
            (new CellAddr(1, 4), CellRole.Label),
            (new CellAddr(1, 5), CellRole.Value));

        grid = GridEditor.SetValue(grid, new CellAddr(3, 0), new CellValue("籍贯"));
        grid = GridEditor.Merge(grid, new CellAddr(3, 1), rowSpan: 1, colSpan: 3);
        grid = GridEditor.SetValue(grid, new CellAddr(3, 1), new CellValue("北京市"));
        grid = WithRoles(
            grid,
            (new CellAddr(3, 0), CellRole.Label),
            (new CellAddr(3, 1), CellRole.Value));

        grid = GridEditor.Merge(grid, new CellAddr(4, 0), rowSpan: 3, colSpan: 1);
        grid = GridEditor.SetValue(grid, new CellAddr(4, 0), new CellValue("家庭主要成员及重要社会关系"));
        grid = WithStyles(
            grid,
            (new CellAddr(4, 0), new CellStyle(Orientation: TextOrientation.VerticalStacked)));
        grid = WithRoles(grid, (new CellAddr(4, 0), CellRole.Header));

        grid = GridEditor.SetValue(grid, new CellAddr(5, 1), new CellValue("学习和工作简历"));
        grid = GridEditor.Merge(grid, new CellAddr(5, 2), rowSpan: 1, colSpan: 5);
        grid = GridEditor.SetValue(grid, new CellAddr(5, 2), new CellValue("2008-2012 某大学；2012-至今 某单位"));
        grid = WithRoles(
            grid,
            (new CellAddr(5, 1), CellRole.Label),
            (new CellAddr(5, 2), CellRole.Value));

        return grid;
    }

    private static TableGrid WithRoles(TableGrid grid, params (CellAddr Addr, CellRole Role)[] roles)
    {
        var dict = grid.Structure.Roles.ToDictionary(entry => entry.Key, entry => entry.Value);
        foreach (var (addr, role) in roles)
            dict[addr] = role;
        return grid with { Structure = grid.Structure with { Roles = dict } };
    }

    private static TableGrid WithStyles(TableGrid grid, params (CellAddr Addr, CellStyle Style)[] styles)
    {
        var dict = grid.Structure.Styles.ToDictionary(entry => entry.Key, entry => entry.Value);
        foreach (var (addr, style) in styles)
            dict[addr] = style;
        return grid with { Structure = grid.Structure with { Styles = dict } };
    }

    private static TableGrid WithTopology(TableGrid grid, GridTopology topology) =>
        grid with { Structure = grid.Structure with { Topology = topology } };

    private static TableGrid ApplyFamilyTableBorders(TableGrid grid)
    {
        const double inner = 0.35;
        const double outer = 0.53;

        grid = WithTopology(
            grid,
            grid.Structure.Topology with { DefaultBorder = BorderSet.Uniform(inner) });

        var rowCount = grid.Structure.Topology.RowCount;
        var colCount = grid.Structure.Topology.ColCount;
        var lastRow = rowCount - 1;
        var lastCol = colCount - 1;

        var edgeStyles = new List<(CellAddr Addr, CellStyle Style)>();

        for (var row = 0; row < rowCount; row++)
        {
            for (var col = 0; col < colCount; col++)
            {
                if (grid.Structure.IsHidden(new CellAddr(row, col)))
                    continue;

                var top = row == 0 ? outer : 0;
                var bottom = row == lastRow ? outer : 0;
                var left = col == 0 ? outer : 0;
                var right = col == lastCol ? outer : 0;

                if (top == 0 && bottom == 0 && left == 0 && right == 0)
                    continue;

                edgeStyles.Add((
                    new CellAddr(row, col),
                    new CellStyle(Borders: new BorderSet(top, right, bottom, left))));
            }
        }

        return WithStyles(grid, edgeStyles.ToArray());
    }
}
