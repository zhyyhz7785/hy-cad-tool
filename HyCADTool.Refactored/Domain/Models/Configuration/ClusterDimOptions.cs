namespace HyCADTool.Refactored.Domain.Models.Configuration
{
    /// <summary>
    /// 聚类标注参数配置（平台无关）
    /// </summary>
    public class ClusterDimOptions
    {
        /// <summary>最小标注间距（默认 3 x Scale）</summary>
        public double MinSpacing => 3 * Scale;

        /// <summary>标注线偏移高度（默认 5 x Scale）</summary>
        public double Offset => 5 * Scale;

        /// <summary>X方向标注是否转为上方（默认 false：下方）</summary>
        public bool XDirectionIsUp { get; set; } = false;

        /// <summary>Y方向标注是否转为右侧（默认 false：左侧）</summary>
        public bool YDirectionIsRight { get; set; } = false;

        /// <summary>比例因子（控制偏移量）</summary>
        public double Scale { get; set; } = 40;

        /// <summary>过滤重复标注的距离容差</summary>
        public double DistanceThreshold { get; set; } = 6000.0;
    }
}
