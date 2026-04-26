namespace HyCADTool.Domain.ValueObjects.Configuration.Global
{
    public class ToleranceConfig
    {
        public double Double { get; set; } = 1e-2;
        public double Vector { get; set; } = 1e-2;
        public double Point { get; set; } = 1e-2;

        public static ToleranceConfig CreateDefault()
        {
            return new ToleranceConfig
            {
                Double = 1e-2,
                Vector = 1e-2,
                Point = 1e-2
            };
        }
    }
}
