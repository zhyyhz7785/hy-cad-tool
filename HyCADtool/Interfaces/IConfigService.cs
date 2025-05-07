// HyCADTool/Interfaces/IConfigService.cs
namespace HyCADTool.Interfaces
{
    public interface IConfigService
    {
        double Scale { get; set; }
        double DiameterOrEdge { get; set; }
        double MinPileCenterDistance { get; set; }
        double InputDisplacementRate { get; set; }
        double PileArrangeRate { get; set; }
        double InputDistanceFromContour { get; set; }
        (double up, double down, double left, double right) Margin { get; set; }
        void InitializeStyle();
    }
}