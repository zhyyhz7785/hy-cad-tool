namespace HyCADTool.Shell.Configuration.Global
{
    public class StylesConfig
    {
        public TextStyleConfig TextStyle { get; set; }
        public DimensionStyleConfig DimensionStyle { get; set; }
        public MLeaderStyleConfig MLeaderStyle { get; set; }

        public static StylesConfig CreateDefault()
        {
            return new StylesConfig
            {
                TextStyle = TextStyleConfig.CreateDefault("0_Hy_40", 1.0),
                DimensionStyle = DimensionStyleConfig.CreateDefault("0_Hy_40_Dim", "0_Hy_40", 1.0),
                MLeaderStyle = MLeaderStyleConfig.CreateDefault("0_Hy_40_Mleader", "0_Hy_40")
            };
        }
    }
}
