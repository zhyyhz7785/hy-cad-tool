namespace HyCADTool.Refactored.Domain.Models.Settlement
{
    /// <summary>
    /// 单层土数据模型（从 Markdown 表格解析）
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

        /// <summary>状态描述</summary>
        public string Description { get; set; } = "";
    }
}
