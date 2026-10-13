using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HyCAD.Geometry;
using HyCAD.Tables;
using HyCADTool.TextLayout;
using HYFEA.Core.LinearAlgebra;
using HYFEA.Hosting;
using HYFEA.Viz;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetArchTest.Rules;
using Xunit;

namespace HyCADTool.LinuxTests
{
    public class ArchitectureBaselineTests
    {
        private static readonly Regex ShellRef = new Regex(@"HyCADTool\.Shell", RegexOptions.Compiled);
        private static readonly Regex GetPoint = new Regex(@"\.\s*GetPoint\s*\(", RegexOptions.Compiled);

        [Fact]
        public void Features_must_not_gain_new_shell_references()
        {
            AssertNoNewViolations(
                "whitelist-features-shell.txt",
                ScanFeatureCounts(ShellRef));
        }

        [Fact]
        public void Features_must_not_gain_new_editor_getpoint_calls()
        {
            AssertNoNewViolations(
                "whitelist-editor-getpoint.txt",
                ScanFeatureCounts(GetPoint));
        }

        [Fact]
        public void Features_must_not_gain_new_mutable_static_fields()
        {
            AssertNoNewViolations(
                "whitelist-mutable-static.txt",
                ScanMutableStatics(FeaturesRoot()));
        }

        [Fact]
        public void Shared_must_not_gain_new_mutable_static_fields()
        {
            AssertNoNewViolations(
                "whitelist-mutable-static-shared.txt",
                ScanMutableStatics(SharedRoot()));
        }

        [Fact]
        public void Neutral_assemblies_must_not_reference_host_ui()
        {
            var prefixes = new[]
            {
                "Autodesk",
                "System.Windows",
                "Avalonia",
                "Microsoft.Web.WebView2",
                "HyCADTool",
            };

            AssertNeutral(typeof(Point2D).Assembly, prefixes);
            AssertNeutral(typeof(TableDocument).Assembly, prefixes);
            AssertNeutral(typeof(DenseMatrix).Assembly, prefixes);
            AssertNeutral(typeof(IFemHostGeometry).Assembly, prefixes);
            AssertNeutral(typeof(ResultMesh).Assembly, prefixes);
            AssertNeutral(typeof(LayoutEngine).Assembly, prefixes);
        }

        private static void AssertNeutral(Assembly assembly, string[] prefixes)
        {
            var bad = new List<string>();
            foreach (var name in assembly.GetReferencedAssemblies())
            {
                var n = name.Name ?? "";
                foreach (var prefix in prefixes)
                {
                    if (n.Equals(prefix, StringComparison.Ordinal)
                        || n.StartsWith(prefix + ".", StringComparison.Ordinal))
                    {
                        bad.Add(n);
                        break;
                    }
                }
            }

            Assert.True(
                bad.Count == 0,
                assembly.GetName().Name + " 依赖了宿主/UI：" + string.Join(", ", bad));

            foreach (var name in prefixes)
            {
                var result = Types.InAssembly(assembly)
                    .ShouldNot()
                    .HaveDependencyOn(name)
                    .GetResult();
                Assert.True(
                    result.IsSuccessful,
                    assembly.GetName().Name + " 类型依赖了 " + name + ": " +
                    string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
            }
        }

        private static IReadOnlyCollection<string> ScanFeatureCounts(Regex pattern)
        {
            var hits = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var path in Directory.GetFiles(FeaturesRoot(), "*.cs", SearchOption.AllDirectories))
            {
                var rel = ToRepoRelative(path);
                var text = StripComments(File.ReadAllText(path));
                var n = pattern.Matches(text).Count;
                if (n > 0)
                    hits.Add(rel + "::" + n);
            }
            return hits;
        }

        private static IReadOnlyCollection<string> ScanMutableStatics(string root)
        {
            var hits = new SortedSet<string>(StringComparer.Ordinal);
            if (!Directory.Exists(root))
                return hits;

            foreach (var path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var rel = ToRepoRelative(path);
                var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
                var rootNode = tree.GetCompilationUnitRoot();
                foreach (var field in rootNode.DescendantNodes().OfType<FieldDeclarationSyntax>())
                {
                    if (!field.Modifiers.Any(SyntaxKind.StaticKeyword))
                        continue;
                    if (field.Modifiers.Any(SyntaxKind.ConstKeyword))
                        continue;
                    if (field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
                        continue;
                    if (HasProcessWide(field))
                        continue;
                    foreach (var variable in field.Declaration.Variables)
                        hits.Add(rel + "::" + variable.Identifier.Text);
                }
            }
            return hits;
        }

        private static bool HasProcessWide(FieldDeclarationSyntax field)
        {
            foreach (var list in field.AttributeLists)
            {
                foreach (var attr in list.Attributes)
                {
                    var name = attr.Name.ToString();
                    if (name == "ProcessWide" || name.EndsWith(".ProcessWide", StringComparison.Ordinal)
                        || name == "ProcessWideAttribute" || name.EndsWith(".ProcessWideAttribute", StringComparison.Ordinal))
                        return true;
                }
            }
            return false;
        }

        private static void AssertNoNewViolations(string whitelistName, IReadOnlyCollection<string> actual)
        {
            var allowed = LoadWhitelist(whitelistName);
            var extra = actual.Where(x => !allowed.Contains(x)).ToArray();
            var stale = allowed.Where(x => !actual.Contains(x)).ToArray();
            Assert.True(extra.Length == 0, "新增违规（次数只减不增；请改代码或更新白名单 " + whitelistName + "）:\n" + string.Join("\n", extra));
            Assert.True(stale.Length == 0, "白名单应收紧（实际次数已下降，请把计数改小 " + whitelistName + "）:\n" + string.Join("\n", stale));
        }

        private static HashSet<string> LoadWhitelist(string name)
        {
            var path = Path.Combine(RepoRoot(), "src", "HyCADTool.LinuxTests", "Architecture", name);
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in File.ReadAllLines(path))
            {
                var t = line.Trim();
                if (t.Length == 0 || t.StartsWith("#"))
                    continue;
                set.Add(t);
            }
            return set;
        }

        private static string StripComments(string text)
        {
            text = Regex.Replace(text, @"/\*.*?\*/", "", RegexOptions.Singleline);
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var idx = lines[i].IndexOf("//", StringComparison.Ordinal);
                if (idx >= 0)
                    lines[i] = lines[i].Substring(0, idx);
            }
            return string.Join("\n", lines);
        }

        private static string FeaturesRoot()
        {
            return Path.Combine(RepoRoot(), "src", "HyCADTool", "Features");
        }

        private static string SharedRoot()
        {
            return Path.Combine(RepoRoot(), "src", "HyCADTool", "Shared");
        }

        private static string ToRepoRelative(string path)
        {
            var root = RepoRoot();
            var full = Path.GetFullPath(path);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return full.Replace('\\', '/');
            return full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HyCADtoolGpt.sln")))
                dir = dir.Parent;
            if (dir == null)
                throw new InvalidOperationException("找不到 HyCADtoolGpt.sln");
            return dir.FullName;
        }
    }
}
