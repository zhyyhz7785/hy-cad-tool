namespace HyCADTool.Refactored.Domain.Models.Settlement
{
    /// <summary>
    /// 单层土数据模型（从 Markdown 表格解析，支持多表合并）
    /// </summary>
    public class SoilLayer
    {
        /// <summary>地层编号（如 "②"、"③₁"）</summary>
        public string Id { get; set; } = "";

        /// <summary>地层名称（如 "粉质黏土"）</summary>
        public string Name { get; set; } = "";

        /// <summary>平均厚度 (m)</summary>
        public double Thickness { get; set; }

        /// <summary>压缩模量 Es (MPa)</summary>
        public double Es { get; set; }

        /// <summary>回弹模量 Eci (MPa)，0 表示按倍率自动估算</summary>
        public double Eci { get; set; }

        /// <summary>状态描述</summary>
        public string Description { get; set; } = "";

        /// <summary>地基承载力特征值 fak (kPa)</summary>
        public double Fak { get; set; }

        /// <summary>天然重度 γ (kN/m³)</summary>
        public double Gamma { get; set; }

        /// <summary>标贯击数 N (击)</summary>
        public double Nspt { get; set; }

        /// <summary>桩侧阻力标准值 qsik (kPa)</summary>
        public double Qsik { get; set; }

        /// <summary>桩端阻力标准值 qpk (kPa)</summary>
        public double Qpk { get; set; }
    }
}
