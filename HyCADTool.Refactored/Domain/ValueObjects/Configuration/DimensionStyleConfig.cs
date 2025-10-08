namespace HyCADTool.Refactored.Domain.ValueObjects.Configuration
{
    /// <summary>
    /// 标注样式配置（平台无关）
    /// </summary>
    public class DimensionStyleConfig
    {
        /// <summary>
        /// 样式名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 关联的文本样式名称
        /// </summary>
        public string TextStyleName { get; set; }

        /// <summary>
        /// 文字高度
        /// </summary>
        public double TextHeight { get; set; }

        /// <summary>
        /// 尺寸线偏移
        /// </summary>
        public double DimLineOffset { get; set; }

        /// <summary>
        /// 尺寸界线超出长度
        /// </summary>
        public double ExtensionLineExtend { get; set; }

        /// <summary>
        /// 尺寸界线起点偏移
        /// </summary>
        public double ExtensionLineOffset { get; set; }

        /// <summary>
        /// 箭头大小
        /// </summary>
        public double ArrowSize { get; set; }

        /// <summary>
        /// 文字与尺寸线的间隙
        /// </summary>
        public double TextGap { get; set; }

        /// <summary>
        /// 小数位数
        /// </summary>
        public int DecimalPlaces { get; set; }

        /// <summary>
        /// 文字小数位数
        /// </summary>
        public int TextDecimalPlaces { get; set; }

        public DimensionStyleConfig()
        {
            Name = "Standard";
            TextStyleName = "Standard";
            TextHeight = 2.5;
            DimLineOffset = 0.0;
            ExtensionLineExtend = 1.0;
            ExtensionLineOffset = 1.0;
            ArrowSize = 1.0;
            TextGap = 1.0;
            DecimalPlaces = 0;
            TextDecimalPlaces = 0;
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static DimensionStyleConfig CreateDefault(string name, string textStyleName, double scale = 1.0)
        {
            return new DimensionStyleConfig
            {
                Name = name,
                TextStyleName = textStyleName,
                TextHeight = 2.5 * scale,
                DimLineOffset = 0.0,
                ExtensionLineExtend = 1.0 * scale,
                ExtensionLineOffset = 1.0 * scale,
                ArrowSize = 1.0 * scale,
                TextGap = 1.0 * scale,
                DecimalPlaces = 0,
                TextDecimalPlaces = 0
            };
        }

        public override string ToString()
        {
            return $"DimensionStyleConfig[{Name}, TextHeight={TextHeight:F2}]";
        }
    }
}

