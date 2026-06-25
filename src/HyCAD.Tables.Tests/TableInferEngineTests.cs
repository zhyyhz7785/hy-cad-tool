using FluentAssertions;
using HyCAD.Tables.Data;
using HyCAD.Tables.Inference;
using HyCAD.Tables.Layout;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Samples;
using HyCAD.Tables.Structure;
using Xunit;

namespace HyCAD.Tables.Tests;

public sealed class TableInferEngineTests
{
    private readonly TableInferEngine _engine = new();

    [Fact]
    public void T1_uniform_3x3_nine_texts()
    {
        var xs = new[] { 0.0, 100.0, 200.0, 300.0 };
        var ys = new[] { 240.0, 160.0, 80.0, 0.0 };
        var texts = new List<TextBox2d>();
        for (var r = 0; r < 3; r++)
        {
            for (var c = 0; c < 3; c++)
                texts.Add(TableInferTestFixtures.TextAtCell(xs, ys, r, c, $"{r}{c}"));
        }

        var input = TableInferTestFixtures.BuildFromBoundaries(xs, ys, texts: texts);
        var result = _engine.Infer(input);

        result.Status.Should().Be(InferStatus.Success);
        result.Grid!.Structure.Topology.RowCount.Should().Be(3);
        result.Grid.Structure.Topology.ColCount.Should().Be(3);
        result.Grid.Structure.Merges.Should().BeEmpty();
        GridEditor.GetValue(result.Grid, new CellAddr(1, 1)).Text.Should().Be("11");
    }

    [Fact]
    public void T2_missing_internal_vertical_merge_colspan_2()
    {
        var input = TableInferTestFixtures.BuildUniformGrid(
            rows: 1,
            cols: 2,
            cellWidth: 50,
            cellHeight: 40,
            top: 40,
            omitVerticalBoundaries: new HashSet<int> { 1 });

        var result = _engine.Infer(input);

        result.Status.Should().Be(InferStatus.Success);
        result.Grid!.Structure.Merges.Should().ContainSingle();
        result.Grid.Structure.Merges[0].ColSpan.Should().Be(2);
    }

    [Fact]
    public void T3_missing_internal_horizontal_merge_rowspan_2()
    {
        var input = TableInferTestFixtures.BuildUniformGrid(
            rows: 2,
            cols: 1,
            cellWidth: 50,
            cellHeight: 40,
            top: 80,
            omitHorizontalBoundaries: new HashSet<int> { 1 });

        var result = _engine.Infer(input);

        result.Status.Should().Be(InferStatus.Success);
        result.Grid!.Structure.Merges.Should().ContainSingle();
        result.Grid.Structure.Merges[0].RowSpan.Should().Be(2);
    }

    [Fact]
    public void T4_title_colspan_3()
    {
        var input = TableInferTestFixtures.BuildUniformGrid(
            rows: 1,
            cols: 3,
            cellWidth: 40,
            cellHeight: 30,
            top: 30,
            omitVerticalBoundaries: new HashSet<int> { 1, 2 });

        var result = _engine.Infer(input);

        result.Status.Should().Be(InferStatus.Success);
        var merge = result.Grid!.Structure.Merges.Should().ContainSingle().Subject;
        merge.TopLeft.Should().Be(new CellAddr(0, 0));
        merge.ColSpan.Should().Be(3);
    }

    [Fact]
    public void T5_photo_rowspan_3()
    {
        var input = TableInferTestFixtures.BuildUniformGrid(
            rows: 3,
            cols: 1,
            cellWidth: 40,
            cellHeight: 30,
            top: 90,
            omitHorizontalBoundaries: new HashSet<int> { 1, 2 });

        var result = _engine.Infer(input);

        result.Status.Should().Be(InferStatus.Success);
        var merge = result.Grid!.Structure.Merges.Should().ContainSingle().Subject;
        merge.RowSpan.Should().Be(3);
    }

