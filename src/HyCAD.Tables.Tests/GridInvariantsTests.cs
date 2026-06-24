using FluentAssertions;
using HyCAD.Tables.Data;
using HyCAD.Tables.Diagnostics;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;
using Xunit;

namespace HyCAD.Tables.Tests;

public sealed class GridInvariantsTests
{
    [Fact]
    public void Validate_valid_table_with_merge_diagonal_field_and_formula_passes()
    {
        var topology = GridTopology.CreateUniform(3, 3, 10, 25);
        var anchor = new CellAddr(0, 0);
        var structure = new GridStructure(
            topology,
            new[] { new MergeRegion(anchor, 2, 2) },
            new Dictionary<CellAddr, DiagonalSplit>
            {
                [anchor] = new(
                    DiagonalDirection.BackSlashTLBR,
                    new[]
                    {
                        new SubCell(new CellValue("学历"), FieldKey: "education"),
                        new SubCell(new CellValue("学位"), FieldKey: "degree")
                    })
            },
            new Dictionary<CellAddr, CellStyle>(),
            new Dictionary<CellAddr, CellRole>(),
            new Dictionary<string, CellAddr>
            {
                ["name"] = new CellAddr(0, 2)
            });

        var data = new GridData(new Dictionary<CellAddr, CellValue>
        {
            [new CellAddr(2, 2)] = new CellValue("42", Kind: CellValueKind.Formula, Formula: "=SUM(A1:A2)")
        });

        var report = GridInvariants.Validate(structure, data);

        report.IsValid.Should().BeTrue();
        report.Violations.Should().BeEmpty();
    }

    [Fact]
    public void Validate_invalid_dimensions_reports_InvalidDimensions()
    {
        var topology = new GridTopology(
            Array.Empty<GridTrack>(),
            new[] { new GridTrack(10) });
        var structure = GridStructure.CreateEmpty(topology);
        var data = GridData.Empty;

        var report = GridInvariants.Validate(structure, data);

        report.IsValid.Should().BeFalse();
        report.Violations.Should().ContainSingle(v => v.Code == TableInvariantCode.InvalidDimensions);
    }

    [Fact]
    public void Validate_out_of_bounds_style_reports_AddressOutOfBounds()
    {
        var structure = BuildStructure(
            styles: new Dictionary<CellAddr, CellStyle>
            {
                [new CellAddr(5, 5)] = new CellStyle()
            });

        var report = GridInvariants.Validate(structure, GridData.Empty);

        report.IsValid.Should().BeFalse();
        report.Violations.Should().Contain(v => v.Code == TableInvariantCode.AddressOutOfBounds);
    }

    [Fact]
    public void Validate_invalid_merge_span_reports_InvalidMergeSpan()
    {
        var structure = BuildStructure(
            merges: new[] { new MergeRegion(new CellAddr(0, 0), 0, 2) });

        var report = GridInvariants.Validate(structure, GridData.Empty);

        report.Violations.Should().Contain(v => v.Code == TableInvariantCode.InvalidMergeSpan);
    }

    [Fact]
    public void Validate_merge_out_of_bounds_reports_MergeOutOfBounds()
    {
        var structure = BuildStructure(
            merges: new[] { new MergeRegion(new CellAddr(2, 2), 2, 1) });

        var report = GridInvariants.Validate(structure, GridData.Empty);

        report.Violations.Should().Contain(v => v.Code == TableInvariantCode.MergeOutOfBounds);
    }

    [Fact]
    public void Validate_overlapping_merges_reports_MergeOverlap()
    {
        var structure = BuildStructure(
            merges: new[]
            {
                new MergeRegion(new CellAddr(0, 0), 2, 2),
                new MergeRegion(new CellAddr(1, 1), 2, 2)
            });

        var report = GridInvariants.Validate(structure, GridData.Empty);

        report.Violations.Should().Contain(v => v.Code == TableInvariantCode.MergeOverlap);
    }

    [Fact]
    public void Validate_diagonal_on_covered_cell_reports_DiagonalOnCoveredCell()
    {
        var covered = new CellAddr(1, 1);
        var structure = BuildStructure(
            merges: new[] { new MergeRegion(new CellAddr(0, 0), 2, 2) },
            diagonals: new Dictionary<CellAddr, DiagonalSplit>
            {
                [covered] = new(
                    DiagonalDirection.BackSlashTLBR,
                    new[] { new SubCell(new CellValue("A")), new SubCell(new CellValue("B")) })
            });

        var report = GridInvariants.Validate(structure, GridData.Empty);

        report.Violations.Should().Contain(v =>
            v.Code == TableInvariantCode.DiagonalOnCoveredCell && v.Cell == covered);
    }

    [Fact]
    public void Validate_duplicate_field_key_reports_DuplicateFieldKey()
    {
        var structure = BuildStructure(
            fieldIndex: new Dictionary<string, CellAddr> { ["dup"] = new CellAddr(0, 1) },
            diagonals: new Dictionary<CellAddr, DiagonalSplit>
            {
                [new CellAddr(0, 0)] = new(
                    DiagonalDirection.BackSlashTLBR,
                    new[] { new SubCell(new CellValue("A"), FieldKey: "dup"), new SubCell(new CellValue("B")) })
            });

        var report = GridInvariants.Validate(structure, GridData.Empty);

        report.Violations.Should().Contain(v => v.Code == TableInvariantCode.DuplicateFieldKey);
    }

