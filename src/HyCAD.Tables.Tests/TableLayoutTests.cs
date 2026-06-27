using FluentAssertions;
using HyCAD.Tables;
using HyCAD.Tables.Layout;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Samples;
using HyCAD.Tables.Structure;
using Xunit;

namespace HyCAD.Tables.Tests;

public sealed class TableLayoutTests
{
    private const double OriginX = 100;
    private const double OriginY = 200;

    [Fact]
    public void Create_uniform_3x3_origin_at_zero()
    {
        var grid = TableGrid.CreateEmpty(3, 3);
        var layout = TableLayout.Create(grid, OriginX, OriginY);

        layout.TryGetCellRect(new CellAddr(0, 0), out var rect, out _).Should().BeTrue();
        rect.Left.Should().Be(OriginX);
        rect.Top.Should().Be(OriginY);
        rect.Right.Should().Be(OriginX + TableConstants.DefaultColWidth);
        rect.Bottom.Should().Be(OriginY - TableConstants.DefaultRowHeight);
        rect.Width.Should().Be(TableConstants.DefaultColWidth);
        rect.Height.Should().Be(TableConstants.DefaultRowHeight);
    }

    [Fact]
    public void Create_with_scale_magnifies_geometry_uniformly()
    {
        var grid = TableGrid.CreateEmpty(3, 3, rowHeight: 12, colWidth: 30);

        var baseLayout = TableLayout.Create(grid, 0, 0);
        var scaledLayout = TableLayout.Create(grid, 0, 0, GrowDirection.Down, 50.0);

        // 口径 B：scale=50 时几何应为 1:1 的 50 倍。
        scaledLayout.Scale.Should().Be(50.0);
        scaledLayout.TotalWidth.Should().BeApproximately(baseLayout.TotalWidth * 50.0, 1e-6);
        scaledLayout.TotalHeight.Should().BeApproximately(baseLayout.TotalHeight * 50.0, 1e-6);

        baseLayout.TryGetCellRect(new CellAddr(1, 1), out var baseRect, out _).Should().BeTrue();
        scaledLayout.TryGetCellRect(new CellAddr(1, 1), out var scaledRect, out _).Should().BeTrue();
        scaledRect.Width.Should().BeApproximately(baseRect.Width * 50.0, 1e-6);
        scaledRect.Height.Should().BeApproximately(baseRect.Height * 50.0, 1e-6);
        scaledRect.Left.Should().BeApproximately(baseRect.Left * 50.0, 1e-6);
        scaledRect.Top.Should().BeApproximately(baseRect.Top * 50.0, 1e-6);
    }

    [Fact]
    public void Create_with_nonpositive_scale_falls_back_to_one()
    {
        var grid = TableGrid.CreateEmpty(2, 2, rowHeight: 10, colWidth: 20);
        var layout = TableLayout.Create(grid, 0, 0, GrowDirection.Down, 0);

        layout.Scale.Should().Be(1.0);
        layout.TotalWidth.Should().Be(40);
    }

    [Fact]
    public void RowTop_decreases_with_row_index_Down()
    {
        var grid = TableGrid.CreateEmpty(3, 3);
        var layout = TableLayout.Create(grid, OriginX, OriginY);

        layout.GetRowTop(1).Should().BeLessThan(layout.GetRowTop(0));
        (layout.GetRowTop(0) - layout.GetRowTop(1))
            .Should().Be(TableConstants.DefaultRowHeight);
    }

    [Fact]
    public void ColumnLeft_increases_with_col_index()
    {
        var grid = TableGrid.CreateEmpty(3, 3);
        var layout = TableLayout.Create(grid, OriginX, OriginY);

        layout.GetColumnLeft(1).Should().BeGreaterThan(layout.GetColumnLeft(0));
        (layout.GetColumnLeft(1) - layout.GetColumnLeft(0))
            .Should().Be(TableConstants.DefaultColWidth);
    }

    [Fact]
    public void TableBounds_matches_sum_of_tracks()
    {
        var grid = TableGrid.CreateEmpty(3, 3, rowHeight: 12, colWidth: 30);
        var layout = TableLayout.Create(grid, OriginX, OriginY);

        layout.TotalWidth.Should().Be(90);
        layout.TotalHeight.Should().Be(36);
        layout.TableBounds.Width.Should().Be(90);
        layout.TableBounds.Height.Should().Be(36);
        layout.TableBounds.Left.Should().Be(OriginX);
        layout.TableBounds.Top.Should().Be(OriginY);
        layout.TableBounds.Right.Should().Be(OriginX + 90);
        layout.TableBounds.Bottom.Should().Be(OriginY - 36);
    }

    [Fact]
    public void Hidden_member_not_in_enumerate()
    {
        var grid = TableGrid.CreateEmpty(2, 2);
        grid = GridEditor.Merge(grid, new CellAddr(0, 0), rowSpan: 2, colSpan: 2);

        var layout = TableLayout.Create(grid, 0, 0);
        layout.EnumerateVisibleCells().Should().ContainSingle();
    }

