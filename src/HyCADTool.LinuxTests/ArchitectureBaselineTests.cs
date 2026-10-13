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
            var hits = new SortedSet<string>(ScanFeatureCounts(CountShellRefs), StringComparer.Ordinal);
            foreach (var row in ScanXamlShellRefs())
                hits.Add(row);
            AssertNoNewViolations("whitelist-features-shell.txt", hits);
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
        public void Global_alias_feature_namespace_is_detected()
        {
            var tree = CSharpSyntaxTree.ParseText(
                "class C { void M() { var x = global::HyCADTool.Features.Road.Plan.Foo.Bar; } }");
            var found = ReferencedHyCadNamespaces(tree.GetCompilationUnitRoot())
                .Any(s => s.StartsWith("HyCADTool.Features.Road", StringComparison.Ordinal));
            Assert.True(found, "global::HyCADTool.Features.Road 应被模块依赖扫描计入");
        }

        [Fact]
        public void Xaml_shell_xmlns_is_counted_and_features_xaml_is_clean()
        {
            Assert.Equal(1, CountXamlShellRefs("xmlns:behaviors=\"clr-namespace:HyCADTool.Shell.Behaviors\""));
            Assert.Equal(0, CountXamlShellRefs("clr-namespace:HyCADTool.Features.Road.CrossSection.Behaviors"));
            Assert.Empty(ScanXamlShellRefs());
        }

        [Fact]
        public void Growing_whitelist_over_cap_fails()
        {
            var allowed = new[] { "a", "b", "c" };
            var actual = new[] { "a", "b", "c" };
            var grown = DescribeWhitelistViolation("demo.txt", allowed, actual, cap: 2);
            Assert.Contains("白名单行数禁止增加", grown);
            Assert.DoesNotContain("或更新白名单", grown);

            var extra = DescribeWhitelistViolation(
                "demo.txt",
                new[] { "a" },
                new[] { "a", "new-row" },
                cap: 10);
            Assert.Contains("禁止扩大白名单，请改代码", extra);
            Assert.DoesNotContain("或更新白名单", extra);
        }

        [Fact]
        public void Unknown_feature_slice_is_not_mapped_to_base()
        {
            Assert.Equal("base", ModuleOfNamespace("HyCADTool.Features.Tables"));
            Assert.Equal("base", ModuleOfNamespace("HyCADTool.Features.Export.Commands"));
            Assert.Equal("road", ModuleOfNamespace("HyCADTool.Features.Road.Plan"));
            Assert.Equal("structure", ModuleOfNamespace("HyCADTool.Features.Settlement.ViewModels"));

            var ex = Assert.Throws<InvalidOperationException>(
                () => ModuleOfNamespace("HyCADTool.Features.NewBizA"));
            Assert.Contains("NewBizA", ex.Message);
            Assert.Contains("禁止默认归入 base", ex.Message);

            string module;
            string unknown;
            Assert.False(TryMapModule("HyCADTool.Features.NewBizB.Services", out module, out unknown));
            Assert.Equal("NewBizB", unknown);
            Assert.Null(module);
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
        /// Features / Shared 等非宿主 XAML 里的 <c>clr-namespace:HyCADTool.Shell</c> 计数。
        /// </summary>
        internal static IReadOnlyCollection<string> ScanXamlShellRefs()
        {
            var hits = new SortedSet<string>(StringComparer.Ordinal);
            var src = Path.Combine(RepoRoot(), "src", "HyCADTool");
            if (!Directory.Exists(src))
                return hits;
            foreach (var path in Directory.GetFiles(src, "*.xaml", SearchOption.AllDirectories))
            {
                if (IsBinOrObj(path))
                    continue;
                var rel = ToRepoRelative(path);
                if (IsHostXaml(rel))
                    continue;
                var n = CountXamlShellRefs(File.ReadAllText(path));
                if (n > 0)
                    hits.Add(rel + "::" + n);
            }
            return hits;
        }

        internal static int CountXamlShellRefs(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            const string needle = "clr-namespace:HyCADTool.Shell";
            var n = 0;
            var i = 0;
            while ((i = text.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
            {
                n++;
                i += needle.Length;
            }
            return n;
        }

        private static bool IsHostXaml(string repoRelative)
        {
            return repoRelative.StartsWith("src/HyCADTool/Shell/", StringComparison.Ordinal)
                || repoRelative.StartsWith("src/HyCADTool/App/", StringComparison.Ordinal);
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
            var unknownSlices = new SortedSet<string>(StringComparer.Ordinal);
            var src = Path.Combine(RepoRoot(), "src", "HyCADTool");
            foreach (var path in EnumerateCsFiles(src))
            {
                var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
                var root = tree.GetCompilationUnitRoot();
                string from;
                string unknownFrom;
                if (!TryMapModule(DeclaredNamespace(root), out from, out unknownFrom))
                {
                    if (unknownFrom != null)
                        unknownSlices.Add(unknownFrom + " :: " + ToRepoRelative(path));
                    continue;
                }
                if (from == null || from == "host")
                    continue;

                var targets = new HashSet<string>(StringComparer.Ordinal);
                foreach (var ns in ReferencedHyCadNamespaces(root))
                {
                    string to;
                    string unknownTo;
                    if (!TryMapModule(ns, out to, out unknownTo))
                    {
                        if (unknownTo != null)
                            unknownSlices.Add(unknownTo + " :: " + ToRepoRelative(path));
                        continue;
                    }
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

            Assert.True(
                unknownSlices.Count == 0,
                "未知 Features 切片（须写入 KnownBaseSlices 或业务模块映射，禁止默认归 base）:\n"
                + string.Join("\n", unknownSlices));
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

        private static readonly HashSet<string> KnownBaseSlices = new HashSet<string>(StringComparer.Ordinal)
        {
            "BlockLeader",
            "Cluster",
            "DimTextAlign",
            "Export",
            "G101",
            "G16",
            "OverKill",
            "Tables",
            "TestPaper",
            "TextEdit",
            "TitleBlock",
        };

        internal static string ModuleOfNamespace(string ns)
        {
            string module;
            string unknownSlice;
            if (TryMapModule(ns, out module, out unknownSlice))
                return module;
            if (unknownSlice != null)
                throw new InvalidOperationException(
                    "未知 Features 切片 '" + unknownSlice + "'，禁止默认归入 base。请写入 KnownBaseSlices 或业务模块映射。");
            return null;
        }

        internal static bool TryMapModule(string ns, out string module, out string unknownSlice)
        {
            module = null;
            unknownSlice = null;
            if (string.IsNullOrEmpty(ns))
                return false;
            if (ns == "HyCADTool.Shared" || ns.StartsWith("HyCADTool.Shared.", StringComparison.Ordinal))
            {
                module = "shared";
                return true;
            }
            if (ns == "HyCADTool.Shell" || ns.StartsWith("HyCADTool.Shell.", StringComparison.Ordinal))
            {
                module = "host";
                return true;
            }
            if (ns == "HyCADTool.App" || ns.StartsWith("HyCADTool.App.", StringComparison.Ordinal))
            {
                module = "host";
                return true;
            }
            if (ns == "HyCADTool.Licensing" || ns.StartsWith("HyCADTool.Licensing.", StringComparison.Ordinal))
            {
                module = "host";
                return true;
            }
            if (ns == "HyCADTool.Domain" || ns.StartsWith("HyCADTool.Domain.", StringComparison.Ordinal))
            {
                module = "road";
                return true;
            }
            const string features = "HyCADTool.Features.";
            if (ns == "HyCADTool.Features")
            {
                module = "base";
                return true;
            }
            if (!ns.StartsWith(features, StringComparison.Ordinal))
                return false;

            var slice = ns.Substring(features.Length);
            var dot = slice.IndexOf('.');
            if (dot >= 0)
                slice = slice.Substring(0, dot);
            switch (slice)
            {
                case "Road":
                    module = "road";
                    return true;
                case "SpongeCity":
                    module = "sponge-city";
                    return true;
                case "Fem":
                    module = "fea";
                    return true;
                case "DesignSpec":
                    module = "design-spec";
                    return true;
                case "DataExchange":
                    module = "hyob";
                    return true;
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
                    module = "structure";
                    return true;
                default:
                    if (KnownBaseSlices.Contains(slice))
                    {
                        module = "base";
                        return true;
                    }
                    unknownSlice = slice;
                    return false;
            }
        }

        internal static IEnumerable<string> ReferencedHyCadNamespaces(CompilationUnitSyntax root)
        {
            foreach (var u in root.Usings)
            {
                var name = StripGlobalPrefix(u.Name != null ? u.Name.ToString() : null);
                if (IsHyCadTrackedNamespace(name))
                    yield return name;
            }

            foreach (var alias in root.DescendantNodes().OfType<AliasQualifiedNameSyntax>())
            {
                var text = StripGlobalPrefix(alias.Name != null ? alias.Name.ToString() : null);
                if (IsHyCadTrackedNamespace(text))
                    yield return text;
            }

            foreach (var name in root.DescendantNodes().OfType<QualifiedNameSyntax>())
            {
                var text = StripGlobalPrefix(name.ToString());
                if (IsHyCadTrackedNamespace(text))
                    yield return text;
            }

            foreach (var access in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                var text = StripGlobalPrefix(access.ToString());
                if (IsHyCadTrackedNamespace(text))
                    yield return text;
            }
        }

        private static string StripGlobalPrefix(string text)
        {
            if (text != null && text.StartsWith("global::", StringComparison.Ordinal))
                return text.Substring("global::".Length);
            return text;
        }

        private static bool IsHyCadTrackedNamespace(string text)
        {
            return text != null
                && (text.StartsWith("HyCADTool.Features.", StringComparison.Ordinal)
                    || text.StartsWith("HyCADTool.Shared.", StringComparison.Ordinal)
                    || text.StartsWith("HyCADTool.Domain.", StringComparison.Ordinal));
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

        internal static readonly IReadOnlyDictionary<string, int> WhitelistCaps =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["whitelist-features-shell.txt"] = 0,
                ["whitelist-editor-getpoint.txt"] = 32,
                ["whitelist-mutable-static.txt"] = 68,
                ["whitelist-mutable-static-shared.txt"] = 5,
                ["processwide-usages.txt"] = 9,
                ["whitelist-module-deps.txt"] = 26,
            };

        private static void AssertNoNewViolations(string whitelistName, IReadOnlyCollection<string> actual)
        {
            int cap;
            if (!WhitelistCaps.TryGetValue(whitelistName, out cap))
                throw new InvalidOperationException("白名单未登记上限: " + whitelistName);
            var allowed = LoadWhitelist(whitelistName);
            var message = DescribeWhitelistViolation(whitelistName, allowed, actual, cap);
            Assert.True(message == null, message);
        }

        /// <summary>
        /// 白名单只减不增：行数不得超过硬编码上限；新增未登记项必须改代码，禁止靠加白名单行放行。
        /// 返回 null 表示通过。
        /// </summary>
        internal static string DescribeWhitelistViolation(
            string whitelistName,
            IReadOnlyCollection<string> allowed,
            IReadOnlyCollection<string> actual,
            int cap)
        {
            if (allowed.Count > cap)
                return "白名单行数禁止增加（" + whitelistName + " 上限 " + cap + "，实际 " + allowed.Count + "）。请改代码，禁止扩大白名单。";
            if (actual.Count > cap)
                return "违规行数禁止增加（" + whitelistName + " 上限 " + cap + "，实际 " + actual.Count + "）。请改代码，禁止扩大白名单。";
            var extra = actual.Where(x => !allowed.Contains(x)).ToArray();
            if (extra.Length > 0)
                return "新增违规（次数只减不增；禁止扩大白名单，请改代码 " + whitelistName + "）:\n" + string.Join("\n", extra);
            var stale = allowed.Where(x => !actual.Contains(x)).ToArray();
            if (stale.Length > 0)
                return "白名单应收紧（实际次数已下降，请把计数改小 " + whitelistName + "）:\n" + string.Join("\n", stale);
            return null;
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
