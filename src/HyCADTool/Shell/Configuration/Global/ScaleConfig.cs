namespace HyCADTool.Shell.Configuration.Global
{
    public class ScaleConfig
    {
        public double Default { get; set; } = 40.0;
        public double MinValue { get; set; } = 1.0;
        public double MaxValue { get; set; } = 200.0;

        public static ScaleConfig CreateDefault()
        {
            return new ScaleConfig
            {
                Default = 40.0,
                MinValue = 1.0,
                MaxValue = 200.0
            };
        }
    }
}
