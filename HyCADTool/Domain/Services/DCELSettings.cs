namespace HyCADTool.Domain.Services
{
    /// <summary>
    /// DCEL处理配置
    /// 控制曲线简化精度和输出模式
    /// </summary>
    public class DCELSettings
    {
        /// <summary>
        /// Arc简化分段数（null=自动：每15度一段，最少4段）
        /// </summary>
        public int? ArcSegmentCount { get; set; }
        
        /// <summary>
        /// Ellipse简化分段数（null=自动：根据周长估算，最少16段）
        /// </summary>
        public int? EllipseSegmentCount { get; set; }
        
        /// <summary>
        /// Spline简化分段数（null=自动：每个控制点区间8段，最少16段）
        /// </summary>
        public int? SplineSegmentCount { get; set; }
        
        /// <summary>
        /// 是否恢复原始曲线（true=恢复Arc为bulge段，false=使用简化线段）
        /// </summary>
        public bool RestoreOriginalCurves { get; set; }
        
        /// <summary>
        /// 默认配置（先测试简化线段输出，不恢复原曲线）
        /// </summary>
        public static DCELSettings Default => new DCELSettings
        {
            ArcSegmentCount = 6,         // 固定6段（便于观察）
            EllipseSegmentCount = 16,    // 固定16段
            SplineSegmentCount = 16,     // 固定16段
            RestoreOriginalCurves = false // ✅ 不恢复原曲线，先测试简化线段
        };
        
        /// <summary>
        /// 高精度配置
        /// </summary>
        public static DCELSettings HighPrecision => new DCELSettings
        {
            ArcSegmentCount = 16,
            EllipseSegmentCount = 64,
            SplineSegmentCount = 128,
            RestoreOriginalCurves = true
        };
        
        /// <summary>
        /// 低精度配置（性能优先）
        /// </summary>
        public static DCELSettings LowPrecision => new DCELSettings
        {
            ArcSegmentCount = 4,
            EllipseSegmentCount = 16,
            SplineSegmentCount = 32,
            RestoreOriginalCurves = false // 使用简化线段
        };
        
        /// <summary>
        /// 当前全局配置（单例）
        /// </summary>
        private static DCELSettings _current = Default;
        
        public static DCELSettings Current
        {
            get => _current;
            set => _current = value ?? Default;
        }
    }
}

