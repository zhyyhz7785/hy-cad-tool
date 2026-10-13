using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using HyCADTool.Licensing;
using HyCADTool.Shell.Commands;
using HyCADTool.Shell.Licensing;
using Xunit;

namespace HyCADTool.LinuxTests
{
    [Collection("CommandCatalogFile")]
    public class CommandTierMapTests
    {
        [Fact]
        public void Generated_table_matches_commands_json()
        {
            var expected = ReadJsonTiers();
            var missing = new List<string>();
            var extra = new List<string>();
            var different = new List<string>();

            foreach (var pair in expected)
            {
                if (!CommandTiers.Table.TryGetValue(pair.Key, out var actual))
                    missing.Add(pair.Key);
                else if (!string.Equals(actual, pair.Value, StringComparison.Ordinal))
                    different.Add(pair.Key + " json=" + pair.Value + " generated=" + actual);
            }

            foreach (var pair in CommandTiers.Table)
            {
                if (!expected.ContainsKey(pair.Key))
                    extra.Add(pair.Key);
            }

            Assert.True(missing.Count == 0, "生成表缺少: " + string.Join(", ", missing));
            Assert.True(extra.Count == 0, "生成表多出: " + string.Join(", ", extra));
            Assert.True(different.Count == 0, "档位不一致: " + string.Join("; ", different));
        }

        [Fact]
        public void GetRequiredTier_uses_builtin_table()
        {
            Assert.Equal(LicenseProductTier.Freemium, LicenseCommandTierMap.GetRequiredTier("Hy"));
            Assert.Equal(LicenseProductTier.Freemium, LicenseCommandTierMap.GetRequiredTier("hyLicense"));
            Assert.Equal(LicenseProductTier.Professional, LicenseCommandTierMap.GetRequiredTier("gj"));
            Assert.Equal(LicenseProductTier.Professional, LicenseCommandTierMap.GetRequiredTier("HY3"));
            Assert.Equal(LicenseProductTier.Standard, LicenseCommandTierMap.GetRequiredTier("definitely-not-a-command"));
            Assert.Equal(LicenseProductTier.Standard, LicenseCommandTierMap.GetRequiredTier(null));
        }

        [Fact]
        public void AlwaysFreemium_wins_over_table()
        {
            Assert.Equal(LicenseProductTier.Freemium, LicenseCommandTierMap.GetRequiredTier("_HyExec"));
            Assert.Equal(LicenseProductTier.Freemium, LicenseCommandTierMap.GetRequiredTier("HyB"));
        }

        [Fact]
        public void RejectAlwaysFreemiumEnqueue_blocks_free_keys()
        {
            Assert.True(LicenseCommandTierMap.IsAlwaysFreemium("_HyExec"));
            Assert.True(LicenseCommandTierMap.IsAlwaysFreemium("hy"));
            Assert.True(LicenseCommandTierMap.IsAlwaysFreemium("hyLicense"));
            Assert.Throws<ArgumentException>(() => LicenseCommandTierMap.RejectAlwaysFreemiumEnqueue("_HyExec"));
            Assert.Throws<ArgumentException>(() => LicenseCommandTierMap.RejectAlwaysFreemiumEnqueue("hy"));
            LicenseCommandTierMap.RejectAlwaysFreemiumEnqueue("hyG101");
        }

        [Fact]
        public void Mismatch_description_uses_builtin_as_source_of_truth()
        {
            Assert.True(LicenseCommandTierMap.TryDescribeMismatch(
                "gj", LicenseProductTier.Professional, "freemium", out var message));
            Assert.Contains("gj", message);
            Assert.Contains("freemium", message);
            Assert.Contains("Professional", message);

            Assert.False(LicenseCommandTierMap.TryDescribeMismatch(
                "gj", LicenseProductTier.Standard, null, out _));
            Assert.False(LicenseCommandTierMap.TryDescribeMismatch(
                "gj", LicenseProductTier.Standard, "standard", out _));
        }

        [Fact]
        public void Tampered_json_type_method_is_rejected()
        {
            Assert.True(CommandRouteResolver.TryRejectIfTampered(
                "CHECKWPF",
                "HyCADTool.Features.Elevation.Elevation3DCommand",
                "Execute",
                out var reason));
            Assert.Contains("CHECKWPF", reason);
            Assert.Contains("不一致", reason);

            Assert.False(CommandRouteResolver.TryRejectIfTampered(
                "CHECKWPF",
                "HyCADTool.Features.Misc.CheckWPFLoadedCommand",
                "CheckWPF",
                out _));
        }

