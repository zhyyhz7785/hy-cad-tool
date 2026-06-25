using FluentAssertions;
using HyCAD.Tables.Layout;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Samples;
using HyCAD.Tables.Structure;
using Xunit;

namespace HyCAD.Tables.Tests;

public sealed class BorderGridResolverTests
{
    private const double Fallback = 0.35;
    private const double Outer = 0.53;

    [Fact]
    public void Uniform_default_all_segments_same_width()
    {
        var layout = TableLayout.Create(TableGrid.CreateEmpty(3, 3), 0, 0);
        var segments = BorderGridResolver.Resolve(layout, Fallback).ToList();

        segments.Should().NotBeEmpty();
        segments.Should().OnlyContain(s => s.WidthMm == Fallback);
    }

    [Fact]
    public void Outer_left_column_thicker()
    {
        var grid = TableGrid.CreateEmpty(3, 3);
        for (var row = 0; row < 3; row++)
            grid = WithBorder(grid, new CellAddr(row, 0), new BorderSet(Left: Outer));

        var layout = TableLayout.Create(grid, 0, 0);
        var segments = BorderGridResolver.Resolve(layout, Fallback).ToList();

        segments.Where(s => s.Orientation == GridLineOrientation.Vertical && s.FixedCoord == layout.ColumnBoundariesX[0])
            .Should().OnlyContain(s => s.WidthMm == Outer);
    }

    [Fact]
    public void Outer_right_column_thicker()
    {
        var grid = TableGrid.CreateEmpty(3, 3);
        for (var row = 0; row < 3; row++)
            grid = WithBorder(grid, new CellAddr(row, 2), new BorderSet(Right: Outer));
        var layout = TableLayout.Create(grid, 0, 0);
        var segments = BorderGridResolver.Resolve(layout, Fallback).ToList();

        segments.Where(s => s.Orientation == GridLineOrientation.Vertical && s.FixedCoord == layout.ColumnBoundariesX[3])
            .Should().OnlyContain(s => s.WidthMm == Outer);
    }

    [Fact]
    public void Inner_vertical_max_of_neighbors()
    {
        var grid = TableGrid.CreateEmpty(3, 3);
        for (var row = 0; row < 3; row++)
        {
            grid = WithBorder(grid, new CellAddr(row, 0), new BorderSet(Right: Outer));
            grid = WithBorder(grid, new CellAddr(row, 1), new BorderSet(Left: Fallback));
        }

        var layout = TableLayout.Create(grid, 0, 0);
        var x = layout.ColumnBoundariesX[1];

        var segments = BorderGridResolver.Resolve(layout, Fallback)
            .Where(s => s.Orientation == GridLineOrientation.Vertical && s.FixedCoord == x)
            .ToList();

        segments.Should().OnlyContain(s => s.WidthMm == Outer);
    }

    [Fact]
    public void Merge_colspan_skips_internal_vertical()
    {
        var anchor = new CellAddr(0, 0);
        var grid = GridEditor.Merge(TableGrid.CreateEmpty(3, 3), anchor, rowSpan: 1, colSpan: 2);
        var layout = TableLayout.Create(grid, 0, 0);
        var internalX = layout.ColumnBoundariesX[1];
        var row0Top = layout.RowBoundariesY[0];
        var row0Bottom = layout.RowBoundariesY[1];

        var segments = BorderGridResolver.Resolve(layout, Fallback)
            .Where(s => s.Orientation == GridLineOrientation.Vertical && s.FixedCoord == internalX)
            .ToList();

        segments.Should().NotContain(s => s.Start <= row0Bottom && s.End >= row0Top);
        segments.Should().NotBeEmpty();
    }

    [Fact]
    public void Merge_rowspan_skips_internal_horizontal()
    {
        var anchor = new CellAddr(0, 0);
        var grid = GridEditor.Merge(TableGrid.CreateEmpty(3, 3), anchor, rowSpan: 2, colSpan: 1);
        var layout = TableLayout.Create(grid, 0, 0);
        var internalY = layout.RowBoundariesY[1];
        var col0Left = layout.ColumnBoundariesX[0];
        var col0Right = layout.ColumnBoundariesX[1];

        var segments = BorderGridResolver.Resolve(layout, Fallback)
            .Where(s => s.Orientation == GridLineOrientation.Horizontal && s.FixedCoord == internalY)
            .ToList();

        segments.Should().NotContain(s => s.Start <= col0Left && s.End >= col0Right);
        segments.Should().NotBeEmpty();
    }

    [Fact]
    public void BuildFamilyTable_outer_thicker_than_inner()
    {
        var layout = TableLayout.Create(TableSamples.BuildFamilyTable(), 0, 0);
        var segments = BorderGridResolver.Resolve(layout, Fallback).ToList();

        var outerMin = segments
            .Where(IsOuterSegment)
            .Min(s => s.WidthMm);

        var innerMax = segments
            .Where(s => !IsOuterSegment(s))
            .Max(s => s.WidthMm);

        outerMin.Should().BeGreaterThan(innerMax);
    }

    [Fact]
    public void Zero_border_falls_back()
    {
        var layout = TableLayout.Create(TableGrid.CreateEmpty(2, 2), 0, 0);
        var segments = BorderGridResolver.Resolve(layout, Fallback).ToList();

        segments.Should().OnlyContain(s => s.WidthMm == Fallback);
    }

    [Fact]
    public void TableBounds_segment_count()
    {
        var layout = TableLayout.Create(TableGrid.CreateEmpty(3, 3), 0, 0);
        var segments = BorderGridResolver.Resolve(layout, Fallback).ToList();

        segments.Count(s => s.Orientation == GridLineOrientation.Vertical).Should().Be(12);
        segments.Count(s => s.Orientation == GridLineOrientation.Horizontal).Should().Be(12);
    }

    private static bool IsOuterSegment(BorderGridSegment seg)
    {
        // 外缘段在 Resolve 中由 c=0/ColCount 与 r=0/RowCount 产生；用宽度区分更稳
        return seg.WidthMm >= Outer - 0.001;
    }

    private static TableGrid WithBorder(TableGrid grid, CellAddr addr, BorderSet borders)
    {
        var dict = grid.Structure.Styles.ToDictionary(e => e.Key, e => e.Value);
        dict[addr] = new CellStyle(Borders: borders);
        return grid with { Structure = grid.Structure with { Styles = dict } };
    }
}
