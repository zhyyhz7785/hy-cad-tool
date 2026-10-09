using System.Collections.Generic;
using HyCADTool.Features.SpongeCity.Domain.Models;

namespace HyCADTool.Features.SpongeCity.Domain.Standards
{
    /// <summary>
    /// DB13(J) 8457-2022 京津冀居住区模板。默认选中这一套，数值与改规范前的面板默认算例一致：
    /// 不舍入、SS 校核用 α×η综合、下沉绿地率 S6/(S2+S6+S7+S8)、透水率 S5/(S3+S4+S5)、
    /// 绿地率用手填值、外排 ψ 计入总评、调蓄取 max(W, 配建容积)。
    /// 设施规模不跟下垫面面积联动。
    /// </summary>
    public static class Db13SpongeStandard
    {
        public static SpongeStandard Create()
        {
            return new SpongeStandard
            {
                Id = SpongeStandardIds.Db13,
                DisplayName = "DB13(J) 8457 京津冀居住区",
                Citation = "DB13(J) 8457-2022 京津冀地方标准；建城函[2014]275 号；GB 50180-2018",
                WorkbookTitle = "海绵城市雨水控制利用计算总表  （住宅 / 商业小区）",
                PassVerdict = "★ 海绵城市强制性指标全部达标，可进入施工图深化阶段。",
                FailVerdict = "✗ 存在不达标项，请回到「设施配置」/「下垫面统计」调整规模或下垫面构成。",
                MultiplySsByAlpha = true,
                UseHardeningAllocation = true,
                Rounding = new SpongeRounding(),
                Defaults = new SpongeProjectDefaults
                {
                    ProjectName = "示例·海绵小区",
                    Location = "河北省衡水市",
                    Region = SpongeRegion.Hebei,
                    ProjectType = ProjectType.NewResidential,
                    TotalAreaM2 = 50000,
                    BuildingFootprintM2 = 12500,
                    GreenRatio = 0.30,
                    AlphaTarget = 0.75,
                    DesignRainfallMm = 22.0,
                    EtaSsTarget = 0.50,
                    InitialDiscardMm = 3.0,
                    ReturnPeriodYear = 3,
                    AnnualRainfallMm = 510.0,
                    StormIntensity = 280,
                    SoilPermeability = 18,
                    UnitSpongeCost = 80,
                    SinkenDepthMm = 150,
                },
                SunkenGreen = new AreaRatioRule
                {
                    Name = "下沉式绿地率",
                    NumeratorCodes = new[] { "S6" },
                    DenominatorCodes = new[] { "S2", "S6", "S7", "S8" },
                    Threshold = 0.50,
                    PassWhenDenominatorZero = false,
                    CountsTowardVerdict = true,
                    Reference = "DB13 §5.2.2 第 5 款",
                },
                PerviousPavement = new AreaRatioRule
                {
                    Name = "透水铺装率",
                    NumeratorCodes = new[] { "S5" },
                    DenominatorCodes = new[] { "S3", "S4", "S5" },
                    Threshold = 0.70,
                    PassWhenDenominatorZero = false,
                    CountsTowardVerdict = true,
                    Reference = "DB13 §5.2.2 第 6 款",
                    Documentation = "京津冀居住区：分母含混凝土路面、块石广场和透水砖。",
                },
                GreenRatio = new GreenRatioRule
                {
                    Name = "绿地率 (R2)",
                    UseManualInput = true,
                    Threshold = 0.30,
                    CountsTowardVerdict = true,
                    Reference = "GB 50180 §4.0.4",
                },
                PsiOutflow = new PsiOutflowRule
                {
                    Name = "外排径流系数 ψc",
                    NewProjectLimit = 0.40,
                    RebuildLimit = 0.50,
                    CountsTowardVerdict = true,
                    Reference = "DB13 §5.2.1",
                },
                Surfaces = Db13Surfaces(),
                Facilities = Db13Facilities(),
            };
        }

        private static List<SurfaceSpec> Db13Surfaces()
        {
            return new List<SurfaceSpec>
            {
                S("S1", "硬质屋面", 0.90, 12500, 1, "硬质屋面/未铺石子的平屋面"),
                S("S2", "种植屋面", 0.40, 0, 3, "DB13 §5.0.5 ≥2 万㎡公建强制"),
                S("S3", "混凝土路面", 0.90, 8000, 7, "车行道/消防车道"),
                S("S4", "块石广场", 0.65, 4000, 6, "园路/活动广场"),
                S("S5", "透水砖", 0.35, 3000, 2, "DB13 §5.0.6 人行 ≥70%"),
                S("S6", "下沉绿地", 0.15, 2500, 92, "DB13 §5.0.5 下沉 100~200mm"),
                S("S7", "普通绿地", 0.15, 12500, 84, "草坪/地被"),
                S("S8", "顶板薄覆土绿地", 0.30, 5500, 44, "覆土<600mm 顶板绿地"),
                S("S9", "水面", 1.00, 1500, 5, "DB13 ψ=1.0 景观水体"),
                S("S10", "其他", 0.50, 500, 8, "嵌草砖 / 裸土等"),
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

        private static List<FacilitySpec> Db13Facilities()
        {
            return new List<FacilitySpec>
            {
                F("F1", "下沉式绿地（深度150mm）", "m²", 2500, 0.150, 0.50, "下沉深度 150mm；DB13 §5.0.5", ""),
                F("F2", "雨水花园（生物滞留）", "m²", 600, 0.30, 0.80, "蓄水深 0.20~0.30m；指南§6.1.3", ""),
                F("F3", "透水铺装贡献调蓄", "m²", 3000, 0.10, 0.85, "按蓄水层 0.10m；DB13 §5.0.6", ""),
                F("F4", "绿色屋顶（简单式）", "m²", 0, 0.05, 0.75, "简单式 0.05~0.10 m³/m²", ""),
                F("F5", "雨水蓄水池（钢混/模块）", "m³", 250, 1.0, 0.85, "≥10mm 屋面雨量；DB13 §6.4.2", SpongeLayerNames.Surface("F5")),
                F("F6", "植草沟（转输+渗透）", "m³", 30, 1.0, 0.60, "断面 ≤1m²；DB13 §5.0.7", SpongeLayerNames.Surface("F6")),
                F("F7", "湿塘 / 雨水湿地", "m³", 0, 1.0, 0.65, "调蓄水位 <1.5m，出口控流", SpongeLayerNames.Surface("F7")),
                F("F8", "渗透塘 / 渗透井", "m³", 0, 1.0, 0.75, "K≥1×10⁻⁶ m/s；距建筑 ≥3m", SpongeLayerNames.Surface("F8")),
            };
        }

        private static FacilitySpec F(string code, string name, string unit, double qty, double factor, double eta, string note, string layer)
        {
            return new FacilitySpec
            {
                Code = code,
                Name = name,
                Unit = unit,
                DefaultQuantity = qty,
                UnitVolumeFactor = factor,
                EtaSs = eta,
                Note = note,
                LayerName = layer,
                AciColor = 7,
                AreaSourceCode = "",
                FactorFromSinkDepth = false,
                ResetFactorEachCompute = false,
            };
        }
    }
}
