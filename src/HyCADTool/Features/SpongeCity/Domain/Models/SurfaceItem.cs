namespace HyCADTool.Features.SpongeCity.Domain.Models
{
    /// <summary>
    /// 单类下垫面参数（与 Excel Sheet2 一行对应）。
    /// </summary>
    public class SurfaceItem
    {
        /// <summary>编号（S1~S10）。</summary>
        public string Code { get; set; } = "";

        /// <summary>显示名称 / 默认图层名（如「硬质屋面」）。</summary>
        public string Name { get; set; } = "";

        /// <summary>实际图层名（用户可改，CAD 侧读图层时用此名）。</summary>
        public string LayerName { get; set; } = "";

        /// <summary>面积 (m²)；可由 hyscRA 自动填或人工填。</summary>
        public double Area { get; set; }

        /// <summary>雨量径流系数 ψ。</summary>
        public double Psi { get; set; }

        /// <summary>备注。</summary>
        public string Note { get; set; } = "";
    }
}
