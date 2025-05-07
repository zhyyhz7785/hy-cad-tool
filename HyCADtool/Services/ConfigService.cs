// HyCADTool/Services/ConfigService.cs
using HyCADTool.Config;
using HyCADTool.Interfaces;
namespace HyCADTool.Services
{
    public class ConfigService : IConfigService
    {
        public double Scale { get; set; } = 40.0;
        public double DiameterOrEdge { get; set; } = 400.0;
        public double MinPileCenterDistance { get; set; } = 1200.0;
        public double InputDisplacementRate { get; set; } = 0.02;
        public double PileArrangeRate { get; set; } = 0.5;
        public double InputDistanceFromContour { get; set; } = 400.0;
        public (double up, double down, double left, double right) Margin { get; set; } = (400.0, 400.0, 400.0, 400.0);
        public void InitializeStyle()
        {
            BaseConfig.InitializeStyle(); // 暂时保留，后续可进一步抽象
        }
    }
}