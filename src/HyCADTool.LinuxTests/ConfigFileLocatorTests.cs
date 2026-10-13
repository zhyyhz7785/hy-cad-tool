using System.IO;
using HyCADTool.App.Bootstrap;
using Xunit;

namespace HyCADTool.LinuxTests
{
    public class ConfigFileLocatorTests
    {
        [Fact]
        public void User_file_wins_over_legacy()
        {
            var root = Path.Combine(Path.GetTempPath(), "hycad-cfg-" + Path.GetRandomFileName());
            var legacyDir = Path.Combine(root, "install");
            var userDir = Path.Combine(root, "appdata");
            Directory.CreateDirectory(legacyDir);
            Directory.CreateDirectory(userDir);
            var user = Path.Combine(userDir, "config.json");
            File.WriteAllText(Path.Combine(legacyDir, "config.json"), "{\"from\":\"legacy\"}");
            File.WriteAllText(user, "{\"from\":\"user\"}");
            try
            {
                var found = ConfigFileLocator.FindConfigJson(new[] { legacyDir }, user);
                Assert.Equal(user, found);
                Assert.Contains("user", File.ReadAllText(found));
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }

        [Fact]
        public void First_run_migrates_legacy_file_to_user_path()
        {
            var root = Path.Combine(Path.GetTempPath(), "hycad-cfg-" + Path.GetRandomFileName());
            var legacyDir = Path.Combine(root, "install");
            var userDir = Path.Combine(root, "appdata");
            Directory.CreateDirectory(legacyDir);
            var user = Path.Combine(userDir, "config.json");
            File.WriteAllText(Path.Combine(legacyDir, "config.json"), "{\"from\":\"legacy\"}");
            try
            {
                var found = ConfigFileLocator.FindConfigJson(new[] { legacyDir }, user);
                Assert.Equal(user, found);
                Assert.True(File.Exists(user));
                Assert.Contains("legacy", File.ReadAllText(user));
                Assert.True(File.Exists(Path.Combine(legacyDir, "config.json")));
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }

        [Fact]
        public void Missing_everywhere_returns_user_path_without_creating()
        {
            var user = Path.Combine(Path.GetTempPath(), "hycad-cfg-none-" + Path.GetRandomFileName(), "config.json");
            var found = ConfigFileLocator.FindConfigJson(new[] { Path.GetTempPath() }, user);
            Assert.Equal(user, found);
            Assert.False(File.Exists(user));
        }
    }
}
