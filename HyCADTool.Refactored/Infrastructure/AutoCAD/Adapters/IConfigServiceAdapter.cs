namespace HyCADTool.Interfaces
{
    /// <summary>
    /// 配置服务接口（适配原项目）
    /// 用于 PilePanel 的兼容性
    /// </summary>
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

