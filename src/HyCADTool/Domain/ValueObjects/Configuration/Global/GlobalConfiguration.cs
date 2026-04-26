using System;

namespace HyCADTool.Domain.ValueObjects.Configuration.Global
{
    public class GlobalConfiguration
    {
        public ScaleConfig Scale { get; set; } = ScaleConfig.CreateDefault();
        public ToleranceConfig Tolerance { get; set; } = ToleranceConfig.CreateDefault();
        public PathConfig Paths { get; set; } = PathConfig.CreateDefault();
        public StylesConfig Styles { get; set; } = StylesConfig.CreateDefault();
        public double ElevationLength { get; set; } = 2.0;

        public static GlobalConfiguration CreateDefault()
        {
            return new GlobalConfiguration
            {
                Scale = ScaleConfig.CreateDefault(),
                Tolerance = ToleranceConfig.CreateDefault(),
                Paths = PathConfig.CreateDefault(),
                Styles = StylesConfig.CreateDefault(),
                ElevationLength = 2.0
            };
        }

        public bool IsValid(out string error)
        {
            error = null;
            if (Scale == null) { error = "Scale 为空"; return false; }
            if (Scale.Default < Scale.MinValue || Scale.Default > Scale.MaxValue)
            {
                error = "比例 Default 不在范围内";
                return false;
            }
            return true;
        }
    }
}
