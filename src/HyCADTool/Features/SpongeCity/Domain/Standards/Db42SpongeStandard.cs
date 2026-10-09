using System.Collections.Generic;
using HyCADTool.Features.SpongeCity.Domain.Models;

namespace HyCADTool.Features.SpongeCity.Domain.Standards
{
    /// <summary>
    /// DB42/T 1714-2021 工业用地，对齐「工业园 v3」计算表。
    /// ψ 含 S10=0.30；雨水花园 η=0.85、单位调蓄 0.40 m；
    /// 下沉绿地率 (S6+S7)/(S6+S7+S8)；SS 直接比 η综合；
    /// 绿地率和外排 ψ 只作参考；总评 = 调蓄 + SS + 下沉 + 透水。
    /// V需按 ψ（3 位）、F（4 位 hm²）代入后保留 1 位小数。
    /// 面积类设施规模每次从 S2/S5/S6/S7 回写；η 保留用户修改。
    /// </summary>
    public static class Db42SpongeStandard
    {
        public static SpongeStandard Create()
        {
            return new SpongeStandard
            {
                Id = SpongeStandardIds.Db42,
                DisplayName = "DB42/T 1714 工业用地",
                Citation = "DB42/T 1714-2021（工业用地；附录 F 容积法、表 F.1 径流系数、表 F.2 削减率）",
                WorkbookTitle = "海绵城市雨水控制利用计算总表  （工业园）v3",
                PassVerdict = "★ 控制性指标达标：调蓄容积、污染削减率、下沉式绿地率、透水铺装率均满足要求，可进入施工图深化",
                FailVerdict = "✗ 存在不达标项，请回到 基本参数 / 下垫面分析 调整规模后，由设施配置自动引用",
                MultiplySsByAlpha = false,
                UseHardeningAllocation = false,
                Rounding = new SpongeRounding
                {
                    PsiDecimals = 3,
                    AreaHm2Decimals = 4,
                    RequiredVolumeDecimals = 1,
                    FacilityVolumeDecimals = 2,
                    EtaDecimals = 3,
                },
                Defaults = new SpongeProjectDefaults
                {
                    ProjectName = "共享特钢精深加工产业园项目",
                    Location = "湖北省广水市杨寨镇工业园（华鑫公司以东、黄杨线以北）",
                    Region = SpongeRegion.Hubei,
                    ProjectType = ProjectType.Industrial,
                    TotalAreaM2 = 1532,
                    BuildingFootprintM2 = 616.77,
                    GreenRatio = 152.86 / 1532.0,
                    AlphaTarget = 0.70,
                    DesignRainfallMm = 23.3,
                    EtaSsTarget = 0.70,
                    InitialDiscardMm = 3.0,
                    ReturnPeriodYear = 3,
                    AnnualRainfallMm = 1000,
                    StormIntensity = 280,
                    SoilPermeability = 18,
                    UnitSpongeCost = 80,
                    SinkenDepthMm = 200,
                },
                SunkenGreen = new AreaRatioRule
                {
                    Name = "下沉式绿地率",
                    NumeratorCodes = new[] { "S6", "S7" },
                    DenominatorCodes = new[] { "S6", "S7", "S8" },
                    Threshold = 0.50,
                    PassWhenDenominatorZero = false,
                    CountsTowardVerdict = true,
                    Reference = "DB42/T 1714 广义下沉式绿地（含生物滞留）；不含种植屋面",
                },
                PerviousPavement = new AreaRatioRule
                {
                    Name = "透水铺装率",
                    NumeratorCodes = new[] { "S5" },
                    // 只含人行/广场透水铺装。当前目录没有单独的不透水人行道代码，故分母仅 S5。
                    // S3 混凝土/沥青路面、S4 碎石场地故意不在此列。
                    DenominatorCodes = new[] { "S5" },
                    Threshold = 0.70,
                    PassWhenDenominatorZero = true,
                    ZeroDenominatorVerdict = "满足",
                    CountsTowardVerdict = true,
                    Reference = "DB42/T 1714 §6.3.4；S5=0 时本条不适用，判定为满足",
                    Documentation = "分母只含人行/广场铺装代码，不含 S3 机动车道和 S4 碎石场地。"
                        + "当前没有单独的不透水人行/广场代码，分母只有 S5：S5>0 时比率为 1，S5=0 时判定为满足。"
                        + "要把某种不透水人行或广场铺装计入分母，把它的代码加入 DenominatorCodes。",
                },
                GreenRatio = new GreenRatioRule
                {
                    Name = "绿地率（工业用地·参考）",
                    UseManualInput = false,
                    FullCreditCodes = new[] { "S6", "S7" },
                    PartialCreditCodes = new[] { "S5" },
                    PartialCreditFactor = 0.30,
                    Threshold = 0.30,
                    CountsTowardVerdict = false,
                    ReferenceVerdict = "—参考",
                    Reference = "工业用地无居住区 30% 强制；计绿 = S5×0.30 + S6 + S7",
                },
                PsiOutflow = new PsiOutflowRule
                {
                    Name = "外排综合径流系数（参考）",
                    NewProjectLimit = 0.40,
                    RebuildLimit = 0.50,
                    CountsTowardVerdict = false,
                    ReferenceVerdict = "—参考",
                    Reference = "工业高硬化场地为指导性指标，不计入总评",
                },
                Surfaces = Db42Surfaces(),
                Facilities = Db42Facilities(),
            };
        }

