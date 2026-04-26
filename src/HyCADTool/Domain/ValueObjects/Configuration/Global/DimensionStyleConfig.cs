namespace HyCADTool.Domain.ValueObjects.Configuration.Global
{
    public class DimensionStyleConfig
    {
        public string Name { get; set; } = "0_Hy_40_Dim";
        public string TextStyleName { get; set; } = "0_Hy_40";
        public double TextHeight { get; set; } = 2.5;
        public double ExtensionLineOffset { get; set; } = 1.0;
        public double ExtensionLineExtend { get; set; } = 1.0;
        public int DecimalPlaces { get; set; } = 0;
        public double TextGap { get; set; } = 1.0;
        public double ArrowSize { get; set; } = 1.0;
        public int TextDecimalPlaces { get; set; } = 0;

        public static DimensionStyleConfig CreateDefault(string name, string textStyle, double xScale = 1.0)
        {
            return new DimensionStyleConfig
            {
                Name = name,
                TextStyleName = textStyle,
                TextHeight = 2.5,
                ExtensionLineOffset = 1.0,
                ExtensionLineExtend = 1.0,
                DecimalPlaces = 0,
                TextGap = 1.0,
                ArrowSize = 1.0,
                TextDecimalPlaces = 0
            };
        }
    }
}
