using NetTopologySuite.Geometries;
namespace HyCADTool.HelpClass
{
    public class MinArea
    {
        // 桩对象
        public Pile Pile { get; set; }
        // 区域轮廓，表示该区域的多边形轮廓
        public Polygon Contour { get; set; }
        // 是否优化完成
        public bool IsOptimized { get; set; }
        // 实际位移率
        public double ReplacementRate { get; set; }
        // 构造函数，初始化 MinArea 类的各个属性
        public MinArea(Pile pile, Polygon contour, bool isOptimized)
        {
            Pile = pile;
            Contour = contour;
        }
        // 可选方法：更新优化状态（例如，计算位移率后更新优化状态）
        public void CalculateDisplacementRate(double displacementThreshold)
        {
            ReplacementRate = Pile.PileArea / Contour.Area;
        }
    }
}
