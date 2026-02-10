namespace HyCADTool.Refactored.Domain.Models.Configuration
{
    /// <summary>
    /// 聚类配置（平台无关），控制 DBSCAN 聚类过程
    /// </summary>
    public class ClusterConfig
    {
        /// <summary>X方向上的聚类距离（单位：图纸单位）</summary>
        public double EpsilonX { get; set; } = 2000.0;

        /// <summary>Y方向上的聚类距离（单位：图纸单位）</summary>
        public double EpsilonY { get; set; } = 800.0;

        /// <summary>最小聚类点数（小于该值视为噪声）</summary>
        public int MinPoints { get; set; } = 3;

        /// <summary>是否生成点集的凸包多段线</summary>
        public bool GenerateConvexHull { get; set; } = true;

        /// <summary>
        /// 扩展轮廓边距（左、上、右、下，单位：图纸单位），用于 EnvelopeExpandedPolyline
        /// </summary>
        public (double Left, double Top, double Right, double Bottom) ExpandMargins { get; set; } = (300.0, 300.0, 300.0, 300.0);
    }
}
