namespace HyCADTool.Licensing
{
    /// <summary>有效许可档位（与 license JSON 的 edition 对应）。无许可=Freemium，仅可运行免费命令。</summary>
    public enum LicenseProductTier
    {
        /// <summary>无有效 license 或已过期，仅 Freemium 命令。</summary>
        Freemium = 0,
        Standard = 1,
        Professional = 2,
        Enterprise = 3
    }
}
