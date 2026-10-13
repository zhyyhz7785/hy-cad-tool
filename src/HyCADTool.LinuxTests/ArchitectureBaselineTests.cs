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
                ScanMutableStatics(EnumerateCsByNamespace(FeatureNamespacePrefixes)));
        }

        [Fact]
        public void Shared_must_not_gain_new_mutable_static_fields()
        {
            AssertNoNewViolations(
                "whitelist-mutable-static-shared.txt",
                ScanMutableStatics(EnumerateCsByNamespace(SharedNamespacePrefixes)));
        }

        [Fact]
        public void ProcessWide_usages_must_match_snapshot()
        {
            var actual = ScanProcessWideUsages(
                EnumerateCsByNamespace(FeatureNamespacePrefixes, SharedNamespacePrefixes));
            AssertNoNewViolations("processwide-usages.txt", actual);
        }

        [Fact]
        public void Shared_and_base_must_not_gain_new_business_module_deps()
        {
            AssertNoNewViolations(
                "whitelist-module-deps.txt",
                ScanForbiddenModuleDeps());
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

        private static readonly string[] FeatureNamespacePrefixes =
        {
            "HyCADTool.Features",
            "HyCADTool.Domain",
        };

        private static readonly string[] SharedNamespacePrefixes =
        {
            "HyCADTool.Shared",
        };

        private static IReadOnlyCollection<string> ScanFeatureCounts(Func<CompilationUnitSyntax, int> count)
        {
            var hits = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var path in EnumerateCsByNamespace(FeatureNamespacePrefixes))
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

        private static IReadOnlyCollection<string> ScanMutableStatics(IEnumerable<string> files)
        {
            var hits = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var path in files)
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
                    if (readonlyField && IsAllowedImmutableReadonlyType(field.Declaration.Type))
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

        private static bool IsAllowedImmutableReadonlyType(TypeSyntax type)
        {
            if (type == null)
                return false;

            if (type is GenericNameSyntax generic
                && generic.Identifier.Text == "Lazy"
                && generic.TypeArgumentList.Arguments.Count == 1)
                return IsAllowedImmutableReadonlyType(generic.TypeArgumentList.Arguments[0]);

            var text = type.ToString();
            if (text.IndexOf("IReadOnly", StringComparison.Ordinal) >= 0)
                return true;
            if (text.IndexOf("Immutable", StringComparison.Ordinal) >= 0)
                return true;
            if (text.IndexOf("Frozen", StringComparison.Ordinal) >= 0)
                return true;
            if (text.IndexOf("ReadOnlySpan", StringComparison.Ordinal) >= 0
                || text.IndexOf("ReadOnlyMemory", StringComparison.Ordinal) >= 0
                || text.IndexOf("ReadOnlyCollection", StringComparison.Ordinal) >= 0
                || text.IndexOf("ReadOnlyObservableCollection", StringComparison.Ordinal) >= 0)
                return true;

            var name = LastTypeName(type);
            switch (name)
            {
                case "bool":
                case "byte":
                case "sbyte":
                case "short":
                case "ushort":
                case "int":
                case "uint":
                case "long":
                case "ulong":
                case "float":
                case "double":
                case "decimal":
                case "char":
                case "string":
                case "String":
                case "object":
                case "Object":
                case "Boolean":
                case "Byte":
                case "SByte":
                case "Int16":
                case "UInt16":
                case "Int32":
                case "UInt32":
                case "Int64":
                case "UInt64":
                case "Single":
                case "Double":
                case "Decimal":
                case "Char":
                case "Regex":
                case "Type":
                case "CultureInfo":
                case "TimeSpan":
                case "DateTime":
                case "DateTimeOffset":
                case "Guid":
                case "Uri":
                case "IntPtr":
                case "UIntPtr":
                case "DependencyProperty":
                case "XNamespace":
                case "Brush":
                case "SolidColorBrush":
                case "MarkdownPipeline":
                case "AtlasRef":
                case "Vector2D":
                case "ElevationValue":
                case "WallThickness":
                case "SlabThickness":
                case "ToleranceSettings":
                case "StyleInfo":
                case "DataShortcutResolveResult":
                case "ConditionalWeakTable":
                    return true;
                default:
                    return false;
            }
        }

        private static string LastTypeName(TypeSyntax type)
        {
            if (type is IdentifierNameSyntax id)
                return id.Identifier.Text;
            if (type is QualifiedNameSyntax q)
                return LastTypeName(q.Right);
            if (type is AliasQualifiedNameSyntax a)
                return LastTypeName(a.Name);
            if (type is GenericNameSyntax g)
                return g.Identifier.Text;
            if (type is PredefinedTypeSyntax p)
                return p.Keyword.Text;
            if (type is NullableTypeSyntax n)
                return LastTypeName(n.ElementType);
            if (type is ArrayTypeSyntax)
                return "[]";
            return type.ToString();
        }

        private static readonly HashSet<string> ForbiddenProcessWideTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "Document", "Database", "ObjectId", "Window", "Editor",
        };

        private static IReadOnlyCollection<string> ScanProcessWideUsages(IEnumerable<string> files)
        {
            var hits = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var path in files)
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

            return hits;
        }

        /// <summary>
        /// 剩余跨模块依赖基线。格式：<c>from -&gt; to :: 仓库相对路径</c>。次数只减不增。
        /// Shared / base 画图切片不得新依赖业务模块；业务模块之间不得新互依。
        /// </summary>
        private static IReadOnlyCollection<string> ScanForbiddenModuleDeps()
        {
            var hits = new SortedSet<string>(StringComparer.Ordinal);
            var src = Path.Combine(RepoRoot(), "src", "HyCADTool");
            foreach (var path in EnumerateCsFiles(src))
            {
                var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
                var root = tree.GetCompilationUnitRoot();
                var from = ModuleOfNamespace(DeclaredNamespace(root));
                if (from == null || from == "host")
                    continue;

                var targets = new HashSet<string>(StringComparer.Ordinal);
                foreach (var ns in ReferencedHyCadNamespaces(root))
                {
                    var to = ModuleOfNamespace(ns);
                    if (to == null || to == from)
                        continue;
                    if (!IsForbiddenModuleDep(from, to))
                        continue;
                    targets.Add(to);
                }

                if (targets.Count == 0)
                    continue;
                var rel = ToRepoRelative(path);
                foreach (var to in targets)
                    hits.Add(from + " -> " + to + " :: " + rel);
            }

            return hits;
        }

        private static bool IsForbiddenModuleDep(string from, string to)
        {
            if (from == "shared" && IsBusinessModule(to))
                return true;
            if (from == "base" && IsBusinessModule(to))
                return true;
            return IsBusinessModule(from) && IsBusinessModule(to);
        }

        private static bool IsBusinessModule(string module)
        {
            return module == "road"
                || module == "sponge-city"
                || module == "structure"
                || module == "fea"
                || module == "design-spec"
                || module == "hyob";
        }

        private static string ModuleOfNamespace(string ns)
        {
            if (string.IsNullOrEmpty(ns))
                return null;
            if (ns == "HyCADTool.Shared" || ns.StartsWith("HyCADTool.Shared.", StringComparison.Ordinal))
                return "shared";
            if (ns == "HyCADTool.Shell" || ns.StartsWith("HyCADTool.Shell.", StringComparison.Ordinal))
                return "host";
            if (ns == "HyCADTool.App" || ns.StartsWith("HyCADTool.App.", StringComparison.Ordinal))
                return "host";
            if (ns == "HyCADTool.Licensing" || ns.StartsWith("HyCADTool.Licensing.", StringComparison.Ordinal))
                return "host";
            if (ns == "HyCADTool.Domain" || ns.StartsWith("HyCADTool.Domain.", StringComparison.Ordinal))
                return "road";
            const string features = "HyCADTool.Features.";
            if (ns == "HyCADTool.Features")
                return "base";
            if (!ns.StartsWith(features, StringComparison.Ordinal))
                return null;

            var slice = ns.Substring(features.Length);
            var dot = slice.IndexOf('.');
            if (dot >= 0)
                slice = slice.Substring(0, dot);
            switch (slice)
            {
                case "Road":
                    return "road";
                case "SpongeCity":
                    return "sponge-city";
                case "Fem":
                    return "fea";
                case "DesignSpec":
                    return "design-spec";
                case "DataExchange":
                    return "hyob";
                case "Reinforcement":
                case "Elevation":
                case "DimensionForReinforcement":
                case "AcadDimension":
                case "AnchorBolt":
                case "EquipmentFoundation":
                case "Pad":
                case "Misc":
                case "Pile":
                case "Settlement":
                case "BaseRein":
                case "DCEL":
                    return "structure";
                default:
                    return "base";
            }
        }

        private static IEnumerable<string> ReferencedHyCadNamespaces(CompilationUnitSyntax root)
        {
            foreach (var u in root.Usings)
            {
                var name = u.Name != null ? u.Name.ToString() : null;
                if (name != null && name.StartsWith("HyCADTool.", StringComparison.Ordinal))
                    yield return name;
            }

            foreach (var name in root.DescendantNodes().OfType<QualifiedNameSyntax>())
            {
                var text = name.ToString();
                if (text.StartsWith("HyCADTool.Features.", StringComparison.Ordinal)
                    || text.StartsWith("HyCADTool.Shared.", StringComparison.Ordinal)
                    || text.StartsWith("HyCADTool.Domain.", StringComparison.Ordinal))
                    yield return text;
            }

            foreach (var access in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                var text = access.ToString();
                if (text.StartsWith("HyCADTool.Features.", StringComparison.Ordinal)
                    || text.StartsWith("HyCADTool.Shared.", StringComparison.Ordinal)
                    || text.StartsWith("HyCADTool.Domain.", StringComparison.Ordinal))
                    yield return text;
            }
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

        private static IEnumerable<string> EnumerateCsByNamespace(params string[] prefixes)
        {
            var merged = new List<string>();
            foreach (var group in prefixes)
            {
                if (group == null)
                    continue;
                merged.Add(group);
            }

            var src = Path.Combine(RepoRoot(), "src", "HyCADTool");
            foreach (var path in EnumerateCsFiles(src))
            {
                if (FileDeclaresNamespace(path, merged))
                    yield return path;
            }
        }

        private static IEnumerable<string> EnumerateCsByNamespace(string[] first, string[] second)
        {
            var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in EnumerateCsByNamespace(first))
                set.Add(path);
            foreach (var path in EnumerateCsByNamespace(second))
                set.Add(path);
            return set;
        }

        private static IEnumerable<string> EnumerateCsFiles(string root)
        {
            if (!Directory.Exists(root))
                yield break;
            foreach (var path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (IsBinOrObj(path))
                    continue;
                yield return path;
            }
        }

        private static bool IsBinOrObj(string path)
        {
            var norm = path.Replace('/', Path.DirectorySeparatorChar);
            return norm.IndexOf(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0
                || norm.IndexOf(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool FileDeclaresNamespace(string path, IReadOnlyCollection<string> prefixes)
        {
            foreach (var line in File.ReadLines(path))
            {
                var t = line.TrimStart();
                if (!t.StartsWith("namespace ", StringComparison.Ordinal))
                    continue;
                var ns = t.Substring("namespace ".Length).Trim();
                if (ns.EndsWith(";", StringComparison.Ordinal) || ns.EndsWith("{", StringComparison.Ordinal))
                    ns = ns.Substring(0, ns.Length - 1).Trim();
                foreach (var prefix in prefixes)
                {
                    if (ns.Equals(prefix, StringComparison.Ordinal)
                        || ns.StartsWith(prefix + ".", StringComparison.Ordinal))
                        return true;
                }

                return false;
            }

            return false;
        }

        private static string DeclaredNamespace(CompilationUnitSyntax root)
        {
            var fileScoped = root.Members.OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
            if (fileScoped != null)
                return fileScoped.Name.ToString();
            var block = root.Members.OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            return block != null ? block.Name.ToString() : null;
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
