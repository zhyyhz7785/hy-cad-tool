using System.Collections.Generic;
using HyCADTool.Features.SpongeCity.Domain.Models;

namespace HyCADTool.Features.SpongeCity.Domain.Standards
{
    /// <summary>规范 Id。新增规范时另起一个常量，不要复用这两个。</summary>
    public static class SpongeStandardIds
    {
        public const string Db13 = "DB13-8457";
        public const string Db42 = "DB42-1714";
    }

    /// <summary>校核行稳定代码，面板 / Markdown / Excel 都按这个对齐，不靠中文名。</summary>
    public static class SpongeCheckCodes
    {
        public const string Alpha = "alpha";
        public const string Volume = "volume";
        public const string Ss = "ss";
        public const string Green = "green";
        public const string Sunken = "sunken";
        public const string Pervious = "pervious";
        public const string Psi = "psi";
    }

    /// <summary>
    /// 图层短码。面积类 HY-海绵-S1…S10；容积类设施 HY-海绵-F5…F8。
    /// 文字图层 = 图层名 + 「-文字」。
    /// </summary>
    public static class SpongeLayerNames
    {
        public static string Surface(string code) => "HY-海绵-" + code;
        public static string Text(string layerName) => layerName + "-文字";
    }

    /// <summary>
    /// 舍入位数。-1 表示不舍入（保留全精度）。
    /// 舍入用 Excel ROUND 的远离 0（AwayFromZero），不用银行家舍入。
    /// </summary>
    public sealed class SpongeRounding
    {
        public int PsiDecimals { get; set; } = -1;
        public int AreaHm2Decimals { get; set; } = -1;
        public int RequiredVolumeDecimals { get; set; } = -1;
        public int FacilityVolumeDecimals { get; set; } = -1;
        public int EtaDecimals { get; set; } = -1;
    }

    /// <summary>
    /// 面积比：分子代码之和 / 分母代码之和，与阈值比较。
    /// 透水铺装率的分母在这里配置。DB42 只放人行/广场铺装代码，不放 S3 车行道、S4 碎石。
    /// </summary>
    public sealed class AreaRatioRule
    {
        public string Name { get; set; } = "";
        public string[] NumeratorCodes { get; set; } = new string[0];
        public string[] DenominatorCodes { get; set; } = new string[0];
        public double Threshold { get; set; }
        /// <summary>分母为 0 时算通过。DB42 透水：没有人行铺装则本条不适用，判定「满足」。</summary>
        public bool PassWhenDenominatorZero { get; set; }
        public string ZeroDenominatorVerdict { get; set; } = "不达标";
        public bool CountsTowardVerdict { get; set; } = true;
        public string Reference { get; set; } = "";
        /// <summary>给人和报告看的口径说明。计算不读这段文字。</summary>
        public string Documentation { get; set; } = "";
    }

    /// <summary>绿地率。手填比例，或按计绿公式 (部分代码×系数 + 全额代码) / 总用地。</summary>
    public sealed class GreenRatioRule
    {
        public string Name { get; set; } = "绿地率";
        public bool UseManualInput { get; set; } = true;
        public string[] FullCreditCodes { get; set; } = new string[0];
        public string[] PartialCreditCodes { get; set; } = new string[0];
        public double PartialCreditFactor { get; set; } = 0.30;
        public double Threshold { get; set; } = 0.30;
        public bool CountsTowardVerdict { get; set; } = true;
        public string ReferenceVerdict { get; set; } = "—参考";
        public string Reference { get; set; } = "";
    }

    /// <summary>外排综合径流系数上限。新建 / 改建两条线；不计入总评时判定写「—参考」。</summary>
    public sealed class PsiOutflowRule
    {
        public string Name { get; set; } = "外排径流系数 ψc";
        public double NewProjectLimit { get; set; } = 0.40;
        public double RebuildLimit { get; set; } = 0.50;
        public bool CountsTowardVerdict { get; set; } = true;
        public string ReferenceVerdict { get; set; } = "—参考";
        public string Reference { get; set; } = "";
    }