    [Fact]
    public void TryGetCellRect_hidden_returns_false()
    {
        var grid = TableGrid.CreateEmpty(2, 2);
        grid = GridEditor.Merge(grid, new CellAddr(0, 0), rowSpan: 2, colSpan: 2);

        var layout = TableLayout.Create(grid, 0, 0);
        layout.TryGetCellRect(new CellAddr(1, 1), out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Merge_2x2_anchor_bounds_span_four_cells()
    {
        var grid = TableGrid.CreateEmpty(3, 3, rowHeight: 10, colWidth: 25);
        grid = GridEditor.Merge(grid, new CellAddr(0, 0), rowSpan: 2, colSpan: 2);

        var layout = TableLayout.Create(grid, OriginX, OriginY);
        layout.TryGetCellRect(new CellAddr(0, 0), out var rect, out var merge).Should().BeTrue();

        merge.Should().NotBeNull();
        merge!.RowSpan.Should().Be(2);
        merge.ColSpan.Should().Be(2);
        rect.Width.Should().Be(50);
        rect.Height.Should().Be(20);
        rect.Left.Should().Be(OriginX);
        rect.Top.Should().Be(OriginY);
        rect.Right.Should().Be(OriginX + 50);
        rect.Bottom.Should().Be(OriginY - 20);
    }

    [Fact]
    public void Merge_colspan_only_width_spans()
    {
        var grid = TableGrid.CreateEmpty(2, 3, rowHeight: 10, colWidth: 20);
        grid = GridEditor.Merge(grid, new CellAddr(0, 0), rowSpan: 1, colSpan: 2);

        var layout = TableLayout.Create(grid, 0, 0);
        layout.TryGetCellRect(new CellAddr(0, 0), out var rect, out _).Should().BeTrue();

        rect.Width.Should().Be(40);
        rect.Height.Should().Be(10);
    }

    [Fact]
    public void Merge_rowspan_only_height_spans()
    {
        var grid = TableGrid.CreateEmpty(3, 2, rowHeight: 8, colWidth: 15);
        grid = GridEditor.Merge(grid, new CellAddr(0, 0), rowSpan: 2, colSpan: 1);

        var layout = TableLayout.Create(grid, 0, 0);
        layout.TryGetCellRect(new CellAddr(0, 0), out var rect, out _).Should().BeTrue();

        rect.Width.Should().Be(15);
        rect.Height.Should().Be(16);
    }

    [Fact]
    public void Non_uniform_col_widths_accumulate()
    {
        var topology = new GridTopology(
            new[] { new GridTrack(10), new GridTrack(10) },
            new[] { new GridTrack(20), new GridTrack(30), new GridTrack(50) },
            GrowDirection.Down,
            BorderSet.None);
        var grid = new TableGrid
        {
            Structure = GridStructure.CreateEmpty(topology)
        };

        var layout = TableLayout.Create(grid, 0, 0);
        layout.GetColumnLeft(0).Should().Be(0);
        layout.GetColumnLeft(1).Should().Be(20);
        layout.GetColumnLeft(2).Should().Be(50);
        layout.GetColumnLeft(3).Should().Be(100);
    }

    [Fact]
    public void Enumerate_order_row_major_matches_html()
    {
        var grid = TableGrid.CreateEmpty(2, 3);
        grid = GridEditor.Merge(grid, new CellAddr(0, 0), rowSpan: 1, colSpan: 2);

        var layout = TableLayout.Create(grid, 0, 0);
        var addrs = layout.EnumerateVisibleCells().Select(x => x.Addr).ToList();

        addrs.Should().Equal(
            new CellAddr(0, 0),
            new CellAddr(0, 2),
            new CellAddr(1, 0),
            new CellAddr(1, 1),
            new CellAddr(1, 2));
    }

    [Fact]
    public void GrowDirection_Up_not_supported()
    {
        var grid = TableGrid.CreateEmpty(2, 2);
        var act = () => TableLayout.Create(grid, 0, 0, GrowDirection.Up);
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Column_and_row_boundaries_have_expected_lengths()
    {
        var grid = TableGrid.CreateEmpty(3, 4, rowHeight: 10, colWidth: 25);
        var layout = TableLayout.Create(grid, OriginX, OriginY);

        layout.ColumnBoundariesX.Should().HaveCount(5);
        layout.RowBoundariesY.Should().HaveCount(4);
        layout.ColumnBoundariesX[0].Should().Be(OriginX);
        layout.ColumnBoundariesX[4].Should().Be(OriginX + 100);
        layout.RowBoundariesY[0].Should().Be(OriginY);
        layout.RowBoundariesY[3].Should().Be(OriginY - 30);
    }

    [Fact]
    public void BuildFamilyTable_visible_count()
    {
        var grid = TableSamples.BuildFamilyTable();
        var layout = TableLayout.Create(grid, 0, 0);

        layout.EnumerateVisibleCells().Should().HaveCount(41);
    }

    [Fact]
    public void BuildPersonnelTable_title_merge_bounds()
    {
        var grid = TableSamples.BuildPersonnelTable();
        var layout = TableLayout.Create(grid, 0, 0);

        layout.TryGetCellRect(new CellAddr(0, 0), out var rect, out var merge).Should().BeTrue();
        merge.Should().NotBeNull();
        merge!.ColSpan.Should().Be(7);
        rect.Width.Should().Be(7 * TableConstants.DefaultColWidth);
        layout.EnumerateVisibleCells().Should().HaveCount(33);
    }
}
