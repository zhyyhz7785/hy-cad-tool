namespace HyCADTool.Features.DCEL.Domain.Services
{
    /// <summary>
    /// DCEL处理配置
    /// 控制曲线简化精度和输出模式
    /// </summary>
    public class DCELSettings
    {
        /// <summary>
        /// 几何容差（顶点合并 + 曲线段匹配统一使用，保证两端同源）
        /// </summary>
        public double Tolerance { get; set; } = 0.01;

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
        /// 是否输出详细性能分析（默认关闭，常用输出保持极简）
        /// </summary>
        public bool VerboseTiming { get; set; }

        /// <summary>
        /// 复制一份配置（设置命令在当前配置基础上修改，避免重置未涉及项）
        /// </summary>
        public DCELSettings Clone()
        {
            return new DCELSettings
            {
                Tolerance = Tolerance,
                ArcSegmentCount = ArcSegmentCount,
                EllipseSegmentCount = EllipseSegmentCount,
                SplineSegmentCount = SplineSegmentCount,
                RestoreOriginalCurves = RestoreOriginalCurves,
                VerboseTiming = VerboseTiming,
            };
        }

        /// <summary>
        /// 默认配置
        /// </summary>
        public static DCELSettings Default => new DCELSettings
        {
            Tolerance = 0.01,
            ArcSegmentCount = 6,
            EllipseSegmentCount = 16,
            SplineSegmentCount = 16,
            RestoreOriginalCurves = false,
            VerboseTiming = false,
        };

        /// <summary>
        /// 高精度配置
        /// </summary>
        public static DCELSettings HighPrecision => new DCELSettings
        {
            Tolerance = 0.001,
            ArcSegmentCount = 16,
            EllipseSegmentCount = 64,
            SplineSegmentCount = 128,
            RestoreOriginalCurves = true,
        };

        /// <summary>
        /// 低精度配置（性能优先）
        /// </summary>
        public static DCELSettings LowPrecision => new DCELSettings
        {
            Tolerance = 0.01,
            ArcSegmentCount = 4,
            EllipseSegmentCount = 16,
            SplineSegmentCount = 32,
            RestoreOriginalCurves = false,
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
