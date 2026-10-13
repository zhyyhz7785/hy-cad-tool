using System;
using System.Collections.Generic;
using HyCADTool.Licensing;
using HyCADTool.Shell.Commands;

namespace HyCADTool.Shell.Licensing
{
    /// <summary>
    /// 命令 → 所需最低许可档。授权只读编译进程序集的 <see cref="CommandTiers"/>。
    /// 运行时 commands.json 的 tier 若与内置不一致，以内置为准并记录日志。
    /// </summary>
    public static class LicenseCommandTierMap
    {
        /// <summary>
        /// 硬编码兜底：无论常量表怎么写，这些键永远 Freemium。
        /// 保证用户在任何许可状态下都能打开面板与激活窗口（否则没机器码可发）。
        /// </summary>
        private static readonly HashSet<string> AlwaysFreemium = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "hy", "HyB", "_HyExec", "hyLicense",
        };

        /// <summary>
        /// 档位不一致时的日志出口。测试可替换；默认写 <see cref="System.Diagnostics.Trace"/>。
        /// </summary>
        public static Action<string> MismatchLogger { get; set; } = DefaultMismatchLogger;

        public static LicenseProductTier GetRequiredTier(string commandKey)
        {
            if (string.IsNullOrWhiteSpace(commandKey)) return LicenseProductTier.Standard;
            var k = commandKey.Trim();
            if (AlwaysFreemium.Contains(k)) return LicenseProductTier.Freemium;

            var builtin = ParseTier(CommandTiers.GetRaw(k));
            TryLogRuntimeMismatch(k, builtin);
            return builtin;
        }

        /// <summary>
        /// 比较内置档与运行时 json 的原始 tier 文本。不一致时写出说明。
        /// </summary>
        public static bool TryDescribeMismatch(string commandKey, LicenseProductTier builtin, string runtimeTier, out string message)
        {
            message = null;
            var runtime = ParseTier(runtimeTier);
            if (runtime == builtin)
                return false;

            var shown = string.IsNullOrWhiteSpace(runtimeTier) ? "(缺省 standard)" : runtimeTier.Trim();
            message = "[HyCAD] commands.json 中命令「" + commandKey + "」的 tier=" + shown
                + " 与内置 " + builtin + " 不一致，以内置为准。";
            return true;
        }

        public static LicenseProductTier ParseTier(string tier)
        {
            if (string.IsNullOrWhiteSpace(tier)) return LicenseProductTier.Standard;
            var t = tier.Trim();
            if (t.Equals("freemium", StringComparison.OrdinalIgnoreCase) || t.Equals("free", StringComparison.OrdinalIgnoreCase))
                return LicenseProductTier.Freemium;
            if (t.Equals("standard", StringComparison.OrdinalIgnoreCase))
                return LicenseProductTier.Standard;
            if (t.Equals("professional", StringComparison.OrdinalIgnoreCase) || t.Equals("pro", StringComparison.OrdinalIgnoreCase))
                return LicenseProductTier.Professional;
            if (t.Equals("enterprise", StringComparison.OrdinalIgnoreCase) || t.Equals("ent", StringComparison.OrdinalIgnoreCase))
                return LicenseProductTier.Enterprise;
            return LicenseProductTier.Standard;
        }

        private static void TryLogRuntimeMismatch(string commandKey, LicenseProductTier builtin)
        {
            try
            {
                var entry = CommandCatalog.Get(commandKey);
                if (entry == null)
                    return;

                string message;
                if (!TryDescribeMismatch(commandKey, builtin, entry.Tier, out message))
                    return;

                var logger = MismatchLogger;
                if (logger != null)
                    logger(message);
            }
            catch
            {
                // 运行时 json 读失败不影响授权：以内置档为准
            }
        }

        private static void DefaultMismatchLogger(string message)
        {
            System.Diagnostics.Trace.WriteLine(message);
        }
    }
}
