using System;
using System.Collections.Generic;
using GenerateCommandTiers;
using Xunit;

namespace HyCADTool.LinuxTests
{
    public class GenerateCommandTiersTests
    {
        [Fact]
        public void Duplicate_command_key_across_modules_fails()
        {
            var modules = new List<(string ModuleId, string[] Keys)>
            {
                ("base", new[] { "Hy", "hyed" }),
                ("road", new[] { "hyRoad1", "Hy" }),
            };

            var ex = Assert.Throws<InvalidOperationException>(
                () => Program.EnsureUniqueModuleKeys(modules));
            Assert.Contains("命令键冲突", ex.Message);
            Assert.Contains("Hy", ex.Message);
            Assert.Contains("base", ex.Message);
            Assert.Contains("road", ex.Message);
        }

        [Fact]
        public void Distinct_command_keys_across_modules_pass()
        {
            var modules = new List<(string ModuleId, string[] Keys)>
            {
                ("base", new[] { "Hy", "hyed" }),
                ("road", new[] { "hyRoad1" }),
            };
            Program.EnsureUniqueModuleKeys(modules);
        }
    }
}