        [Fact]
        public void Missing_runtime_json_is_not_treated_as_tamper()
        {
            Assert.False(CommandRouteResolver.TryRejectIfTampered("HY3", (string)null, null, out _));
            Assert.True(CommandRouteResolver.TryRejectIfTampered("definitely-not-a-command", "X", "Y", out var reason));
            Assert.Contains("没有键", reason);
        }

        [Fact]
        public void Generated_routes_match_commands_json_type_method()
        {
            var path = Path.Combine(RepoRoot(), "src", "ReCall", "commands.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var prop in doc.RootElement.GetProperty("commands").EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Object)
                    continue;
                var type = prop.Value.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
                    ? t.GetString()
                    : null;
                var method = prop.Value.TryGetProperty("method", out var m) && m.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(m.GetString())
                    ? m.GetString()
                    : "Execute";
                var route = CommandRoutes.Get(prop.Name);
                Assert.NotNull(route);
                Assert.Equal(type, route.Type);
                Assert.Equal(method, route.Method);
                AssertCtorMatches(prop.Value, route);
            }
        }

        [Fact]
        public void Confirmed_panel_keys_are_professional_and_standard_is_insufficient()
        {
            foreach (var key in new[] { "hyG101", "hyG16", "hyBaseRein" })
            {
                var route = CommandRoutes.Get(key);
                Assert.NotNull(route);
                Assert.False(string.IsNullOrWhiteSpace(route.Type));
                Assert.Equal("professional", route.Tier);
                Assert.Equal("professional", CommandTiers.GetRaw(key));

                var required = LicenseCommandTierMap.GetRequiredTier(key);
                Assert.Equal(LicenseProductTier.Professional, required);
                Assert.True(
                    (int)LicenseProductTier.Standard < (int)required,
                    key + " 在 Standard 许可下应被拒绝");
            }

            Assert.Equal("HyCADTool.Shell.Commands.ShowPanelCommand", CommandRoutes.Get("hyG101").Type);
            Assert.Equal("ShowG101Panel", CommandRoutes.Get("hyG101").Method);
            Assert.Equal("ShowG16Panel", CommandRoutes.Get("hyG16").Method);
            Assert.Equal("ShowBaseReinPanel", CommandRoutes.Get("hyBaseRein").Method);
        }

        [Fact]
        public void Confirmed_g_series_and_hydcel_are_professional_temporary_n_stay_standard()
        {
            foreach (var key in new[] { "gj", "gg", "ggj", "g1", "g2", "ge", "ge1", "gd", "gb", "gb1", "gb2", "HYDCEL" })
            {
                Assert.Equal("professional", CommandTiers.GetRaw(key));
                Assert.Equal(LicenseProductTier.Professional, LicenseCommandTierMap.GetRequiredTier(key));
            }

            foreach (var key in new[] { "N8", "N9", "N10", "N14", "N37" })
            {
                Assert.Equal("standard", CommandTiers.GetRaw(key));
                Assert.Equal(LicenseProductTier.Standard, LicenseCommandTierMap.GetRequiredTier(key));
            }
        }

        [Fact]
        public void TryPrepareDispatch_is_the_dispatcher_tamper_gate()
        {
            CommandRoute route;
            string error;

            Assert.False(CommandRouteResolver.TryPrepareDispatch(
                "CHECKWPF",
                new CommandEntry
                {
                    Type = "HyCADTool.Features.Elevation.Elevation3DCommand",
                    Method = "Execute",
                },
                out route,
                out error));
            Assert.Null(route);
            Assert.Contains("CHECKWPF", error);
            Assert.Contains("不一致", error);

            Assert.True(CommandRouteResolver.TryPrepareDispatch(
                "CHECKWPF",
                new CommandEntry
                {
                    Type = "HyCADTool.Features.Misc.CheckWPFLoadedCommand",
                    Method = "CheckWPF",
                },
                out route,
                out error));
            Assert.NotNull(route);
            Assert.Null(error);
            Assert.Equal("HyCADTool.Features.Misc.CheckWPFLoadedCommand", route.Type);
            Assert.Equal("CheckWPF", route.Method);

            Assert.True(CommandRouteResolver.TryPrepareDispatch("HY3", null, out route, out error));
            Assert.NotNull(route);
            Assert.Null(error);

            Assert.False(CommandRouteResolver.TryPrepareDispatch(
                "definitely-not-a-command",
                new CommandEntry { Type = "X", Method = "Y" },
                out route,
                out error));
            Assert.Null(route);
            Assert.Contains("未定义", error);
        }

        [Fact]
        public void Missing_pending_key_requires_standard()
        {
            var key = CommandRouteResolver.ResolvePendingLicenseKey(null);
            Assert.Equal(CommandRouteResolver.MissingPendingKey, key);
            Assert.Equal(LicenseProductTier.Standard, LicenseCommandTierMap.GetRequiredTier(key));
            Assert.Equal("gj", CommandRouteResolver.ResolvePendingLicenseKey(" gj "));
        }

