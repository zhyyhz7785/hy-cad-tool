using System;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 绘图单位枚举
    /// </summary>
    public enum DrawingUnit
    {
        Millimeter,  // 毫米 (mm)
        Meter        // 米 (m)
    }

    /// <summary>
    /// HYOV 命令参数配置（单例模式）
    /// </summary>
    public class HyovSettings
    {
        private static HyovSettings _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static HyovSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new HyovSettings();
                        }
                    }
                }
                return _instance;
            }
        }

        private HyovSettings()
        {
            // 初始化默认值
            ResetToDefaults();
        }

        #region OVERKILL 参数

        /// <summary>
        /// 几何容差（用于点重合、共线判断）
        /// </summary>
        public double GeometricTolerance { get; set; }

        /// <summary>
        /// 平行线合并距离阈值（单位：图形单位）
        /// </summary>
        public double ParallelMergeDistance { get; set; }

        /// <summary>
        /// 是否启用预清理（合并重叠和平行线）
        /// </summary>
        public bool EnablePreClean { get; set; }

        #endregion

        #region FILLET 参数

        /// <summary>
        /// 绘图单位（毫米/米）
        /// </summary>
        public DrawingUnit Unit { get; set; }

        /// <summary>
        /// 最小线段长度阈值（单位：当前绘图单位）
        /// <br/>- 长度 &lt; MinLineLength 的线段将被删除（短线段）
        /// <br/>- 长度 ≥ MinLineLength 的线段将被保留（有效线段）
        /// </summary>
        public double MinLineLength { get; set; }

        /// <summary>
        /// 端点延伸最大距离（单位：图形单位）
        /// </summary>
        public double MaxExtendDistance { get; set; }

        /// <summary>
        /// 是否启用打断相交线段
        /// </summary>
        public bool EnableBreakLines { get; set; }

        /// <summary>
        /// 是否启用端点延伸
        /// </summary>
        public bool EnableExtendEndpoints { get; set; }

        /// <summary>
        /// 是否启用端点到线延伸
        /// </summary>
        public bool EnableExtendToLine { get; set; }

        #endregion

        #region 标记参数

        /// <summary>
        /// 独立端点检测容差（单位：图形单位）
        /// </summary>
        public double IndependentEndpointTolerance { get; set; }

        /// <summary>
        /// 标记缩放倍数
        /// </summary>
        public double MarkerScale { get; set; }

        /// <summary>
        /// 是否显示独立端点标记
        /// </summary>
        public bool ShowIndependentEndpoints { get; set; }

        #endregion

        #region 单位换算方法

        /// <summary>
        /// 获取单位换算系数（相对于毫米）
        /// </summary>
        public double GetUnitScale()
        {
            return Unit == DrawingUnit.Meter ? 0.001 : 1.0;
        }

        /// <summary>
        /// 获取实际使用的参数值（应用单位换算）
        /// </summary>
        public double GetScaledMinLineLength() => MinLineLength * GetUnitScale();
        public double GetScaledMaxExtendDistance() => MaxExtendDistance * GetUnitScale();
        public double GetScaledGeometricTolerance() => GeometricTolerance * GetUnitScale();
        public double GetScaledParallelMergeDistance() => ParallelMergeDistance * GetUnitScale();
        public double GetScaledIndependentEndpointTolerance() => IndependentEndpointTolerance * GetUnitScale();

        /// <summary>
        /// 切换绘图单位并自动调整参数值
        /// </summary>
        public void SwitchUnit(DrawingUnit newUnit)
        {
            if (Unit == newUnit) return;
            
            double conversionFactor = (newUnit == DrawingUnit.Meter) ? 0.001 : 1000.0;
            
            // 调整所有距离参数
            MinLineLength *= conversionFactor;
            MaxExtendDistance *= conversionFactor;
            GeometricTolerance *= conversionFactor;
            ParallelMergeDistance *= conversionFactor;
            IndependentEndpointTolerance *= conversionFactor;
            
            Unit = newUnit;
        }

        #endregion

        /// <summary>
        /// 重置为默认值
        /// </summary>
        public void ResetToDefaults()
        {
            // 绘图单位默认值
            Unit = DrawingUnit.Millimeter;

            // OVERKILL 默认值（毫米）
            GeometricTolerance = 1e-6;
            ParallelMergeDistance = 1.0;
            EnablePreClean = true;

            // FILLET 默认值（毫米）
            MinLineLength = 5.0;
            MaxExtendDistance = 10.0;
            EnableBreakLines = true;
            EnableExtendEndpoints = true;
            EnableExtendToLine = true;

            // 标记默认值（毫米）
            IndependentEndpointTolerance = 1.0;
            MarkerScale = 20.0;  // 标记缩放不受单位影响
            ShowIndependentEndpoints = true;
        }

        /// <summary>
        /// 复制当前设置
        /// </summary>
        public HyovSettings Clone()
        {
            return new HyovSettings
            {
                Unit = this.Unit,
                GeometricTolerance = this.GeometricTolerance,
                ParallelMergeDistance = this.ParallelMergeDistance,
                EnablePreClean = this.EnablePreClean,
                MinLineLength = this.MinLineLength,
                MaxExtendDistance = this.MaxExtendDistance,
                EnableBreakLines = this.EnableBreakLines,
                EnableExtendEndpoints = this.EnableExtendEndpoints,
                EnableExtendToLine = this.EnableExtendToLine,
                IndependentEndpointTolerance = this.IndependentEndpointTolerance,
                MarkerScale = this.MarkerScale,
                ShowIndependentEndpoints = this.ShowIndependentEndpoints
            };
        }

        /// <summary>
        /// 从另一个设置复制值
        /// </summary>
        public void CopyFrom(HyovSettings other)
        {
            this.Unit = other.Unit;
            this.GeometricTolerance = other.GeometricTolerance;
            this.ParallelMergeDistance = other.ParallelMergeDistance;
            this.EnablePreClean = other.EnablePreClean;
            this.MinLineLength = other.MinLineLength;
            this.MaxExtendDistance = other.MaxExtendDistance;
            this.EnableBreakLines = other.EnableBreakLines;
            this.EnableExtendEndpoints = other.EnableExtendEndpoints;
            this.EnableExtendToLine = other.EnableExtendToLine;
            this.IndependentEndpointTolerance = other.IndependentEndpointTolerance;
            this.MarkerScale = other.MarkerScale;
            this.ShowIndependentEndpoints = other.ShowIndependentEndpoints;
        }
    }
}






