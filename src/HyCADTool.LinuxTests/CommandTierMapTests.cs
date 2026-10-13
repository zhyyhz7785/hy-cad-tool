using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using HyCADTool.Licensing;
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
            Assert.Equal(LicenseProductTier.Standard, LicenseCommandTierMap.GetRequiredTier("gj"));
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

        private static Dictionary<string, string> ReadJsonTiers()
        {
            var path = Path.Combine(RepoRoot(), "src", "ReCall", "commands.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.GetProperty("commands").EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Object)
                    continue;
                var raw = prop.Value.TryGetProperty("tier", out var tier) && tier.ValueKind == JsonValueKind.String
                    ? tier.GetString()
                    : null;
                result[prop.Name] = Normalize(raw);
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