    [Fact]
    public void T6_text_in_merged_bottom_right_goes_to_anchor()
    {
        var xs = new[] { 0.0, 50.0, 100.0 };
        var ys = new[] { 40.0, 0.0 };
        var input = TableInferTestFixtures.BuildFromBoundaries(
            xs,
            ys,
            omitVerticalBoundaries: new HashSet<int> { 1 },
            texts: new[]
            {
                new TextBox2d(new LayoutRect(85, 10, 95, 2), "Photo", 3.5),
            });

        var result = _engine.Infer(input);

        result.Status.Should().Be(InferStatus.Success);
        GridEditor.GetValue(result.Grid!, new CellAddr(0, 0)).Text.Should().Be("Photo");
    }

    [Fact]
    public void T7_diagonal_line_ignored()
    {
        var xs = new[] { 0.0, 50.0, 100.0 };
        var ys = new[] { 50.0, 0.0 };
        var input = TableInferTestFixtures.BuildFromBoundaries(
            xs,
            ys,
            texts: new[] { TableInferTestFixtures.TextAtCell(xs, ys, 0, 0, "A") });

        input = new TableInferInput
        {
            Segments = input.Segments
                .Concat(new[] { new LineSegment2d(0, 50, 50, 0, GridLineOrientation.Horizontal) })
                .ToList(),
            Texts = input.Texts,
        };

        var result = _engine.Infer(input);

        result.Status.Should().Be(InferStatus.Success);
        GridEditor.GetValue(result.Grid!, new CellAddr(0, 0)).Text.Should().Be("A");
    }

    [Fact]
    public void T8_near_45_noise_discarded_with_message()
    {
        var input = TableInferTestFixtures.BuildUniformGrid(1, 1, 50, 40, top: 40);
        input = new TableInferInput
        {
            Segments = input.Segments
                .Concat(new[] { new LineSegment2d(0, 0, 30, 30, GridLineOrientation.Horizontal) })
                .ToList(),
        };

        var result = _engine.Infer(input);

        result.Status.Should().Be(InferStatus.Success);
        result.Messages.Should().Contain(m => m.Contains("45°"));
    }

    [Fact]
    public void T9_missing_outer_edge_incomplete_grid()
    {
        var xs = new[] { 0.0, 100.0 };
        var ys = new[] { 80.0, 0.0 };
        var segments = new List<LineSegment2d>
        {
            new(0, 80, 100, 80, GridLineOrientation.Horizontal),
            new(0, 0, 100, 0, GridLineOrientation.Horizontal),
            new(0, 0, 0, 80, GridLineOrientation.Vertical),
        };

        var result = _engine.Infer(new TableInferInput { Segments = segments });

        result.Status.Should().Be(InferStatus.IncompleteGrid);
    }

    [Fact]
    public void T10_single_line_incomplete_grid()
    {
        var result = _engine.Infer(new TableInferInput
        {
            Segments = new[] { new LineSegment2d(0, 0, 100, 0, GridLineOrientation.Horizontal) },
        });

        result.Status.Should().BeOneOf(InferStatus.IncompleteGrid, InferStatus.NoTextNoStructure);
    }

    [Fact]
    public void T11_non_uniform_column_widths()
    {
        var xs = new[] { 0.0, 50.0, 150.0 };
        var ys = new[] { 40.0, 0.0 };
        var result = _engine.Infer(TableInferTestFixtures.BuildFromBoundaries(xs, ys));

        result.Status.Should().Be(InferStatus.Success);
        result.Grid!.Structure.Topology.Cols[0].Size.Should().BeApproximately(50, 0.01);
        result.Grid.Structure.Topology.Cols[1].Size.Should().BeApproximately(100, 0.01);
    }

    [Fact]
    public void T12_empty_table_lines_only_success()
    {
        var result = _engine.Infer(TableInferTestFixtures.BuildUniformGrid(2, 2, 50, 40, top: 80));

        result.Status.Should().Be(InferStatus.Success);
        result.Grid!.Data.Cells.Should().BeEmpty();
    }

