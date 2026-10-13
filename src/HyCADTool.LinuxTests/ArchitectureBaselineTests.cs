using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        [Fact]
        public void Features_must_not_gain_new_shell_references()
        {
            AssertNoNewViolations(
                "whitelist-features-shell.txt",
                ScanFeatureCounts(CountShellRefs));
        }

        [Fact]
        public void Features_must_not_gain_new_editor_getpoint_calls()
        {
            AssertNoNewViolations(
                "whitelist-editor-getpoint.txt",
                ScanFeatureCounts(CountGetPointCalls));
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
        public void ProcessWide_usages_must_match_snapshot()
        {
            var actual = ScanProcessWideUsages(FeaturesRoot(), SharedRoot());
            AssertNoNewViolations("processwide-usages.txt", actual);
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

            var self = assembly.GetName().Name ?? "";
            foreach (var name in prefixes)
            {
                if (self.Equals(name, StringComparison.Ordinal)
                    || self.StartsWith(name + ".", StringComparison.Ordinal))
                    continue;

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

        private static IReadOnlyCollection<string> ScanFeatureCounts(Func<CompilationUnitSyntax, int> count)
        {
            var hits = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var path in Directory.GetFiles(FeaturesRoot(), "*.cs", SearchOption.AllDirectories))
            {
                var rel = ToRepoRelative(path);
                var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
                var n = count(tree.GetCompilationUnitRoot());
                if (n > 0)
                    hits.Add(rel + "::" + n);
            }
            return hits;
        }

        /// <summary>
        /// 每个 <c>HyCADTool.Shell</c> 限定名计 1（含 using）。不扫字符串与注释。
        /// </summary>
        internal static int CountShellRefs(CompilationUnitSyntax root)
        {
            var n = 0;
            foreach (var name in root.DescendantNodes().OfType<QualifiedNameSyntax>())
            {
                if (name.Right.Identifier.Text == "Shell"
                    && name.Left is IdentifierNameSyntax left
                    && left.Identifier.Text == "HyCADTool")
                    n++;
            }

            // HyCADTool.Shell.ViewModels.Foo.Current 是成员访问，不是 QualifiedName。
            foreach (var access in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                if (access.Name.Identifier.Text == "Shell"
                    && access.Expression is IdentifierNameSyntax left
                    && left.Identifier.Text == "HyCADTool")
                    n++;
            }
            return n;
        }

        /// <summary>
        /// 每个名为 GetPoint 的调用计 1。不扫字符串与注释。
        /// </summary>
        internal static int CountGetPointCalls(CompilationUnitSyntax root)
        {
            var n = 0;
            foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (inv.Expression is MemberAccessExpressionSyntax member
                    && member.Name.Identifier.Text == "GetPoint")
                    n++;
                else if (inv.Expression is IdentifierNameSyntax id
                    && id.Identifier.Text == "GetPoint")
                    n++;
            }
            return n;
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
                    if (HasValidProcessWide(field, field.Declaration.Type))
                        continue;
                    var readonlyField = field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);
                    if (readonlyField && !IsMutableCollectionType(field.Declaration.Type))
                        continue;
                    foreach (var variable in field.Declaration.Variables)
                        hits.Add(rel + "::" + variable.Identifier.Text);
                }

                foreach (var prop in rootNode.DescendantNodes().OfType<PropertyDeclarationSyntax>())
                {
                    if (!prop.Modifiers.Any(SyntaxKind.StaticKeyword))
                        continue;
                    if (HasValidProcessWide(prop, prop.Type))
                        continue;
                    if (!HasWritableAccessor(prop))
                        continue;
                    hits.Add(rel + "::" + prop.Identifier.Text);
                }

                foreach (var ev in rootNode.DescendantNodes().OfType<EventFieldDeclarationSyntax>())
                {
                    if (!ev.Modifiers.Any(SyntaxKind.StaticKeyword))
                        continue;
                    if (HasValidProcessWide(ev, ev.Declaration.Type))
                        continue;
                    foreach (var variable in ev.Declaration.Variables)
                        hits.Add(rel + "::event " + variable.Identifier.Text);
                }
            }
            return hits;
        }

        private static bool HasWritableAccessor(PropertyDeclarationSyntax prop)
        {
            if (prop.AccessorList == null)
                return false;
            foreach (var acc in prop.AccessorList.Accessors)
            {
                if (acc.IsKind(SyntaxKind.SetAccessorDeclaration) || acc.IsKind(SyntaxKind.InitAccessorDeclaration))
                    return true;
            }
            return false;
        }

        private static bool IsMutableCollectionType(TypeSyntax type)
        {
            var text = type.ToString();
            if (text.IndexOf("IReadOnly", StringComparison.Ordinal) >= 0)
                return false;
            if (text.IndexOf("Immutable", StringComparison.Ordinal) >= 0)
                return false;
            if (text.IndexOf("ReadOnlySpan", StringComparison.Ordinal) >= 0
                || text.IndexOf("ReadOnlyMemory", StringComparison.Ordinal) >= 0)
                return false;

            return text.IndexOf("List<", StringComparison.Ordinal) >= 0
                || text.IndexOf("Dictionary<", StringComparison.Ordinal) >= 0
                || text.IndexOf("HashSet<", StringComparison.Ordinal) >= 0
                || text.IndexOf("Queue<", StringComparison.Ordinal) >= 0
                || text.IndexOf("Stack<", StringComparison.Ordinal) >= 0
                || text.IndexOf("Collection<", StringComparison.Ordinal) >= 0
                || text.IndexOf("Concurrent", StringComparison.Ordinal) >= 0
                || text.EndsWith("[]", StringComparison.Ordinal);
        }

        private static readonly HashSet<string> ForbiddenProcessWideTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "Document", "Database", "ObjectId", "Window", "Editor",
        };

        private static IReadOnlyCollection<string> ScanProcessWideUsages(params string[] roots)
        {
            var hits = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var root in roots)
            {
                if (!Directory.Exists(root))
                    continue;
                foreach (var path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    var rel = ToRepoRelative(path);
                    var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
                    var rootNode = tree.GetCompilationUnitRoot();
                    foreach (var field in rootNode.DescendantNodes().OfType<FieldDeclarationSyntax>())
                    {
                        string reason;
                        if (!TryGetValidProcessWideReason(field, field.Declaration.Type, out reason))
                            continue;
                        foreach (var variable in field.Declaration.Variables)
                            hits.Add(rel + "::" + variable.Identifier.Text + "::" + reason);
                    }

                    foreach (var prop in rootNode.DescendantNodes().OfType<PropertyDeclarationSyntax>())
                    {
                        string reason;
                        if (!TryGetValidProcessWideReason(prop, prop.Type, out reason))
                            continue;
                        hits.Add(rel + "::" + prop.Identifier.Text + "::" + reason);
                    }

                    foreach (var ev in rootNode.DescendantNodes().OfType<EventFieldDeclarationSyntax>())
                    {
                        string reason;
                        if (!TryGetValidProcessWideReason(ev, ev.Declaration.Type, out reason))
                            continue;
                        foreach (var variable in ev.Declaration.Variables)
                            hits.Add(rel + "::event " + variable.Identifier.Text + "::" + reason);
                    }
                }
            }

            return hits;
        }

        private static bool HasValidProcessWide(MemberDeclarationSyntax member, TypeSyntax type)
        {
            string reason;
            return TryGetValidProcessWideReason(member, type, out reason);
        }

        private static bool TryGetValidProcessWideReason(MemberDeclarationSyntax member, TypeSyntax type, out string reason)
        {
            reason = null;
            foreach (var list in member.AttributeLists)
            {
                foreach (var attr in list.Attributes)
                {
                    var name = attr.Name.ToString();
                    if (name != "ProcessWide" && !name.EndsWith(".ProcessWide", StringComparison.Ordinal)
                        && name != "ProcessWideAttribute" && !name.EndsWith(".ProcessWideAttribute", StringComparison.Ordinal))
                        continue;

                    if (attr.ArgumentList == null || attr.ArgumentList.Arguments.Count == 0)
                        return false;
                    var expr = attr.ArgumentList.Arguments[0].Expression as LiteralExpressionSyntax;
                    if (expr == null || !expr.IsKind(SyntaxKind.StringLiteralExpression))
                        return false;
                    var text = expr.Token.ValueText;
                    if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 8)
                        return false;
                    if (TypeHasForbiddenHostToken(type))
                        return false;
                    reason = text.Trim();
                    return true;
                }
            }

            return false;
        }

        private static bool TypeHasForbiddenHostToken(TypeSyntax type)
        {
            if (type == null)
                return false;
            foreach (var ident in type.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            {
                if (ForbiddenProcessWideTypes.Contains(ident.Identifier.Text))
                    return true;
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