        private static List<SurfaceSpec> Db42Surfaces()
        {
            return new List<SurfaceSpec>
            {
                S("S1", "硬质屋面/未铺石子的平屋面", 0.90, 616.77, 1, "建筑屋面投影"),
                S("S2", "种植屋面/绿色屋顶", 0.40, 0, 3, "DB42/T 1714 表 F.1 绿化屋面 ψ=0.30~0.40"),
                S("S3", "混凝土或沥青路面", 0.90, 264.31, 7, "车行道、消防车道；不计入透水铺装率"),
                S("S4", "碎石场地", 0.65, 497.69, 6, "碎石场地；不计入透水铺装率"),
                S("S5", "透水砖铺装（嵌草砖停车）", 0.35, 0, 2, "S5=0 时透水铺装率判定为满足；表 F.1 ψ=0.29~0.36"),
                S("S6", "下沉式绿地（深度200mm）", 0.15, 0, 92, "狭义下沉式绿地；ψ=0.15"),
                S("S7", "雨水花园（生物滞留，深度400mm）", 0.15, 152.86, 84, "生物滞留；面积计入绿化用地"),
                S("S8", "覆土<600mm 的地库顶板绿地", 0.30, 0, 44, "覆土薄折算径流增加"),
                S("S9", "水面（景观水体）", 1.00, 0, 5, "表 F.1 水面 ψ=1.0"),
                S("S10", "其他（裸土）", 0.30, 0, 8, "非铺砌土面 ψ=0.30"),
            };
        }

        private static SurfaceSpec S(string code, string name, double psi, double area, short color, string note)
        {
            return new SurfaceSpec
            {
                Code = code,
                Name = name,
                LayerName = SpongeLayerNames.Surface(code),
                DefaultPsi = psi,
                DefaultArea = area,
                AciColor = color,
                Note = note,
            };
        }

        private static List<FacilitySpec> Db42Facilities()
        {
            return new List<FacilitySpec>
            {
                Area("F1", "下沉式绿地（深度200mm）", "m²", 0.20, true, "S6", 0.50,
                    "狭义下沉 ≤200mm；深度取面板下沉深度；规模 = S6", ""),
                Area("F2", "雨水花园（生物滞留，深度400mm）", "m²", 0.40, false, "S7", 0.85,
                    "蓄水深 0.40 m；表 F.2 复杂型生物滞留取 85%；规模 = S7", ""),
                Area("F3", "透水铺装贡献调蓄", "m²", 0.10, false, "S5", 0.85,
                    "蓄水层 0.10 m；规模 = S5", ""),
                Area("F4", "绿色屋顶（简单式）", "m²", 0.05, false, "S2", 0.75,
                    "简单式 0.05 m³/m²；规模 = S2", ""),
                Volume("F5", "雨水蓄水池（钢混/模块）", 0, 0.85, "末端调蓄；在面板改容积，不跟下垫面"),
                Volume("F6", "植草沟（转输+渗透）", 30, 0.60, "道路沿线转输兼渗透"),
                Volume("F7", "湿塘 / 雨水湿地", 0, 0.65, "区域低洼调蓄"),
                Volume("F8", "渗透塘 / 渗透井", 0, 0.75, "土壤渗透性好的区域"),
            };
        }

        private static FacilitySpec Area(string code, string name, string unit, double factor, bool fromDepth,
            string source, double eta, string note, string layer)
        {
            return new FacilitySpec
            {
                Code = code,
                Name = name,
                Unit = unit,
                DefaultQuantity = 0,
                UnitVolumeFactor = factor,
                FactorFromSinkDepth = fromDepth,
                ResetFactorEachCompute = true,
                AreaSourceCode = source,
                EtaSs = eta,
                Note = note,
                LayerName = layer,
                AciColor = 7,
            };
        }

        private static FacilitySpec Volume(string code, string name, double qty, double eta, string note)
        {
            return new FacilitySpec
            {
                Code = code,
                Name = name,
                Unit = "m³",
                DefaultQuantity = qty,
                UnitVolumeFactor = 1.0,
                FactorFromSinkDepth = false,
                ResetFactorEachCompute = true,
                AreaSourceCode = "",
                EtaSs = eta,
                Note = note,
                LayerName = SpongeLayerNames.Surface(code),
                AciColor = code == "F5" ? (short)30 : code == "F6" ? (short)40 : code == "F7" ? (short)50 : (short)60,
            };
        }
    }
}
