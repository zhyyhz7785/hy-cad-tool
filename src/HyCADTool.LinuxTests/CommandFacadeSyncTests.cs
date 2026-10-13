using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace HyCADTool.LinuxTests
{
    public class CommandFacadeSyncTests
    {
        private static readonly Regex CommandMethodName = new Regex(
            @"\[CommandMethod\(\s*""([^""]+)""",
            RegexOptions.Compiled);

        [Fact]
        public void Production_command_set_matches_recall_facade()
        {
            // 两个门面的 [CommandMethod] 名字（含别名，区分大小写）必须一致。
            // 唯一例外：hySeg3 在开发模式挂在 Recall.cs（与 C2 同程序集，再写入 CommandFacade 会 eDuplicateKey），
            // 生产模式没有 ReCall.dll，所以只出现在 ProductionCommandFacade。
            // C1/C2/hyRecallSelfCheck 是 ReCall 壳命令，不进生产门面。
            var root = RepoRoot();
            var recall = ReadCommandNames(Path.Combine(root, "src", "ReCall", "CommandFacade.cs"));
            var production = ReadCommandNames(Path.Combine(root, "src", "HyCADTool", "App", "Production", "ProductionCommandFacade.cs"));
            var recallClass = ReadCommandNames(Path.Combine(root, "src", "ReCall", "Recall.cs"));

            var onlyRecall = recall.Except(production).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            var onlyProduction = production.Except(recall).OrderBy(s => s, StringComparer.Ordinal).ToArray();

            Assert.True(onlyRecall.Length == 0, "只在 CommandFacade: " + string.Join(", ", onlyRecall));
            Assert.Equal(new[] { "hySeg3" }, onlyProduction);
            Assert.Contains("hySeg3", recallClass);
            Assert.DoesNotContain("hySeg3", recall);
        }

        [Fact]
        public void Commands_json_keys_are_registered()
        {
            var root = RepoRoot();
            var registered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in ReadCommandNames(Path.Combine(root, "src", "ReCall", "CommandFacade.cs")))
                registered.Add(name);
            foreach (var path in Directory.GetFiles(Path.Combine(root, "src", "ReCall"), "*.cs"))
            {
                if (string.Equals(Path.GetFileName(path), "CommandFacade.cs", StringComparison.OrdinalIgnoreCase))
                    continue;
                foreach (var name in ReadCommandNames(path))
                    registered.Add(name);
            }

            var missing = new List<string>();
            using (var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "src", "ReCall", "commands.json"))))
            {
                foreach (var prop in doc.RootElement.GetProperty("commands").EnumerateObject())
                {
                    if (!registered.Contains(prop.Name))
                        missing.Add(prop.Name);
                }
            }

            Assert.True(missing.Count == 0, "commands.json 有键但没有 [CommandMethod]: " + string.Join(", ", missing));
        }

        [Fact]
        public void Road_tree_double_click_uses_registered_commands()
        {
            var root = RepoRoot();
            var handler = File.ReadAllText(Path.Combine(
                root, "src", "HyCADTool", "Shared", "AutoCAD", "Services", "Road", "AutoCadRoadTreeInteractionHandler.cs"));
            var registered = new HashSet<string>(
                ReadCommandNames(Path.Combine(root, "src", "ReCall", "CommandFacade.cs")),
                StringComparer.OrdinalIgnoreCase);

            Assert.Contains("return \"hyRoadAw\"", handler);
            Assert.Contains("return \"hyRoadP\"", handler);
            Assert.Contains("return \"hyRoadC\"", handler);
            Assert.DoesNotContain("hyRoadAlnWorkbench", handler);
            Assert.DoesNotContain("hyRoadProf\"", handler);
            Assert.DoesNotContain("hyRoadCorridor", handler);
            Assert.DoesNotContain("hyRoadCheckCode", handler);

            foreach (Match match in Regex.Matches(handler, "return \"([^\"]+)\""))
            {
                var cmd = match.Groups[1].Value;
                Assert.True(registered.Contains(cmd), "项目树投递了未注册命令: " + cmd);
            }
        }

        [Fact]
        public void Production_commands_go_through_license_gate()
        {
            var root = RepoRoot();
            var text = File.ReadAllText(Path.Combine(root, "src", "HyCADTool", "App", "Production", "ProductionCommandFacade.cs"));
            var method = new Regex(
                @"(\[CommandMethod\((?:.|\n)*?\)\]\s*)+public void \w+\(\)\s*=>\s*([^;]+);",
                RegexOptions.Compiled);
            var matches = method.Matches(text);

            Assert.True(matches.Count >= 100, "生产门面方法数异常: " + matches.Count);
            foreach (Match match in matches)
            {
                Assert.Contains("LicenseGate.RunGated", match.Groups[2].Value);
            }

            Assert.Equal(CommandMethodName.Matches(text).Count, CountCommandAttributes(matches));
        }

        [Fact]
        public void License_gate_wrap_script_must_not_exist()
        {
            var root = RepoRoot();
            var script = Path.Combine(
                root, "build", "scripts", "Wrap-LicenseGate-ProductionCommandFacade.ps1");
            Assert.False(
                File.Exists(script),
                "双门面已由生成器写出 LicenseGate.RunGated，禁止再保留或新增 Wrap 脚本: " + script);
        }

        private static int CountCommandAttributes(MatchCollection methods)
        {
            int count = 0;
            foreach (Match match in methods)
                count += Regex.Matches(match.Value, @"\[CommandMethod\(").Count;
            return count;
        }

        private static HashSet<string> ReadCommandNames(string path)
        {
            var text = File.ReadAllText(path);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in CommandMethodName.Matches(text))
                names.Add(match.Groups[1].Value);
            return names;
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
