using FluentAssertions;
using HyCAD.Tables;
using Xunit;

namespace HyCAD.Tables.Tests;

public sealed class PlatformIndependenceTests
{
    private static readonly string[] ForbiddenAssemblyPrefixes =
    {
        "Autodesk",
        "AcCoreMgd",
        "AcDbMgd",
        "AcMgd",
        "AcCui",
        "AcWindows",
        "AdWindows",
        "NPOI",
        "Markdig",
        "PresentationCore",
        "PresentationFramework",
        "WindowsBase"
    };

    [Fact]
    public void HyCAD_Tables_has_no_forbidden_platform_references()
    {
        var assembly = typeof(TableGrid).Assembly;
        var referencedNames = assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(n => n != null)
            .Cast<string>()
            .ToList();

        foreach (var forbidden in ForbiddenAssemblyPrefixes)
        {
            referencedNames.Should().NotContain(
                name => name.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase),
                because: $"HyCAD.Tables must stay platform-independent (found reference to {forbidden})");
        }
    }

    [Fact]
    public void HyCAD_Tables_tests_only_reference_tables_assembly()
    {
        var testAssembly = typeof(PlatformIndependenceTests).Assembly;
        var refs = testAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(n => n != null)
            .Cast<string>()
            .ToList();

        refs.Should().Contain("HyCAD.Tables");
        refs.Should().NotContain("HyCADTool");
        refs.Should().NotContain("ReCall");
    }
}
