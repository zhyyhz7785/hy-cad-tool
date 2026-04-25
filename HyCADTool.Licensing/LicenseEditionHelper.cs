using System;

namespace HyCADTool.Licensing
{
    public static class LicenseEditionHelper
    {
        public static LicenseProductTier TryParseTier(string edition)
        {
            if (string.IsNullOrWhiteSpace(edition)) return LicenseProductTier.Freemium;
            var s = edition.Trim();
            if (s.Equals("standard", StringComparison.OrdinalIgnoreCase)) return LicenseProductTier.Standard;
            if (s.Equals("professional", StringComparison.OrdinalIgnoreCase) || s.Equals("pro", StringComparison.OrdinalIgnoreCase))
                return LicenseProductTier.Professional;
            if (s.Equals("enterprise", StringComparison.OrdinalIgnoreCase) || s.Equals("ent", StringComparison.OrdinalIgnoreCase))
                return LicenseProductTier.Enterprise;
            return LicenseProductTier.Freemium;
        }
    }
}
