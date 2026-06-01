using System.Collections.Generic;

namespace HyCADTool.Features.SpongeCity.Domain.Models
{
    /// <summary>地区，影响 §5.2.2 配建公式与 α 默认值。</summary>
    public enum SpongeRegion
    {
        Beijing,
        Tianjin,
        Hebei,
    }

    /// <summary>用地性质，影响 §5.2.4-1 α 默认值与 §5.2.2 硬化面积口径。</summary>
    public enum ProjectType
    {
        /// <summary>新建住宅小区。</summary>
        NewResidential,
        /// <summary>新建商业小区（绿地率 ≥25%）。</summary>
        NewCommercialHighGreen,
        /// <summary>新建商业小区（绿地率 &lt;25%）。</summary>
        NewCommercialLowGreen,
        /// <summary>改扩建老旧小区。</summary>
        RebuildOldResidential,
        /// <summary>改扩建其他小区。</summary>
        RebuildOtherResidential,
        /// <summary>改扩建公共建筑。</summary>
        RebuildPublic,
    }

    /// <summary>
    /// 海绵城市项目输入参数（与 Excel Sheet1 基本参数一一对应）。
    /// </summary>
    public class SpongeProjectInput
    {
        // ── 一、项目信息 ──
        public string ProjectName { get; set; } = "示例·海绵小区";
        public string Location { get; set; } = "河北省衡水市";
        public SpongeRegion Region { get; set; } = SpongeRegion.Hebei;
        public ProjectType ProjectType { get; set; } = ProjectType.NewResidential;

        /// <summary>总用地面积 F (m²)。</summary>
        public double TotalAreaM2 { get; set; } = 50000;

        /// <summary>建筑占地面积 (m²)（屋面投影；居住区配建口径用）。</summary>
        public double BuildingFootprintM2 { get; set; } = 12500;

        /// <summary>绿地率 (0~1)。</summary>
        public double GreenRatio { get; set; } = 0.30;

        public string Designer { get; set; } = "";
        public string Date { get; set; } = "";

        // ── 二、设计控制目标 ──
        /// <summary>年径流总量控制率目标 α (0~1)。</summary>
        public double AlphaTarget { get; set; } = 0.75;

        /// <summary>对应设计降雨厚度 h_y (mm)。</summary>
        public double DesignRainfallMm { get; set; } = 22.0;

        /// <summary>年径流污染削减率目标 η_SS (0~1)。</summary>
        public double EtaSsTarget { get; set; } = 0.50;

        /// <summary>初期弃流厚度 (mm)。</summary>
        public double InitialDiscardMm { get; set; } = 3.0;

        /// <summary>外排峰值流量控制重现期 (年)。</summary>
        public int ReturnPeriodYear { get; set; } = 3;

        // ── 三、衡水地区参数库 ──
        /// <summary>多年平均降雨量 (mm)。</summary>
        public double AnnualRainfallMm { get; set; } = 510.0;

        /// <summary>暴雨强度 P=3 年雨力 q (L/(s·hm²))。</summary>
        public double StormIntensity { get; set; } = 280;

        /// <summary>土壤渗透系数 K (mm/h)。</summary>
        public double SoilPermeability { get; set; } = 18;

        // ── 四、费率参数 ──
        /// <summary>单方海绵投资估算 (元/m²)。</summary>
        public double UnitSpongeCost { get; set; } = 80;

        /// <summary>下沉式绿地下沉深度 (mm)。</summary>
        public double SinkenDepthMm { get; set; } = 150;

        // ── 五、下垫面 & 设施 ──
        public List<SurfaceItem> Surfaces { get; set; } = new List<SurfaceItem>();
        public List<FacilityItem> Facilities { get; set; } = new List<FacilityItem>();

        /// <summary>构造时填充默认下垫面表 + 默认设施表。</summary>
        public static SpongeProjectInput CreateDefault()
        {
            var input = new SpongeProjectInput();
            // 10 类下垫面默认面积（与 py 算例对齐）
            var defaultAreas = new[] { 12500.0, 0, 8000, 4000, 3000, 2500, 12500, 5500, 1500, 500 };
            int i = 0;
            foreach (var e in SurfaceCatalog.Entries)
            {
                input.Surfaces.Add(new SurfaceItem
                {
                    Code = e.Code,
                    Name = e.Name,
                    LayerName = e.Name,
                    Area = i < defaultAreas.Length ? defaultAreas[i] : 0,
                    Psi = e.DefaultPsi,
                    Note = e.Note,
                });
                i++;
            }
            foreach (var f in FacilityCatalog.CreateDefaults())
                input.Facilities.Add(f);
            return input;
        }
    }
}