        [Fact]
        public void GetRequiredTier_logs_when_runtime_json_disagrees()
        {
            var captured = new List<string>();
            var previous = LicenseCommandTierMap.MismatchLogger;
            var catalogPath = Path.Combine(AppContext.BaseDirectory, "commands.json");
            var previousJson = File.Exists(catalogPath) ? File.ReadAllText(catalogPath) : null;
            try
            {
                LicenseCommandTierMap.MismatchLogger = captured.Add;
                File.WriteAllText(catalogPath, @"{
  ""commands"": {
    ""HY3"": { ""tier"": ""freemium"", ""type"": ""X"", ""method"": ""Execute"" }
  }
}");
                HyCADTool.Shell.Commands.CommandCatalog.ForceReload();

                var required = LicenseCommandTierMap.GetRequiredTier("HY3");

                Assert.Equal(LicenseProductTier.Professional, required);
                Assert.Contains(captured, line => line.Contains("HY3") && line.Contains("以内置为准"));
            }
            finally
            {
                LicenseCommandTierMap.MismatchLogger = previous;
                if (previousJson == null)
                {
                    if (File.Exists(catalogPath))
                        File.Delete(catalogPath);
                }
                else
                {
                    File.WriteAllText(catalogPath, previousJson);
                }
                HyCADTool.Shell.Commands.CommandCatalog.ForceReload();
            }
        }

        private static void AssertCtorMatches(JsonElement command, CommandRoute route)
        {
            object[] expectedCtor = null;
            if (command.TryGetProperty("ctor", out var ctorEl) && ctorEl.ValueKind == JsonValueKind.Array)
            {
                var list = new List<object>();
                foreach (var item in ctorEl.EnumerateArray())
                    list.Add(ReadCtorValue(item));
                expectedCtor = list.ToArray();
            }

            if (expectedCtor == null)
                Assert.Null(route.Ctor);
            else
            {
                Assert.NotNull(route.Ctor);
                Assert.Equal(expectedCtor.Length, route.Ctor.Length);
                for (int i = 0; i < expectedCtor.Length; i++)
                    Assert.Equal(expectedCtor[i], route.Ctor[i]);
            }

            string[] expectedEnums = null;
            if (command.TryGetProperty("ctorEnumTypes", out var enumEl) && enumEl.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var item in enumEl.EnumerateArray())
                    list.Add(item.ValueKind == JsonValueKind.String ? item.GetString() : null);
                expectedEnums = list.ToArray();
            }

            if (expectedEnums == null)
                Assert.Null(route.CtorEnumTypes);
            else
                Assert.Equal(expectedEnums, route.CtorEnumTypes);
        }

        private static object ReadCtorValue(JsonElement item)
        {
            switch (item.ValueKind)
            {
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.String:
                    return item.GetString();
                case JsonValueKind.Number:
                    if (item.TryGetInt64(out var l) && l >= int.MinValue && l <= int.MaxValue && item.GetRawText().IndexOf('.') < 0)
                        return (int)l;
                    return item.GetDouble();
                case JsonValueKind.Null:
                    return null;
                default:
                    return item.GetRawText();
            }
        }

        private static Dictionary<string, string> ReadJsonTiers()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var modulesDir = Path.Combine(RepoRoot(), "src", "HyCADTool", "Modules");
            foreach (var path in Directory.GetFiles(modulesDir, "commands.json", SearchOption.AllDirectories))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (!doc.RootElement.TryGetProperty("commands", out var commands)
                    || commands.ValueKind != JsonValueKind.Object)
                    continue;
                foreach (var prop in commands.EnumerateObject())
                {
                    if (prop.Value.ValueKind != JsonValueKind.Object)
                        continue;
                    var raw = prop.Value.TryGetProperty("tier", out var tier) && tier.ValueKind == JsonValueKind.String
                        ? tier.GetString()
                        : null;
                    result[prop.Name] = Normalize(raw);
                }
            }
            return result;
        }

        private static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "standard";
            var t = raw.Trim();
            if (t.Equals("freemium", StringComparison.OrdinalIgnoreCase) || t.Equals("free", StringComparison.OrdinalIgnoreCase))
                return "freemium";
            if (t.Equals("standard", StringComparison.OrdinalIgnoreCase))
                return "standard";
            if (t.Equals("professional", StringComparison.OrdinalIgnoreCase) || t.Equals("pro", StringComparison.OrdinalIgnoreCase))
                return "professional";
            if (t.Equals("enterprise", StringComparison.OrdinalIgnoreCase) || t.Equals("ent", StringComparison.OrdinalIgnoreCase))
                return "enterprise";
            return "standard";
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