    [Fact]
    public void T13_two_texts_same_cell_marks_uncertain()
    {
        var xs = new[] { 0.0, 50.0 };
        var ys = new[] { 40.0, 0.0 };
        var input = TableInferTestFixtures.BuildFromBoundaries(
            xs,
            ys,
            texts: new[]
            {
                TableInferTestFixtures.TextAtCell(xs, ys, 0, 0, "A"),
                TableInferTestFixtures.TextAtCell(xs, ys, 0, 0, "B"),
            });

        var result = _engine.Infer(input);

        result.Status.Should().Be(InferStatus.Success);
        result.UncertainCells.Should().Contain(new CellAddr(0, 0));
        GridEditor.GetValue(result.Grid!, new CellAddr(0, 0)).Text.Should().Contain("A").And.Contain("B");
    }

    [Fact]
    public void T14_success_cases_pass_grid_invariants()
    {
        var cases = new[]
        {
            TableInferTestFixtures.BuildUniformGrid(3, 3, 100, 80),
            TableInferTestFixtures.BuildUniformGrid(1, 2, 50, 40, top: 40, omitVerticalBoundaries: new HashSet<int> { 1 }),
        };

        foreach (var input in cases)
        {
            var result = _engine.Infer(input);
            result.Status.Should().Be(InferStatus.Success);
            GridInvariants.Validate(result.Grid!).IsValid.Should().BeTrue();
        }
    }

    [Fact]
    public void T15_golden_grid_round_trip_diff_at_least_95_percent()
    {
        var golden = TableSamples.BuildPersonnelTable();
        var input = TableInferTestFixtures.FromGoldenGrid(golden);
        var result = _engine.Infer(input);

        result.Status.Should().Be(InferStatus.Success);
        var score = TableInferTestFixtures.CompareGolden(result.Grid!, golden);
        score.Should().BeGreaterOrEqualTo(0.95);
    }

    [Fact]
    public void T16_exploded_personnel_with_carrier_and_photoslot_succeeds()
    {
        var golden = TableSamples.BuildPersonnelTable();
        var layout = TableLayout.Create(golden, 0, 0);
        var input = TableInferTestFixtures.FromGoldenGrid(golden);
        input = TableInferTestFixtures.WithCarrierBounds(input, layout.TableBounds);
        layout.TryGetCellRect(new CellAddr(1, 6), out var photoRect, out _);
        input = TableInferTestFixtures.WithInsetRect(input, photoRect, 2.0);

        var result = _engine.Infer(input);

        result.Status.Should().Be(InferStatus.Success, string.Join("; ", result.Messages));
    }

    [Fact]
    public void T16b_personnel_with_photoslot_only_no_carrier()
    {
        var golden = TableSamples.BuildPersonnelTable();
        var layout = TableLayout.Create(golden, 0, 0);
        var input = TableInferTestFixtures.FromGoldenGrid(golden);
        layout.TryGetCellRect(new CellAddr(1, 6), out var photoRect, out _);
        input = TableInferTestFixtures.WithInsetRect(input, photoRect, 2.0);

        var result = _engine.Infer(input);

        result.Status.Should().Be(InferStatus.Success, string.Join("; ", result.Messages));
    }

    [Fact]
    public void T17_explode_offset_outer_verticals_succeeds()
    {
        var golden = TableSamples.BuildPersonnelTable();
        var layout = TableLayout.Create(golden, 0, 0);
        var input = TableInferTestFixtures.FromGoldenGrid(golden);
        input = TableInferTestFixtures.WithExplodeOffsetOuterFrames(input, layout.TableBounds);

        var result = _engine.Infer(input);

        result.Status.Should().Be(InferStatus.Success, string.Join("; ", result.Messages));
    }

    [Fact]
    public void T18_outer_vertical_row_gaps_succeeds()
    {
        var input = TableInferTestFixtures.BuildUniformGridWithOuterVerticalGaps(3, 3, 100, 80, gap: 0.3);
        var result = _engine.Infer(input);

        result.Status.Should().Be(InferStatus.Success, string.Join("; ", result.Messages));
    }
}
