namespace HyCADTool.Domain.ValueObjects.Configuration.Global
{
    public class TextStyleConfig
    {
        public string Name { get; set; } = "0_Hy_40";
        public string FontFileName { get; set; } = "tssdeng.shx";
        public string BigFontFileName { get; set; } = "hztxt.shx";
        public double TextSize { get; set; } = 2.5;
        public double XScale { get; set; } = 0.7;

        public static TextStyleConfig CreateDefault(string name, double xScale = 1.0)
        {
            return new TextStyleConfig
            {
                Name = name,
                FontFileName = "tssdeng.shx",
                BigFontFileName = "hztxt.shx",
                TextSize = 2.5,
                XScale = xScale
            };
        }
    }
}
