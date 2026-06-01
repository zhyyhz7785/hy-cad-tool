namespace HyCADTool.Features.SpongeCity.Domain.Models
{
    /// <summary>
    /// LID 设施一行（与 Excel Sheet 设施配置 + 污染削减率 对应）。
    /// </summary>
    public class FacilityItem
    {
        /// <summary>序号（F1~F8）。</summary>
        public string Code { get; set; } = "";

        /// <summary>设施名称。</summary>
        public string Name { get; set; } = "";

        /// <summary>单位（m² / m³ / m）。</summary>
        public string Unit { get; set; } = "";

        /// <summary>规模数量（面积或容积或长度）。</summary>
        public double Quantity { get; set; }

        /// <summary>单位调蓄系数（如下沉绿地=下沉深 m，雨水花园 0.30 m³/m²，蓄水池=1.0 m³/m³）。</summary>
        public double UnitVolumeFactor { get; set; }

        /// <summary>SS 削减率（0~1）。</summary>
        public double EtaSs { get; set; }

        /// <summary>承担调蓄量 (m³) = Quantity × UnitVolumeFactor（容积类设施直接=Quantity）。</summary>
        public double ProvidedVolume { get; set; }

        /// <summary>说明 / 依据。</summary>
        public string Note { get; set; } = "";
    }

    /// <summary>8 类设施默认表（对齐 gen_sponge_housing_calc.py create_facility）。</summary>
    public static class FacilityCatalog
    {
        public static FacilityItem[] CreateDefaults() => new[]
        {
            new FacilityItem { Code = "F1", Name = "下沉式绿地（深度150mm）", Unit = "m²", Quantity = 2500, UnitVolumeFactor = 0.150, EtaSs = 0.50, Note = "下沉深度 150mm；DB13 §5.0.5" },
            new FacilityItem { Code = "F2", Name = "雨水花园（生物滞留）",   Unit = "m²", Quantity = 600,  UnitVolumeFactor = 0.30,  EtaSs = 0.80, Note = "蓄水深 0.20~0.30m；指南§6.1.3" },
            new FacilityItem { Code = "F3", Name = "透水铺装贡献调蓄",       Unit = "m²", Quantity = 3000, UnitVolumeFactor = 0.10,  EtaSs = 0.85, Note = "按蓄水层 0.10m；DB13 §5.0.6" },
            new FacilityItem { Code = "F4", Name = "绿色屋顶（简单式）",     Unit = "m²", Quantity = 0,    UnitVolumeFactor = 0.05,  EtaSs = 0.75, Note = "简单式 0.05~0.10 m³/m²" },
            new FacilityItem { Code = "F5", Name = "雨水蓄水池（钢混/模块）", Unit = "m³", Quantity = 250,  UnitVolumeFactor = 1.0,   EtaSs = 0.85, Note = "≥10mm 屋面雨量；DB13 §6.4.2" },
            new FacilityItem { Code = "F6", Name = "植草沟（转输+渗透）",     Unit = "m³", Quantity = 30,   UnitVolumeFactor = 1.0,   EtaSs = 0.60, Note = "断面 ≤1m²；DB13 §5.0.7" },
            new FacilityItem { Code = "F7", Name = "湿塘 / 雨水湿地",         Unit = "m³", Quantity = 0,    UnitVolumeFactor = 1.0,   EtaSs = 0.65, Note = "调蓄水位 <1.5m，出口控流" },
            new FacilityItem { Code = "F8", Name = "渗透塘 / 渗透井",         Unit = "m³", Quantity = 0,    UnitVolumeFactor = 1.0,   EtaSs = 0.75, Note = "K≥1×10⁻⁶ m/s；距建筑 ≥3m" },
        };
    }
}