    [Fact]
    public void Validate_formula_without_text_reports_FormulaMissing()
    {
        var addr = new CellAddr(0, 0);
        var data = new GridData(new Dictionary<CellAddr, CellValue>
        {
            [addr] = new CellValue("", Kind: CellValueKind.Formula, Formula: null)
        });

        var report = GridInvariants.Validate(BuildStructure(), data);

        report.Violations.Should().Contain(v =>
            v.Code == TableInvariantCode.FormulaMissing && v.Cell == addr);
    }

    [Fact]
    public void Validate_bound_without_expression_reports_BindingMissing()
    {
        var addr = new CellAddr(1, 1);
        var data = new GridData(new Dictionary<CellAddr, CellValue>
        {
            [addr] = new CellValue("x", Kind: CellValueKind.Bound, BindingExpr: "")
        });

        var report = GridInvariants.Validate(BuildStructure(), data);

        report.Violations.Should().Contain(v =>
            v.Code == TableInvariantCode.BindingMissing && v.Cell == addr);
    }

    [Fact]
    public void MergeRegion_Intersects_adjacent_regions_do_not_overlap()
    {
        var left = new MergeRegion(new CellAddr(0, 0), 2, 1);
        var right = new MergeRegion(new CellAddr(0, 1), 2, 1);

        left.Intersects(right).Should().BeFalse();
    }

    [Fact]
    public void MergeRegion_Intersects_overlapping_regions_intersect()
    {
        var a = new MergeRegion(new CellAddr(0, 0), 2, 2);
        var b = new MergeRegion(new CellAddr(1, 1), 2, 2);

        a.Intersects(b).Should().BeTrue();
    }

    [Fact]
    public void MergeRegion_Covers_addr_inside_rectangle()
    {
        var merge = new MergeRegion(new CellAddr(0, 0), 2, 2);

        merge.Covers(new CellAddr(0, 0)).Should().BeTrue();
        merge.Covers(new CellAddr(1, 1)).Should().BeTrue();
        merge.Covers(new CellAddr(2, 0)).Should().BeFalse();
        merge.Covers(new CellAddr(0, 2)).Should().BeFalse();
    }

    [Fact]
    public void Validate_data_on_hidden_cell_reports_DataOnHiddenCell()
    {
        var anchor = new CellAddr(0, 0);
        var member = new CellAddr(1, 1);
        var structure = BuildStructure(
            merges: new[] { new MergeRegion(anchor, 2, 2) });
        var data = new GridData(new Dictionary<CellAddr, CellValue>
        {
            [anchor] = new CellValue("anchor"),
            [member] = new CellValue("stale")
        });

        var report = GridInvariants.Validate(structure, data);

        report.Violations.Should().Contain(v =>
            v.Code == TableInvariantCode.DataOnHiddenCell && v.Cell == member);
    }

    [Fact]
    public void Validate_field_key_on_hidden_cell_reports_FieldKeyNotOnAnchor()
    {
        var hidden = new CellAddr(1, 1);
        var structure = BuildStructure(
            merges: new[] { new MergeRegion(new CellAddr(0, 0), 2, 2) },
            fieldIndex: new Dictionary<string, CellAddr> { ["name"] = hidden });

        var report = GridInvariants.Validate(structure, GridData.Empty);

        report.Violations.Should().Contain(v =>
            v.Code == TableInvariantCode.FieldKeyNotOnAnchor && v.Cell == hidden);
    }

    [Fact]
    public void Validate_same_value_mirror_mismatch_reports_violation()
    {
        var anchor = new CellAddr(0, 0);
        var member = new CellAddr(1, 1);
        var structure = BuildStructure(
            merges: new[] { new MergeRegion(anchor, 2, 2, MergeValuePolicy.SameValue) });
        var data = new GridData(new Dictionary<CellAddr, CellValue>
        {
            [anchor] = new CellValue("a"),
            [member] = new CellValue("b")
        });

        var report = GridInvariants.Validate(structure, data);

        report.Violations.Should().Contain(v =>
            v.Code == TableInvariantCode.SameValueMirrorMismatch && v.Cell == member);
    }

    private static GridStructure BuildStructure(
        IReadOnlyList<MergeRegion>? merges = null,
        IReadOnlyDictionary<CellAddr, DiagonalSplit>? diagonals = null,
        IReadOnlyDictionary<CellAddr, CellStyle>? styles = null,
        IReadOnlyDictionary<CellAddr, CellRole>? roles = null,
        IReadOnlyDictionary<string, CellAddr>? fieldIndex = null)
    {
        var topology = GridTopology.CreateUniform(3, 3, 10, 25);
        return new GridStructure(
            topology,
            merges ?? Array.Empty<MergeRegion>(),
            diagonals ?? new Dictionary<CellAddr, DiagonalSplit>(),
            styles ?? new Dictionary<CellAddr, CellStyle>(),
            roles ?? new Dictionary<CellAddr, CellRole>(),
            fieldIndex ?? new Dictionary<string, CellAddr>());
    }
}
