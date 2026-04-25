using System;
using System.Collections.Generic;
using HyCADTool.Licensing;

namespace HyCADTool.Presentation.Licensing
{
    /// <summary>命令名（commands.json 键 / <see cref="ProductionDispatcher.Invoke"/> 参数，大小写不敏感）→ 最低许可档。</summary>
    public static class LicenseCommandTierMap
    {
        private static readonly HashSet<string> FreeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "hy", "HyB", "Hy", "_HyExec",
            "HYJC", "hySC",
            "HYOV", "HYOVSET",
            "hyLicense", "hyCmdList", "CHECKWPF"
        };

        public static LicenseProductTier GetRequiredTier(string key)
        {
            if (string.IsNullOrEmpty(key)) return LicenseProductTier.Standard;
            if (FreeKeys.Contains(key)) return LicenseProductTier.Freemium;

            if (key.StartsWith("hyRoad", StringComparison.OrdinalIgnoreCase))
                return LicenseProductTier.Professional;
            if (key.Equals("hySeg3", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("hyRoadSeg3", StringComparison.OrdinalIgnoreCase))
                return LicenseProductTier.Professional;
            if (key.StartsWith("HYpile", StringComparison.OrdinalIgnoreCase))
                return LicenseProductTier.Professional;
            if (key.Equals("HY3", StringComparison.OrdinalIgnoreCase))
                return LicenseProductTier.Professional;

            return LicenseProductTier.Standard;
        }
    }
}
