using System.IO;
using HyCADTool.Shared.Core;
using Newtonsoft.Json.Linq;
using Xunit;

namespace HyCADTool.LinuxTests
{
    public class AtomicFileTests
    {
        [Fact]
        public void WriteAllText_replaces_existing_and_keeps_backup()
        {
            var dir = Path.Combine(Path.GetTempPath(), "hycad-atomic-" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "cfg.json");
            try
            {
                File.WriteAllText(path, "{\"PileConfigData\":1}");
                AtomicFile.WriteAllText(path, "{\"GlobalConfiguration\":{\"Scale\":40},\"PileConfigData\":1}");

                var text = File.ReadAllText(path);
                var root = JObject.Parse(text);
                Assert.Equal(40, root["GlobalConfiguration"]["Scale"].Value<int>());
                Assert.Equal(1, root["PileConfigData"].Value<int>());
                Assert.True(File.Exists(path + ".bak"));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        [Fact]
        public void Merge_does_not_drop_sibling_sections()
        {
            var existing = JObject.Parse("{\"PileConfigData\":{\"x\":1},\"Other\":true}");
            existing["GlobalConfiguration"] = JObject.Parse("{\"ElevationLength\":2.5}");

            Assert.Equal(1, existing["PileConfigData"]["x"].Value<int>());
            Assert.True(existing["Other"].Value<bool>());
            Assert.Equal(2.5, existing["GlobalConfiguration"]["ElevationLength"].Value<double>());
        }

        [Fact]
        public void LoadOrCreate_missing_file_returns_empty_object()
        {
            var path = Path.Combine(Path.GetTempPath(), "hycad-missing-" + Path.GetRandomFileName() + ".json");
            var root = ConfigJsonRoot.LoadOrCreate(path);
            Assert.Empty(root.Properties());
            Assert.False(File.Exists(path));
        }

        [Fact]
        public void LoadOrCreate_corrupt_file_backs_up_and_throws_without_clobbering()
        {
            var dir = Path.Combine(Path.GetTempPath(), "hycad-corrupt-" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "config.json");
            const string original = "{\"PileConfigData\":{\"x\":1},";
            try
            {
                File.WriteAllText(path, original);
                var ex = Assert.Throws<InvalidOperationException>(() => ConfigJsonRoot.LoadOrCreate(path));
                Assert.Contains("拒绝覆盖", ex.Message);
                Assert.Equal(original, File.ReadAllText(path));

                var backups = Directory.GetFiles(dir, "config.json.corrupt-*");
                Assert.Single(backups);
                Assert.Equal(original, File.ReadAllText(backups[0]));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }
}
