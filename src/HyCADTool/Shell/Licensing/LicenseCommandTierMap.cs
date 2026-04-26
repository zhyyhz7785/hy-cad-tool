using System;
using System.Collections.Generic;
using HyCADTool.Licensing;

namespace HyCADTool.Shell.Licensing
{
    /// <summary>
    /// 命令 → 所需最低许可档。与 docs/archive/062、063 及销售套餐说明一致。
    /// </summary>
    public static class LicenseCommandTierMap
    {
        private static readonly HashSet<string> FreemiumCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "hy", "HyB", "_HyExec",
            "HYJC", "hySC",
            "HYOV", "HYOVSET",
            "hyLicense", "hyCmdList", "CHECKWPF",
        };

        public static LicenseProductTier GetRequiredTier(string commandKey)
        {
            if (string.IsNullOrWhiteSpace(commandKey)) return LicenseProductTier.Standard;
            var k = commandKey.Trim();
            if (FreemiumCommands.Contains(k)) return LicenseProductTier.Freemium;
            if (IsProfessionalCommand(k)) return LicenseProductTier.Professional;
            return LicenseProductTier.Standard;
        }

        private static bool IsProfessionalCommand(string k)
        {
            if (k.StartsWith("hyRoad", StringComparison.OrdinalIgnoreCase)) return true;
            if (k.StartsWith("HYpile", StringComparison.OrdinalIgnoreCase)) return true;
            if (k.StartsWith("hyPile", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(k, "HY3", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(k, "hySeg3", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
