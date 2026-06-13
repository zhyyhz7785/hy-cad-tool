using System;
using System.Collections.Generic;
using HyCADTool.Licensing;
using HyCADTool.Shell.Commands;

namespace HyCADTool.Shell.Licensing
{
    /// <summary>
    /// 命令 → 所需最低许可档。数据驱动：以 commands.json 每条命令的 "tier" 字段为准
    /// （freemium / standard / professional / enterprise，缺省 standard）。
    /// 新增/调档命令只改 commands.json，本文件不再随业务迭代修改。
    /// </summary>
    public static class LicenseCommandTierMap
    {
        /// <summary>
        /// 硬编码兜底：无论 commands.json 怎么写，这些键永远 Freemium。
        /// 保证用户在任何许可状态下都能打开面板与激活窗口（否则没机器码可发）。
        /// </summary>
        private static readonly HashSet<string> AlwaysFreemium = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "hy", "HyB", "_HyExec", "hyLicense",
        };

        public static LicenseProductTier GetRequiredTier(string commandKey)
        {
            if (string.IsNullOrWhiteSpace(commandKey)) return LicenseProductTier.Standard;
            var k = commandKey.Trim();
            if (AlwaysFreemium.Contains(k)) return LicenseProductTier.Freemium;

            try
            {
                var entry = CommandCatalog.Get(k);
                if (entry != null)
                    return ParseTier(entry.Tier);
            }
            catch
            {
                // commands.json 读取异常时落到默认档，不阻塞命令分发
            }
            return LicenseProductTier.Standard;
        }

        private static LicenseProductTier ParseTier(string tier)
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
    }
}
