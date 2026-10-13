using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HyCAD.Geometry;
using HyCAD.Tables;
using HYFEA.Core.LinearAlgebra;
using HYFEA.Hosting;
using HYFEA.Viz;
using NetArchTest.Rules;
using Xunit;

namespace HyCADTool.LinuxTests
{
    public class ArchitectureBaselineTests
    {
        private static readonly Regex ShellRef = new Regex(@"HyCADTool\.Shell", RegexOptions.Compiled);
        private static readonly Regex GetPoint = new Regex(@"\.\s*GetPoint\s*\(", RegexOptions.Compiled);
        private static readonly Regex ProcessWide = new Regex(@"\[\s*ProcessWide\b", RegexOptions.Compiled);
        private static readonly Regex StaticField = new Regex(
            @"^\s*(?:(?:public|private|internal|protected|new|unsafe|volatile|partial|static|readonly|const)\s+)+static\s+" +
            @"(?!readonly\b)(?!const\b)(?!event\b)(?!class\b)(?!struct\b)(?!enum\b)(?!delegate\b)(?!implicit\b)(?!explicit\b)",
            RegexOptions.Compiled);

        [Fact]
        public void Features_must_not_gain_new_shell_references()
        {
            AssertNoNewViolations(
                "whitelist-features-shell.txt",
                ScanFeatureFiles((rel, text) => ShellRef.IsMatch(text) ? rel : null));
        }

        [Fact]
        public void Features_must_not_gain_new_editor_getpoint_calls()
        {
            AssertNoNewViolations(
                "whitelist-editor-getpoint.txt",
                ScanFeatureFiles((rel, text) => GetPoint.IsMatch(text) ? rel : null));
        }

        [Fact]
        public void Features_must_not_gain_new_mutable_static_fields()
        {
            AssertNoNewViolations(
                "whitelist-mutable-static.txt",
                ScanMutableStatics());
        }

        [Fact]
        public void Neutral_assemblies_must_not_reference_host_ui()
        {
            var forbidden = new[]
            {
                "Autodesk.AutoCAD.ApplicationServices",
                "Autodesk.AutoCAD.DatabaseServices",
                "System.Windows",
                "Avalonia",
            };

            AssertNeutral(typeof(Point2D).Assembly, forbidden);
            AssertNeutral(typeof(TableDocument).Assembly, forbidden);
            AssertNeutral(typeof(DenseMatrix).Assembly, forbidden);
            AssertNeutral(typeof(IFemHostGeometry).Assembly, forbidden);
            AssertNeutral(typeof(ResultMesh).Assembly, forbidden);
        }

        private static void AssertNeutral(Assembly assembly, string[] forbidden)
        {
            foreach (var name in forbidden)
            {
                var result = Types.InAssembly(assembly)
                    .ShouldNot()
                    .HaveDependencyOn(name)
                    .GetResult();
                Assert.True(
                    result.IsSuccessful,
                    assembly.GetName().Name + " 依赖了 " + name + ": " +
                    string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
            }
        }

        private static IReadOnlyCollection<string> ScanFeatureFiles(Func<string, string, string> pick)
        {
            var hits = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var path in Directory.GetFiles(FeaturesRoot(), "*.cs", SearchOption.AllDirectories))
            {
                var rel = ToRepoRelative(path);
                var text = StripComments(File.ReadAllText(path));
                var hit = pick(rel, text);
                if (!string.IsNullOrEmpty(hit))
                    hits.Add(hit);
            }
            return hits;
        }

        private static IReadOnlyCollection<string> ScanMutableStatics()
        {
            var hits = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var path in Directory.GetFiles(FeaturesRoot(), "*.cs", SearchOption.AllDirectories))
            {
                var rel = ToRepoRelative(path);
                var text = StripComments(File.ReadAllText(path));
                var prev = new List<string>();
                foreach (var rawLine in text.Split('\n'))
                {
                    var line = rawLine.TrimEnd('\r');
                    var stripped = line.Trim();
                    if (stripped.Length == 0)
                    {
                        prev.Clear();
                        continue;
                    }
                    if (stripped.StartsWith("["))
                    {
                        prev.Add(stripped);
                        continue;
                    }
                    if (StaticField.IsMatch(line) && IsField(line) && !prev.Exists(ProcessWide.IsMatch))
                        hits.Add(rel + "::" + stripped);
                    prev.Clear();
                }
            }
            return hits;
        }

        private static bool IsField(string line)
        {
            if (Regex.IsMatch(line, @"\b\w+\s*=>") || Regex.IsMatch(line, @"\)\s*=>"))
                return false;
            if (line.IndexOf('(') >= 0)
            {
                var beforeParen = line.Split('(')[0];
                if (!beforeParen.Contains("{") && !Regex.IsMatch(beforeParen, @"\bnew\b"))
                    return false;
            }
            if (Regex.IsMatch(line, @"\{\s*(get|set|add|remove)\b"))
                return false;
            if (line.TrimEnd().EndsWith("{") || line.Contains("{ get") || line.Contains("{get"))
                return false;
            return line.Contains(";") || line.Contains("=");
        }

        private static void AssertNoNewViolations(string whitelistName, IReadOnlyCollection<string> actual)
        {
            var allowed = LoadWhitelist(whitelistName);
            var extra = actual.Where(x => !allowed.Contains(x)).ToArray();
            var stale = allowed.Where(x => !actual.Contains(x)).ToArray();
            Assert.True(extra.Length == 0, "新增违规（请改代码或更新白名单 " + whitelistName + "）:\n" + string.Join("\n", extra));
            Assert.True(stale.Length == 0, "白名单已过期（请删掉已消失的条目 " + whitelistName + "）:\n" + string.Join("\n", stale));
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
