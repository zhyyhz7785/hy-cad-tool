namespace HyCADTool.Domain.ValueObjects.Configuration.Global
{
    /// <summary>
    /// 文本样式配置（平台无关）
    /// </summary>
    public class TextStyleConfig
    {
        /// <summary>
        /// 样式名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 字体文件名（如 "tssdeng.shx"）
        /// </summary>
        public string FontFileName { get; set; }

        /// <summary>
        /// 大字体文件名（如 "hztxt.shx"）
        /// </summary>
        public string BigFontFileName { get; set; }

        /// <summary>
        /// 文字高度
        /// </summary>
        public double TextSize { get; set; }

        /// <summary>
        /// 宽度比例因子
        /// </summary>
        public double XScale { get; set; }

        /// <summary>
        /// 倾斜角度（弧度）
        /// </summary>
        public double ObliqueAngle { get; set; }

        public TextStyleConfig()
        {
            Name = "Standard";
            FontFileName = "txt.shx";
            BigFontFileName = string.Empty;
            TextSize = 2.5;
            XScale = 1.0;
            ObliqueAngle = 0.0;
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static TextStyleConfig CreateDefault(string name, double scale = 1.0)
        {
            return new TextStyleConfig
            {
                Name = name,
                FontFileName = "tssdeng.shx",
                BigFontFileName = "hztxt.shx",
                TextSize = 2.5 * scale,
                XScale = 0.7,
                ObliqueAngle = 0.0
            };
        }

        public override string ToString()
        {
            return $"TextStyleConfig[{Name}, Size={TextSize:F2}]";
        }
    }
}

