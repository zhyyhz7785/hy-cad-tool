using FluentAssertions;
using HyCAD.Tables.Adapters;
using HyCAD.Tables.Data;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;
using Xunit;

namespace HyCAD.Tables.Tests;

public sealed class HtmlTableAdapterTests
{
    private static readonly HtmlTableAdapter Adapter = new();

    [Fact]
    public void Capability_matches_html_defaults()
    {
        var cap = Adapter.Capability;

        cap.Grid.Should().Be(AdapterFeatureLevel.Full);
        cap.RectangularMerge.Should().Be(AdapterFeatureLevel.Full);
        cap.DiagonalSplit.Should().Be(AdapterFeatureLevel.Degraded);
        cap.VerticalStacked.Should().Be(AdapterFeatureLevel.Full);
        cap.Formula.Should().Be(AdapterFeatureLevel.Degraded);
        cap.RichStyles.Should().Be(AdapterFeatureLevel.Degraded);
    }

    [Fact]
    public void Import_throws_not_supported()
    {
        var act = () => Adapter.Import("<table></table>");

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Export*");
    }

    [Fact]
    public void Export_merge_outputs_rowspan_colspan_and_skips_hidden_cells()
    {
        var anchor = new CellAddr(0, 0);
        var grid = GridEditor.Merge(
            GridEditor.SetValue(TableGrid.CreateEmpty(3, 3), anchor, new CellValue("merged")),
            anchor,
            rowSpan: 2,
            colSpan: 2);

        var html = Adapter.Export(grid, new HtmlTableExportOptions { Title = "merge-test" });

        html.Should().Contain("rowspan=\"2\"");
        html.Should().Contain("colspan=\"2\"");
        html.Should().Contain("merged");

        var trCount = CountOccurrences(html, "<tr");
        trCount.Should().Be(3);

        var tdCount = CountOccurrences(html, "<td");
        tdCount.Should().BeLessThan(9);
    }

    [Fact]
    public void Export_vertical_stacked_includes_vstack_class()
    {
        var addr = new CellAddr(0, 0);
        var grid = TableGrid.CreateEmpty(2, 2) with
        {
            Structure = GridStructure.CreateEmpty(GridTopology.CreateUniform(2, 2, 10, 25)) with
            {
                Styles = new Dictionary<CellAddr, CellStyle>
                {
                    [addr] = new CellStyle(Orientation: TextOrientation.VerticalStacked)
                }
            }
        };
        grid = GridEditor.SetValue(grid, addr, new CellValue("竖排"));

        var html = Adapter.Export(grid);

        html.Should().Contain("class=\"vstack\"");
        html.Should().Contain("竖排");
    }

    [Fact]
    public void Export_diagonal_includes_both_subcell_texts()
    {
        var addr = new CellAddr(0, 0);
        var grid = GridEditor.SplitDiagonal(
            TableGrid.CreateEmpty(2, 2),
            addr,
            DiagonalDirection.BackSlashTLBR,
            new[]
            {
                new SubCell(new CellValue("学历")),
                new SubCell(new CellValue("学位"))
            });

        var html = Adapter.Export(grid);

        html.Should().Contain("diag-backslash");
        html.Should().Contain("学历");
        html.Should().Contain("学位");
    }

    [Fact]
    public void Export_escapes_html_special_characters()
    {
        var grid = GridEditor.SetValue(
            TableGrid.CreateEmpty(1, 1),
            new CellAddr(0, 0),
            new CellValue("<A&B>"));

        var html = Adapter.Export(grid);

        html.Should().Contain("&lt;A&amp;B&gt;");
        html.Should().NotContain("<A&B>");
    }

    [Fact]
    public void Export_family_table_has_correct_row_count_and_header()
    {
        var grid = SampleTablesEndToEndTests.BuildFamilyTable();
        var html = Adapter.Export(grid, new HtmlTableExportOptions { Title = "家庭成员表" });

        CountOccurrences(html, "<tr").Should().Be(7);
        html.Should().Contain("称谓");
        html.Should().Contain("工作单位及职务");
        html.Should().Contain("colspan=\"2\"");
    }

    [Fact]
    public void Export_personnel_table_has_title_vertical_and_photo_slot()
    {
        var grid = SampleTablesEndToEndTests.BuildPersonnelTable();
        var html = Adapter.Export(grid, new HtmlTableExportOptions { Title = "人员基本情况表" });

        CountOccurrences(html, "<tr").Should().Be(7);
        html.Should().Contain("人员基本情况表");
        html.Should().Contain("vstack");
        html.Should().Contain("role-photoslot");
        html.Should().Contain("张三");
    }

    [Fact]
    public void Export_table_fragment_without_full_document()
    {
        var grid = TableGrid.CreateEmpty(1, 1);
        var html = Adapter.Export(grid, new HtmlTableExportOptions { FullDocument = false });

        html.Should().StartWith("<table");
        html.Should().NotContain("<!DOCTYPE");
        html.Should().NotContain("<html");
    }

    [Fact]
    public void Export_sample_tables_to_html_files_for_visual_review()
    {
        var outputDir = Path.Combine(AppContext.BaseDirectory, "html-samples");
        Directory.CreateDirectory(outputDir);

        var family = SampleTablesEndToEndTests.BuildFamilyTable();
        var personnel = SampleTablesEndToEndTests.BuildPersonnelTable();

        var familyPath = Path.Combine(outputDir, "family.html");
        var personnelPath = Path.Combine(outputDir, "personnel.html");

        File.WriteAllText(
            familyPath,
            Adapter.Export(family, new HtmlTableExportOptions { Title = "家庭成员表" }));
        File.WriteAllText(
            personnelPath,
            Adapter.Export(personnel, new HtmlTableExportOptions { Title = "人员基本情况表" }));

        File.Exists(familyPath).Should().BeTrue();
        File.Exists(personnelPath).Should().BeTrue();
        new FileInfo(familyPath).Length.Should().BeGreaterThan(100);
        new FileInfo(personnelPath).Length.Should().BeGreaterThan(100);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }
}