    public sealed class SurfaceSpec
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string LayerName { get; set; } = "";
        public double DefaultPsi { get; set; }
        public double DefaultArea { get; set; }
        public short AciColor { get; set; } = 7;
        public string Note { get; set; } = "";
        public string TextLayerName => SpongeLayerNames.Text(
            string.IsNullOrEmpty(LayerName) ? Name : LayerName);
    }

    public sealed class FacilitySpec
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Unit { get; set; } = "";
        public double DefaultQuantity { get; set; }
        public double UnitVolumeFactor { get; set; }
        /// <summary>为 true 时单位调蓄 = 下沉深度 mm / 1000，不使用 UnitVolumeFactor。</summary>
        public bool FactorFromSinkDepth { get; set; }
        /// <summary>每次计算把单位调蓄写回规范值。η 不回写。</summary>
        public bool ResetFactorEachCompute { get; set; }
        /// <summary>非空则每次计算把规模写成该下垫面面积。</summary>
        public string AreaSourceCode { get; set; } = "";
        public double EtaSs { get; set; }
        public string Note { get; set; } = "";
        /// <summary>非空则 hyscDL 只建层、不画几何。容积类 F5…F8 用。</summary>
        public string LayerName { get; set; } = "";
        public short AciColor { get; set; } = 7;
    }

    public sealed class SpongeProjectDefaults
    {
        public string ProjectName { get; set; } = "";
        public string Location { get; set; } = "";
        public SpongeRegion Region { get; set; } = SpongeRegion.Hebei;
        public ProjectType ProjectType { get; set; } = ProjectType.NewResidential;
        public double TotalAreaM2 { get; set; }
        public double BuildingFootprintM2 { get; set; }
        public double GreenRatio { get; set; }
        public double AlphaTarget { get; set; }
        public double DesignRainfallMm { get; set; }
        public double EtaSsTarget { get; set; }
        public double InitialDiscardMm { get; set; } = 3;
        public int ReturnPeriodYear { get; set; } = 3;
        public double AnnualRainfallMm { get; set; }
        public double StormIntensity { get; set; } = 280;
        public double SoilPermeability { get; set; } = 18;
        public double UnitSpongeCost { get; set; } = 80;
        public double SinkenDepthMm { get; set; } = 150;
    }

    /// <summary>
    /// 一套海绵计算规范：下垫面 ψ、设施 η、校核规则、总评口径、计算书/表头引用。
    /// 面板、Markdown、Excel 只消费 <see cref="Services.SpongeCalculationService.Compute"/> 的结果，不另写公式。
    /// </summary>
    public interface ISpongeStandard
    {
        string Id { get; }
        string DisplayName { get; }
        /// <summary>计算书和表头引用的规范原文口径（短）。</summary>
        string Citation { get; }
        string WorkbookTitle { get; }
        string PassVerdict { get; }
        string FailVerdict { get; }
        IReadOnlyList<SurfaceSpec> Surfaces { get; }
        IReadOnlyList<FacilitySpec> Facilities { get; }
        SpongeRounding Rounding { get; }
        AreaRatioRule SunkenGreen { get; }
        AreaRatioRule PerviousPavement { get; }
        GreenRatioRule GreenRatio { get; }
        PsiOutflowRule PsiOutflow { get; }
        /// <summary>true：SS 校核比较 α×η综合；false：直接比较 η综合。</summary>
        bool MultiplySsByAlpha { get; }
        /// <summary>true：调蓄需求还要和配建容积取较大值（DB13 §5.2.2）。</summary>
        bool UseHardeningAllocation { get; }
        SpongeProjectDefaults Defaults { get; }

        /// <summary>配建容积。默认实现见 <see cref="SpongeStandard.AllocationVolume"/>。</summary>
        double AllocationVolume(SpongeProjectInput input, double hardeningAreaM2);

        /// <summary>7 项校核。默认实现按上面的规则表生成；特殊规范可覆盖。</summary>
        List<CheckRow> BuildChecks(SpongeProjectInput input, SpongeResult result);

        SpongeProjectInput CreateInput();
    }
}
